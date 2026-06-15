using System.Diagnostics;
using CbmEngine.Abstractions;
using CbmEngine.Host.MonoGame;
using CbmEngine.Tests.Shared.Helpers;
using Microsoft.Xna.Framework.Graphics;
using Moq;
using ViceSharp.Abstractions;
using Xunit;

namespace CbmEngine.Tests.Unit;

/// <summary>
/// Headless coverage for the reusable, Game-agnostic <see cref="CbmViewport"/> (UP-CBM-001).
/// Exercises the internal dependency-injection seam so the emulator composition can be
/// driven without a real MonoGame <c>GraphicsDevice</c>.
/// </summary>
[Trait("Speed", "Fast")]
public class CbmViewportTests
{
    private static CbmViewport NewNonHybrid(
        IMachine machine,
        IBlitTarget blit,
        IInputScript input,
        IAudioBackend? audio = null,
        double refreshHz = 50.0,
        int sampleRate = 44100)
        => new(machine, blit, ownsBlit: false, input, audio, refreshHz, sampleRate,
               game: null, gameContext: null, useHybridPump: false);

    // TEST-CBM-HOST-005
    [Fact]
    public void TEST_CBM_HOST_005_NonHybridTick_DrivesOneFramePerTick()
    {
        var (machine, _, keyboard, _) = FakeMachineBuilder.Build();
        var blit = new FakeBlitTarget();
        var input = new FakeInputScript()
            .Press(0, 0x0A)
            .Release(2, 0x0A);
        using var viewport = NewNonHybrid(machine.Object, blit, input);

        for (int i = 0; i < 5; i++)
        {
            viewport.Tick();
            viewport.RefreshTexture();
        }

        machine.Verify(m => m.RunFrame(), Times.Exactly(5));
        Assert.Equal(5, blit.UploadCount);
        Assert.Equal(5, input.DrainCallCount);
        Assert.Equal(FakeMachineBuilder.FbWidth, blit.LastWidth);
        Assert.Equal(FakeMachineBuilder.FbHeight, blit.LastHeight);
        keyboard.Verify(k => k.SetKey(0x0A, true), Times.Once);
        keyboard.Verify(k => k.SetKey(0x0A, false), Times.Once);
    }

    // TEST-CBM-HOST-006
    [Fact]
    public void TEST_CBM_HOST_006_HybridPump_ProducesFramesAndUploads()
    {
        var (machine, _, _, _) = FakeMachineBuilder.Build();
        var blit = new FakeBlitTarget();
        using var viewport = new CbmViewport(
            machine.Object, blit, ownsBlit: false, new FakeInputScript(), audioBackend: null,
            refreshHz: 200.0, sampleRate: 44100, game: null, gameContext: null, useHybridPump: true);

        var sw = Stopwatch.StartNew();
        while (viewport.FramesCompleted < 1 && sw.Elapsed < TimeSpan.FromSeconds(2))
            Thread.Sleep(5);

        Assert.True(viewport.FramesCompleted >= 1, $"Pump produced no frames in 2s (got {viewport.FramesCompleted}).");

        viewport.RefreshTexture();

        Assert.True(blit.UploadCount >= 1);
        Assert.Equal(FakeMachineBuilder.FbWidth, blit.LastWidth);
        Assert.Equal(FakeMachineBuilder.FbHeight, blit.LastHeight);
    }

