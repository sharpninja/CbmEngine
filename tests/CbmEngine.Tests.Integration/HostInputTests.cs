using CbmEngine.Abstractions;
using CbmEngine.Host.MonoGame;
using CbmEngine.Systems.Boot;
using ViceSharp.Architectures.C64;
using ViceSharp.RomFetch;
using Xunit;

namespace CbmEngine.Tests.Integration;

[Trait("Speed", "Slow")]
public class HostInputTests
{
    private const byte MatrixA = 0x0A;

    private sealed class TwoFrameAInput : IInputScript
    {
        public IReadOnlyList<InputEvent> DrainForFrame(int frameIndex) => frameIndex switch
        {
            0 => new[] { new InputEvent(MatrixA, true) },
            1 => new[] { new InputEvent(MatrixA, false) },
            _ => Array.Empty<InputEvent>()
        };
    }

    [Fact]
    public void TEST_CBM_HOST_003_KeyPress_AppearsInKeyboardBufferWithinTenFrames()
    {
        var roms = Helpers.TestRomProvider.Create();
        var result = BootRunner.Run(C64MachineProfiles.C64Pal, roms, framesToWarm: 180);
        var machine = result.Machine;
        var bus = machine.Bus;

        var fakeClock = new global::CbmEngine.Tests.Shared.Helpers.FakeClock();
        var fakeBlit = new global::CbmEngine.Tests.Shared.Helpers.FakeBlitTarget();
        var host = new HeadlessHost(machine, fakeBlit, new TwoFrameAInput(), fakeClock, 50.125);

        bool found = false;
        for (int i = 0; i < 10 && !found; i++)
        {
            host.RunFrames(1);
            if (bus.Read(0x0277) == 0x41 || bus.Read(0x00C5) == 0x0A)
                found = true;
        }

        Assert.True(found, $"Expected keypress to reach KEYD[$0277] or LSTX[$00C5] within 10 frames; KEYD={bus.Read(0x0277):X2} LSTX={bus.Read(0x00C5):X2}.");
    }
}
