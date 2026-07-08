using NSubstitute;
using ViceSharp.Abstractions;

namespace CbmEngine.Tests.Shared.Helpers;

public static class FakeMachineBuilder
{
    public const int FbWidth = 384;
    public const int FbHeight = 272;

    public static (IMachine Machine, IVideoChip Video, IKeyboardMatrix Keyboard, byte[] FrameBuffer) Build(bool withKeyboard = true)
    {
        var fb = new byte[FbWidth * FbHeight * 4];
        var video = Substitute.For<IVideoChip>();
        video.FrameBuffer.Returns(fb);
        video.FrameWidth.Returns(FbWidth);
        video.FrameHeight.Returns(FbHeight);

        var keyboard = Substitute.For<IKeyboardMatrix>();
        var keyboards = withKeyboard
            ? (IReadOnlyList<IKeyboardMatrix>)new[] { keyboard }
            : Array.Empty<IKeyboardMatrix>();

        var registry = Substitute.For<IDeviceRegistry>();
        registry.GetByRole(DeviceRole.VideoChip).Returns(video);
        registry.GetAll<IKeyboardMatrix>().Returns(keyboards);
        registry.GetAll<IAudioChip>().Returns(Array.Empty<IAudioChip>());

        var machine = Substitute.For<IMachine>();
        machine.Devices.Returns(registry);

        return (machine, video, keyboard, fb);
    }
}
