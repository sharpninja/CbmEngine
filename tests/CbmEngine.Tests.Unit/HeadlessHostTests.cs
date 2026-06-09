using CbmEngine.Host.MonoGame;
using CbmEngine.Tests.Shared.Helpers;
using Moq;
using ViceSharp.Abstractions;
using Xunit;

namespace CbmEngine.Tests.Unit;

[Trait("Speed", "Fast")]
public class HeadlessHostTests
{
    [Fact]
    public void TEST_CBM_HOST_004_RunsFullPipeline_WithoutGraphicsDevice()
    {
        var (machine, video, keyboard, _) = FakeMachineBuilder.Build();
        var blit = new FakeBlitTarget();
        var input = new FakeInputScript()
            .Press(0, 0x0A)   // A
            .Release(2, 0x0A);
        var clock = new FakeClock();
        var host = new HeadlessHost(machine.Object, blit, input, clock, 50.0);

        host.RunFrames(5);

        machine.Verify(m => m.RunFrame(), Times.Exactly(5));
        Assert.Equal(5, blit.UploadCount);
        Assert.Equal(5, input.DrainCallCount);
        keyboard.Verify(k => k.SetKey(0x0A, true), Times.Once);
        keyboard.Verify(k => k.SetKey(0x0A, false), Times.Once);
    }

    [Fact]
    public void HeadlessHost_NullMachine_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new HeadlessHost(null!, new FakeBlitTarget(), new FakeInputScript(), new FakeClock(), 50.0));
    }

    [Fact]
    public void HeadlessHost_MachineWithoutVideoChip_Throws()
    {
        var registry = new Mock<IDeviceRegistry>();
        registry.Setup(r => r.GetByRole(DeviceRole.VideoChip)).Returns((IDevice?)null);
        var machine = new Mock<IMachine>();
        machine.SetupGet(m => m.Devices).Returns(registry.Object);

        Assert.Throws<InvalidOperationException>(() =>
            new HeadlessHost(machine.Object, new FakeBlitTarget(), new FakeInputScript(), new FakeClock(), 50.0));
    }

    [Fact]
    public void HeadlessHost_RunFramesNegative_Throws()
    {
        var (machine, _, _, _) = FakeMachineBuilder.Build();
        var host = new HeadlessHost(machine.Object, new FakeBlitTarget(), new FakeInputScript(), new FakeClock(), 50.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => host.RunFrames(-1));
    }
}
