using CbmEngine.Abstractions;
using CbmEngine.Systems.Audio;
using CbmEngine.Systems.Native;
using ViceSharp.Abstractions;
using ViceSharp.Chips.Cpu;

namespace CbmEngine.Systems.Services;

public sealed class MusicService
{
    private const int ExecutionCycleBudget = 200_000;
    private const ushort Sentinel = 0xFFFE;
    private const ushort SentinelReturn = 0xFFFF;
    private const ushort ReservedZeroPageMin = 0x0002;
    private const ushort ReservedZeroPageMax = 0x00FF;

    private readonly ICommodoreMachine _machine;
    private readonly Mos6502 _cpu;
    private PsidProgram? _program;
    private int _song = 1;
    private bool _isPlaying;

    public bool IsPlaying => _isPlaying;
    public int CurrentSong => _song;
    public PsidHeader? CurrentHeader => _program?.Header;
    public bool NativeIrqDriverInstalled { get; private set; }
    public ushort NativeIrqStubAddress { get; private set; }

    public MusicService(ICommodoreMachine machine)
    {
        _machine = machine ?? throw new ArgumentNullException(nameof(machine));
        _cpu = (machine.Underlying.Devices.GetByRole(DeviceRole.Cpu) as Mos6502)
            ?? throw new InvalidOperationException("Machine CPU is not a Mos6502.");
    }

    public void Install(PsidProgram program, int song = 1)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (song < 1 || song > program.Header.SongCount)
            throw new ArgumentOutOfRangeException(nameof(song));
        ValidatePlacement(program);

        var payload = program.Payload.Span;
        int loadAddr = program.Header.LoadAddress;
        int endAddr = loadAddr + payload.Length - 1;
        if (endAddr > 0xFFFF)
            throw new PsidPlacementException($"PSID payload from ${loadAddr:X4} + {payload.Length} bytes overflows past $FFFF.");

        _machine.Memory.WriteRange((ushort)loadAddr, payload);

        _program = program;
        _song = song;
        RunRoutine(program.Header.InitAddress, (byte)(song - 1));
        _isPlaying = program.Header.PlayAddress != 0;
    }

    public void Tick()
    {
        if (!_isPlaying || _program is null) return;
        if (NativeIrqDriverInstalled) return;
        RunRoutine(_program.Header.PlayAddress, 0);
    }

    public byte[] InjectIrqStubBytes(ushort stubAddress = 0xC000)
    {
        if (_program is null) throw new InvalidOperationException("Install PSID first.");
        if (_program.Header.PlayAddress == 0) throw new InvalidOperationException("PSID has no play address.");

        int end = stubAddress + IrqPlayerStub.Size - 1;
        if (end > 0xFFFF) throw new ArgumentOutOfRangeException(nameof(stubAddress), "Stub extends past $FFFF.");
        if (stubAddress <= 0x00FF) throw new ArgumentOutOfRangeException(nameof(stubAddress), "Stub overlaps reserved zero page.");

        var bytes = IrqPlayerStub.Build(stubAddress, _program.Header.PlayAddress);
        _machine.Memory.WriteRange(stubAddress, bytes);
        NativeIrqStubAddress = stubAddress;
        return bytes;
    }

    public void InstallNativeIrqDriver(ushort stubAddress = 0xC000)
    {
        InjectIrqStubBytes(stubAddress);
        RunRoutine(stubAddress, aReg: 0);
        NativeIrqDriverInstalled = true;
    }

    public void SetSong(int subTune)
    {
        if (_program is null) throw new InvalidOperationException("No PSID installed.");
        Install(_program, subTune);
    }

    public void Stop()
    {
        _isPlaying = false;
        if (NativeIrqDriverInstalled)
        {
            _machine.Bus.Write(0x0314, 0x31);
            _machine.Bus.Write(0x0315, 0xEA);
            NativeIrqDriverInstalled = false;
        }
        _machine.Sound.SilenceAll();
    }

    private void ValidatePlacement(PsidProgram program)
    {
        int load = program.Header.LoadAddress;
        int end = load + program.Payload.Length - 1;
        if (load == 0 && program.Payload.Length >= 2)
        {
            load = program.Payload.Span[0] | (program.Payload.Span[1] << 8);
            end = load + program.Payload.Length - 3;
        }
        if (load <= ReservedZeroPageMax && end >= ReservedZeroPageMin)
            throw new PsidPlacementException($"PSID load range [${load:X4}..${end:X4}] overlaps engine-reserved zero page [${ReservedZeroPageMin:X4}..${ReservedZeroPageMax:X4}].");
    }

    private void RunRoutine(ushort routine, byte aReg)
    {
        ushort savedPc = _cpu.PC;
        byte savedA = _cpu.A, savedX = _cpu.X, savedY = _cpu.Y, savedS = _cpu.S, savedP = _cpu.Flags;

        _cpu.A = aReg;
        _cpu.X = 0;
        _cpu.Y = 0;
        _cpu.S = 0xFD;
        _machine.Bus.Write((ushort)(0x0100 + _cpu.S), 0xFF);
        _cpu.S--;
        _machine.Bus.Write((ushort)(0x0100 + _cpu.S), (byte)(Sentinel & 0xFF));
        _cpu.S--;
        _cpu.PC = routine;

        try
        {
            int cyclesUsed = 0;
            while (_cpu.PC != SentinelReturn)
            {
                cyclesUsed += _cpu.ExecuteInstruction();
                if (cyclesUsed > ExecutionCycleBudget)
                    throw new PsidExecutionException($"PSID routine at ${routine:X4} exceeded {ExecutionCycleBudget} cycle budget (likely infinite loop).");
            }
        }
        finally
        {
            _cpu.PC = savedPc;
            _cpu.A = savedA;
            _cpu.X = savedX;
            _cpu.Y = savedY;
            _cpu.S = savedS;
            _cpu.Flags = savedP;
        }
    }
}
