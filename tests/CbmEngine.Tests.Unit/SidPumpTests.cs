using CbmEngine.Host.MonoGame;
using CbmEngine.Tests.Shared.Helpers;
using Moq;
using ViceSharp.Abstractions;
using Xunit;

namespace CbmEngine.Tests.Unit;

[Trait("Speed", "Fast")]
public class SidPumpTests
{
    [Fact]
    public void PumpFrame_SubmitsExpectedSampleCount()
    {
        var sid = new Mock<IAudioChip>();
        float i = 0;
        sid.Setup(s => s.GenerateSample()).Returns(() => i++ * 0.001f);
        var backend = new RecordingAudioBackend();

        var pump = new SidPump(sid.Object, backend, sampleRate: 44100, refreshHz: 50.0);
        pump.PumpFrame();

        Assert.Equal(882, pump.SamplesPerFrame);
        Assert.Equal(1, backend.SubmitCallCount);
        Assert.Equal(882, backend.Samples.Count);
    }

    [Fact]
    public void Pump_RejectsBadConstructorArgs()
    {
        var sid = new Mock<IAudioChip>().Object;
        var backend = new RecordingAudioBackend();

        Assert.Throws<ArgumentNullException>(() => new SidPump(null!, backend, 44100, 50.0));
        Assert.Throws<ArgumentNullException>(() => new SidPump(sid, null!, 44100, 50.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SidPump(sid, backend, 0, 50.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SidPump(sid, backend, 44100, 0));
    }
}
