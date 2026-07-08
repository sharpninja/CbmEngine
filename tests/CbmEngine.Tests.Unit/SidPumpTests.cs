using CbmEngine.Host.MonoGame;
using CbmEngine.Tests.Shared.Helpers;
using NSubstitute;
using ViceSharp.Abstractions;
using Xunit;

namespace CbmEngine.Tests.Unit;

[Trait("Speed", "Fast")]
public class SidPumpTests
{
    [Fact]
    public void PumpFrame_SubmitsExpectedSampleCount()
    {
        var sid = Substitute.For<IAudioChip>();
        float i = 0;
        sid.GenerateSample().Returns(_ => i++ * 0.001f);
        var backend = new RecordingAudioBackend();

        var pump = new SidPump(sid, backend, sampleRate: 44100, refreshHz: 50.0);
        pump.PumpFrame();

        Assert.Equal(882, pump.SamplesPerFrame);
        Assert.Equal(1, backend.SubmitCallCount);
        Assert.Equal(882, backend.Samples.Count);
    }

    [Fact]
    public void Pump_RejectsBadConstructorArgs()
    {
        var sid = Substitute.For<IAudioChip>();
        var backend = new RecordingAudioBackend();

        Assert.Throws<ArgumentNullException>(() => new SidPump(null!, backend, 44100, 50.0));
        Assert.Throws<ArgumentNullException>(() => new SidPump(sid, null!, 44100, 50.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SidPump(sid, backend, 0, 50.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SidPump(sid, backend, 44100, 0));
    }
}
