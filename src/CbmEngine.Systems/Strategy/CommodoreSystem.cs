using CbmEngine.Abstractions;
using CbmEngine.Systems.Services;
using ViceSharp.Abstractions;
using ViceSharp.Architectures.C64;
using ViceSharp.Chips.Cpu;
using ViceSharp.Core;

namespace CbmEngine.Systems.Strategy;

public static class CommodoreSystem
{
    public static readonly IReadOnlyList<string> SupportedProfileIds = new[] { "c64", "c64c", "ntsc", "newntsc" };

    /// <summary>Build a machine using ROMs auto-discovered via <see cref="RomDiscovery"/>.</summary>
    public static ICommodoreMachine Build(string profileId) => Build(profileId, RomDiscovery.Discover());

    public static ICommodoreMachine Build(string profileId, IRomProvider roms)
        => Build(profileId, roms, audioBackend: null);

    /// <summary>
    /// Build a machine with an optional live-audio backend. When supplied, the SID streams samples to
    /// it during emulation (the builder also runs <c>ConfigureAudioClock</c> for the profile's master
    /// clock), so a host that advances RunFrame at the machine's refresh rate gets correctly paced
    /// audio. Pass null for a silent machine (parity / headless rigs).
    /// </summary>
    public static ICommodoreMachine Build(string profileId, IRomProvider roms, IAudioBackend? audioBackend)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(roms);

        var profile = ResolveProfile(profileId);
        var descriptor = new C64Descriptor(profile);
        var machine = new ArchitectureBuilder(roms, audioBackend).Build(descriptor);
        return new CommodoreMachine(machine, profile);
    }

    /// <summary>
    /// Build a ROM-less C64 that needs no VICE ROM images. BASIC and CHARGEN are supplied as blank
    /// (zeroed) images under non-canonical names - so the ROM loader's size check passes but its MD5
    /// check is skipped - and the KERNAL is omitted entirely (<see cref="C64ViceRomNames.KernalNone"/>).
    /// The managed VIC-II still rasterises a host-driven multicolour bitmap frame (a bitmap at $2000
    /// never reads CHARGEN) and the SID still produces audio (e.g. a PSID driven by a stand-alone RAM
    /// IRQ). The 6510 is parked with the KERNAL banked out and RAM hardware vectors installed, so the
    /// absent-KERNAL reset vector can never run garbage into VIC/SID state while a host drives
    /// <c>RunFrame</c>. Use this to render and play music without shipping the copyrighted VICE ROMs.
    /// </summary>
    /// <param name="profileId">VIC timing profile; one of <see cref="SupportedProfileIds"/>.</param>
    /// <param name="audioBackend">Optional live SID audio sink; null for a silent machine.</param>
    public static ICommodoreMachine BuildRomless(string profileId, IAudioBackend? audioBackend = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        var profile = ResolveProfile(profileId) with
        {
            BasicRomName = "basic-romless.bin",
            CharacterRomName = "chargen-romless.bin",
            KernalRomName = C64ViceRomNames.KernalNone,
        };

        var descriptor = new C64Descriptor(profile);
        var machine = new ArchitectureBuilder(new BlankRomProvider(), audioBackend).Build(descriptor);
        var commodore = new CommodoreMachine(machine, profile);

        // Establish VIC bank 0 the way the KERNAL boot does. The VIC resets to bank 3, but a display's
        // screen RAM ($0400) and bitmap ($2000) live in bank 0; without this the VIC fetches content
        // from bank 3 and only the background register ($D021) renders (everything else is black).
        InitVicBank0(commodore);

        // With no KERNAL the reset vector reads RAM ($0000), so an un-parked CPU would execute garbage
        // and could scribble on VIC/SID state during RunFrame. Park it: bank the KERNAL region out,
        // plant RAM hardware vectors, and idle the 6510 in a JMP* loop.
        new KernalBypass(commodore).Engage();

        return commodore;
    }

    // Program CIA2 for VIC bank 0 through the CPU. The bank moves on the CIA port-A output change that a
    // CPU store triggers (VIC bank = 3 - ($DD00 & 3)); a raw bus write does not fire that path, which is
    // why the display must not just poke $DD00 directly. Runs before the CPU is parked.
    private static void InitVicBank0(ICommodoreMachine machine)
    {
        var cpu = machine.Underlying.Devices.GetByRole(DeviceRole.Cpu) as Mos6502
            ?? throw new InvalidOperationException("ROM-less VIC-bank init requires a Mos6502 CPU.");

        const ushort stub = 0xC000; // free RAM below the KernalBypass park/handler region ($CE00+)
        machine.Memory.WriteRange(stub, new byte[]
        {
            0xA9, 0x3F,       // LDA #$3F
            0x8D, 0x02, 0xDD, // STA $DD02  (CIA2 DDRA: drive the VIC-bank bits as outputs)
            0xA9, 0x03,       // LDA #$03
            0x8D, 0x00, 0xDD, // STA $DD00  (bits 0-1 = %11 -> VIC bank 0)
        });

        cpu.PC = stub;
        for (int i = 0; i < 4; i++) cpu.ExecuteInstruction(); // LDA, STA, LDA, STA
    }

    public static C64MachineProfile ResolveProfile(string profileId)
    {
        if (C64MachineProfiles.TryResolve(profileId, out var profile) &&
            SupportedProfileIds.Contains(profileId, StringComparer.OrdinalIgnoreCase))
        {
            return profile;
        }

        throw new ArgumentException(
            $"Unknown or unsupported CbmEngine profile '{profileId}'. Supported v1 ids: {string.Join(", ", SupportedProfileIds)}.",
            nameof(profileId));
    }
}
