using CbmEngine.Host.MonoGame;
using CbmEngine.Tests.Shared.Helpers;
using Xunit;

namespace CbmEngine.Tests.Unit;

[Trait("Speed", "Fast")]
public class HostLoopTests
{
    [Fact]
    public void TEST_CBM_HOST_001_PalRefresh_PresentsAtLeast240FramesIn5Seconds()
    {
        var (machine, _, _, _) = FakeMachineBuilder.Build();
        var blit = new FakeBlitTarget();
        var input = new FakeInputScript();
        var clock = new FakeClock();
        var host = new HeadlessHost(machine.Object, blit, input, clock, refreshHz: 50.125);

        host.Run(TimeSpan.FromSeconds(5));

        Assert.True(blit.UploadCount >= 240, $"Expected >=240 uploads after 5s @ 50Hz; got {blit.UploadCount}.");
        Assert.True(host.FrameCount >= 240);
    }

    [Fact]
    public void HeadlessHost_RunFrames_AdvancesFrameCountExactly()
    {
        var (machine, _, _, _) = FakeMachineBuilder.Build();
        var host = new HeadlessHost(machine.Object, new FakeBlitTarget(), new FakeInputScript(), new FakeClock(), 50.0);

        host.RunFrames(17);

        Assert.Equal(17, host.FrameCount);
    }
}
