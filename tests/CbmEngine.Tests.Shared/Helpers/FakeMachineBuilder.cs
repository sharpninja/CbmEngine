using Moq;
using ViceSharp.Abstractions;

namespace CbmEngine.Tests.Shared.Helpers;

public static class FakeMachineBuilder
{
    public const int FbWidth = 384;
    public const int FbHeight = 272;

    public static (Mock<IMachine> Machine, Mock<IVideoChip> Video, Mock<IKeyboardMatrix> Keyboard, byte[] FrameBuffer) Build(bool withKeyboard = true)
    {
        var fb = new byte[FbWidth * FbHeight * 4];
        var video = new Mock<IVideoChip>();
        video.SetupGet(v => v.FrameBuffer).Returns(fb);
        video.SetupGet(v => v.FrameWidth).Returns(FbWidth);
        video.SetupGet(v => v.FrameHeight).Returns(FbHeight);

        var keyboard = new Mock<IKeyboardMatrix>();
        var keyboards = withKeyboard
            ? (IReadOnlyList<IKeyboardMatrix>)new[] { keyboard.Object }
            : Array.Empty<IKeyboardMatrix>();

        var registry = new Mock<IDeviceRegistry>();
        registry.Setup(r => r.GetByRole(DeviceRole.VideoChip)).Returns(video.Object);
        registry.Setup(r => r.GetAll<IKeyboardMatrix>()).Returns(keyboards);
        registry.Setup(r => r.GetAll<IAudioChip>()).Returns(Array.Empty<IAudioChip>());

        var machine = new Mock<IMachine>();
        machine.SetupGet(m => m.Devices).Returns(registry.Object);

        return (machine, video, keyboard, fb);
    }
}