    // TEST-CBM-HOST-007
    [Fact]
    public void TEST_CBM_HOST_007_NullMachine_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            NewNonHybrid(null!, new FakeBlitTarget(), new FakeInputScript()));
    }

    // TEST-CBM-HOST-007
    [Fact]
    public void TEST_CBM_HOST_007_NullGraphicsDevice_Throws()
    {
        var (machine, _, _, _) = FakeMachineBuilder.Build();
        Assert.Throws<ArgumentNullException>(() =>
            new CbmViewport(machine.Object, (GraphicsDevice)null!));
    }

    // TEST-CBM-HOST-007
    [Fact]
    public void TEST_CBM_HOST_007_MachineWithoutVideoChip_Throws()
    {
        var registry = new Mock<IDeviceRegistry>();
        registry.Setup(r => r.GetByRole(DeviceRole.VideoChip)).Returns((IDevice?)null);
        var machine = new Mock<IMachine>();
        machine.SetupGet(m => m.Devices).Returns(registry.Object);

        Assert.Throws<InvalidOperationException>(() =>
            NewNonHybrid(machine.Object, new FakeBlitTarget(), new FakeInputScript()));
    }

    // TEST-CBM-HOST-007
    [Fact]
    public void TEST_CBM_HOST_007_FrameDimensions_MatchVideoChip()
    {
        var (machine, _, _, _) = FakeMachineBuilder.Build();
        using var viewport = NewNonHybrid(machine.Object, new FakeBlitTarget(), new FakeInputScript());

        Assert.Equal(FakeMachineBuilder.FbWidth, viewport.FrameWidth);
        Assert.Equal(FakeMachineBuilder.FbHeight, viewport.FrameHeight);
    }

    // TEST-CBM-HOST-008
    [Fact]
    public void TEST_CBM_HOST_008_AudioPump_SubmitsSamplesEachTick()
    {
        var (machine, registry, _, audioChip) = BuildMachineWithAudio();
        var backend = new RecordingAudioBackend();
        using var viewport = NewNonHybrid(machine.Object, new FakeBlitTarget(), new FakeInputScript(),
            audio: backend, refreshHz: 50.0, sampleRate: 441);

        for (int i = 0; i < 3; i++)
            viewport.Tick();

        Assert.Equal(3, backend.SubmitCallCount);
        Assert.True(backend.Samples.Count > 0);
        audioChip.Verify(a => a.GenerateSample(), Times.AtLeastOnce);
    }

    // TEST-CBM-HOST-008
    [Fact]
    public void TEST_CBM_HOST_008_EnqueueKey_ForwardsToKeyboardMatrix()
    {
        var (machine, _, keyboard, _) = FakeMachineBuilder.Build();
        using var viewport = NewNonHybrid(machine.Object, new FakeBlitTarget(), new FakeInputScript());

        viewport.EnqueueKey(0x0A, true);

        keyboard.Verify(k => k.SetKey(0x0A, true), Times.Once);
    }

    [Fact]
    public void CbmViewport_DoesNotDeriveFromGame()
    {
        Assert.False(typeof(Microsoft.Xna.Framework.Game).IsAssignableFrom(typeof(CbmViewport)),
            "CbmViewport must be Game-agnostic and must not derive from MonoGame Game.");
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(CbmViewport)));
    }

    private static (Mock<IMachine> Machine, Mock<IDeviceRegistry> Registry, Mock<IKeyboardMatrix> Keyboard, Mock<IAudioChip> Audio)
        BuildMachineWithAudio()
    {
        var fb = new byte[FakeMachineBuilder.FbWidth * FakeMachineBuilder.FbHeight * 4];
        var video = new Mock<IVideoChip>();
        video.SetupGet(v => v.FrameBuffer).Returns(fb);
        video.SetupGet(v => v.FrameWidth).Returns(FakeMachineBuilder.FbWidth);
        video.SetupGet(v => v.FrameHeight).Returns(FakeMachineBuilder.FbHeight);

        var keyboard = new Mock<IKeyboardMatrix>();
        var audio = new Mock<IAudioChip>();
        audio.Setup(a => a.GenerateSample()).Returns(0.5f);

        var registry = new Mock<IDeviceRegistry>();
        registry.Setup(r => r.GetByRole(DeviceRole.VideoChip)).Returns(video.Object);
        registry.Setup(r => r.GetByRole(DeviceRole.AudioChip)).Returns(audio.Object);
        registry.Setup(r => r.GetAll<IKeyboardMatrix>())
            .Returns((IReadOnlyList<IKeyboardMatrix>)new[] { keyboard.Object });

        var machine = new Mock<IMachine>();
        machine.SetupGet(m => m.Devices).Returns(registry.Object);

        return (machine, registry, keyboard, audio);
    }
}
