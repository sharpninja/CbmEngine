using CbmEngine.Abstractions;
using ViceSharp.Abstractions;
using ViceSharp.Chips.Cpu;

namespace CbmEngine.Systems.Services;

/// <summary>
/// Runs the emulated C64 with the KERNAL ROM banked out of $E000-$FFFF - the official C64 takeover via
/// the $01 processor port - so KERNAL routines can never run and fight a host that drives the machine
/// itself. The motivating case: the screen editor's CINT (~$E5B0) periodically resets the VIC to its
/// char-mode / blue-border defaults partway down a frame, flashing garbage on a host that owns the VIC.
///
/// After <see cref="Engage"/> the 6510 is parked in a one-byte RAM loop and the hardware vectors live
/// in RAM: IRQ routes to a caller-supplied handler (e.g. a music player's PLAY wrapper) or, when none
/// is given, a planted stub that merely acknowledges the VIC/CIA interrupt and returns. BASIC and I/O
/// stay mapped ($01=$35) so $D000-$DFFF remains VIC/SID/CIA.
///
/// Call AFTER the KERNAL has booted, so the CIA1 timer that generates the ~50/60 Hz IRQ this rides is
/// already running. Reversible with <see cref="Disengage"/>.
/// </summary>
public sealed class KernalBypass
{
    // Default RAM layout in the free $C000 gap (clear of a $C000 music payload of up to a few KB).
    public const ushort DefaultParkAddress = 0xCE00;
    public const ushort DefaultIrqHandlerAddress = 0xCE10;
    public const ushort DefaultNmiAddress = 0xCE40;

    private const byte FlagInterruptDisable = 0x04;
    private const byte BankKernalOut = 0x35;   // LORAM=1 (BASIC in), HIRAM=0 (KERNAL out), CHAREN=1 (I/O in)
    private const byte BankDefault = 0x37;     // BASIC + KERNAL + I/O (power-up map)

    private readonly ICommodoreMachine _machine;
    private readonly Mos6502 _cpu;

    public KernalBypass(ICommodoreMachine machine)
    {
        _machine = machine ?? throw new ArgumentNullException(nameof(machine));
        _cpu = (machine.Underlying.Devices.GetByRole(DeviceRole.Cpu) as Mos6502)
            ?? throw new InvalidOperationException("Machine CPU is not a Mos6502.");
    }

    /// <summary>True between <see cref="Engage"/> and <see cref="Disengage"/> (KERNAL ROM banked out).</summary>
    public bool Engaged { get; private set; }

    /// <summary>
    /// Bank the KERNAL ROM out, install RAM hardware vectors, and park the 6510 in a RAM idle loop with
    /// interrupts enabled. <paramref name="irqHandlerAddress"/> is the RAM IRQ handler to route to (it
    /// must save/restore any registers it uses and end in RTI); when null a minimal "ack VIC+CIA, RTI"
    /// stub is planted at <see cref="DefaultIrqHandlerAddress"/> so the machine survives the CIA timer
    /// IRQ without doing work. All addresses must be outside zero page.
    /// </summary>
    public void Engage(
        ushort? irqHandlerAddress = null,
        ushort parkAddress = DefaultParkAddress,
        ushort nmiAddress = DefaultNmiAddress)
    {
        if (parkAddress <= 0x00FF || nmiAddress <= 0x00FF)
            throw new ArgumentException("Bypass addresses must be outside zero page.");

        ushort irq = irqHandlerAddress ?? PlantAckOnlyIrqStub(DefaultIrqHandlerAddress);
        if (irq <= 0x00FF)
            throw new ArgumentException("IRQ handler address must be outside zero page.", nameof(irqHandlerAddress));

        // A park loop (JMP *) and an NMI that just returns.
        _machine.Memory.WriteRange(parkAddress,
            new byte[] { 0x4C, (byte)(parkAddress & 0xFF), (byte)(parkAddress >> 8) });
        _machine.Memory.WriteRange(nmiAddress, new byte[] { 0x40 });  // RTI

        // Bank the KERNAL out. Writing $01 rebuilds the PLA page table, so $E000-$FFFF now reads RAM.
        _machine.Bus.Write(0x0001, BankKernalOut);

        // Hardware vectors are RAM now: IRQ -> handler, NMI -> rti, RESET -> park.
        WriteVector(0xFFFE, irq);
        WriteVector(0xFFFA, nmiAddress);
        WriteVector(0xFFFC, parkAddress);

        // Park the CPU in the RAM loop with a clean stack and interrupts enabled.
        _cpu.PC = parkAddress;
        _cpu.S = 0xFF;
        _cpu.Flags = (byte)(_cpu.Flags & ~FlagInterruptDisable);

        Engaged = true;
    }

    /// <summary>
    /// Restore the KERNAL ROM ($01=$37) and the standard KERNAL IRQ vector ($0314/$0315 -> $EA31). The
    /// CPU is left parked; redirect it back into ROM if KERNAL execution must resume.
    /// </summary>
    public void Disengage()
    {
        if (!Engaged) return;
        _machine.Bus.Write(0x0001, BankDefault);
        _machine.Bus.Write(0x0314, 0x31);
        _machine.Bus.Write(0x0315, 0xEA);
        Engaged = false;
    }

    private ushort PlantAckOnlyIrqStub(ushort address)
    {
        // PHA / LDA #$01 / STA $D019 / LDA $DC0D / PLA / RTI -- acknowledge both sources, nothing else.
        _machine.Memory.WriteRange(address, new byte[]
        {
            0x48,                   // PHA
            0xA9, 0x01,             // LDA #$01
            0x8D, 0x19, 0xD0,       // STA $D019  (ack VIC)
            0xAD, 0x0D, 0xDC,       // LDA $DC0D  (ack CIA1)
            0x68,                   // PLA
            0x40,                   // RTI
        });
        return address;
    }

    private void WriteVector(ushort vectorAddress, ushort target)
    {
        _machine.Bus.Write(vectorAddress, (byte)(target & 0xFF));
        _machine.Bus.Write((ushort)(vectorAddress + 1), (byte)(target >> 8));
    }
}
