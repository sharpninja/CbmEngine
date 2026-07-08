using CbmEngine.Host.MonoGame;
using CbmEngine.Tests.Shared.Helpers;
using NSubstitute;
using ViceSharp.Abstractions;
using Xunit;

namespace CbmEngine.Tests.Unit;

[Trait("Speed", "Fast")]
public class HeadlessHostTests
{
    [Fact]
    public void TEST_CBM_HOST_004_RunsFullPipeline_WithoutGraphicsDevice()
    {
        var (machine, _, keyboard, _) = FakeMachineBuilder.Build();
        var blit = new FakeBlitTarget();
        var input = new FakeInputScript()
            .Press(0, 0x0A)   // A
            .Release(2, 0x0A);
        var clock = new FakeClock();
        var host = new HeadlessHost(machine, blit, input, clock, 50.0);

        host.RunFrames(5);

        machine.Received(5).RunFrame();
        Assert.Equal(5, blit.UploadCount);
        Assert.Equal(5, input.DrainCallCount);
        keyboard.Received(1).SetKey(0x0A, true);
        keyboard.Received(1).SetKey(0x0A, false);
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
        var registry = Substitute.For<IDeviceRegistry>();
        registry.GetByRole(DeviceRole.VideoChip).Returns((IDevice?)null);
        var machine = Substitute.For<IMachine>();
        machine.Devices.Returns(registry);

        Assert.Throws<InvalidOperationException>(() =>
            new HeadlessHost(machine, new FakeBlitTarget(), new FakeInputScript(), new FakeClock(), 50.0));
    }

    [Fact]
    public void HeadlessHost_RunFramesNegative_Throws()
    {
        var (machine, _, _, _) = FakeMachineBuilder.Build();
        var host = new HeadlessHost(machine, new FakeBlitTarget(), new FakeInputScript(), new FakeClock(), 50.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => host.RunFrames(-1));
    }
}
