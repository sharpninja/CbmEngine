using System.Collections.Immutable;
using CbmEngine.Abstractions;
using ViceSharp.Abstractions;

namespace CbmEngine.Tests.Unit.CbmFr;

/// <summary>
/// Minimal <see cref="ICommodoreMachine"/> test double for the CBMFR work. Provides a real
/// <see cref="RecordingMemory"/>, a <see cref="IBus"/> that routes into that same store (so writes
/// made by <c>LineDirector</c> are visible via <c>ReadIo</c>), a no-op clock, and capabilities.
/// Members not exercised by the code under test throw <see cref="NotSupportedException"/>.
/// </summary>
internal sealed class FakeMachine : ICommodoreMachine
{
    public RecordingMemory MemoryStore { get; } = new();
    private readonly FakeBus _bus;
    private readonly FakeClock _clock = new();

    public FakeMachine(int rasterLines = 312, int cyclesPerLine = 63)
    {
        _bus = new FakeBus(MemoryStore);
        Capabilities = new MachineCapabilities(
            "c64", "Fake C64", VideoStandard.Pal, cyclesPerLine, rasterLines,
            985248, 50.0, SidModel.Mos6581, ImmutableArray<uint>.Empty);
    }

    public IMemoryService Memory => MemoryStore;
    public IBus Bus => _bus;
    public IClock Clock => _clock;
    public MachineCapabilities Capabilities { get; }

    public IMachine Underlying => throw new NotSupportedException();
    public IVideoChip VideoChip => throw new NotSupportedException();
    public IAudioChip? AudioChip => null;
    public IKeyboardMatrix? KeyboardMatrix => null;
    public ISoundChipStrategy Sound => throw new NotSupportedException();
    public IPubSub PubSub => throw new NotSupportedException();
    public void RunFrame() => throw new NotSupportedException();

    private sealed class FakeBus(RecordingMemory mem) : IBus
    {
        public void Write(ushort address, byte value)
        {
            if (mem.IsIoAddress(address))
            {
                Span<byte> b = stackalloc byte[1];
                b[0] = value;
                mem.WriteIo(address, b);
            }
            else
            {
                mem.Ram[address] = value;
            }
        }

        public byte Read(ushort address) => mem.IsIoAddress(address) ? mem.ReadIo(address) : mem.Ram[address];
        public byte Peek(ushort address) => Read(address);
        public void RegisterDevice(IAddressSpace device) { }
        public void UnregisterDevice(IAddressSpace device) { }
    }

    private sealed class FakeClock : IClock
    {
        public long TotalCycles { get; private set; }
        public long FrequencyHz => 985248;
        public void Step() => TotalCycles++;
        public void Step(long cycles) => TotalCycles += cycles;
        public void Register(IClockedDevice device) { }
        public void Unregister(IClockedDevice device) { }
        public void Reset() => TotalCycles = 0;
    }
}
