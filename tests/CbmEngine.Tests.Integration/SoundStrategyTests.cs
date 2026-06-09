using CbmEngine.Abstractions;
using CbmEngine.Host.MonoGame;
using CbmEngine.Systems.Strategy;
using CbmEngine.Tests.Integration.Helpers;
using CbmEngine.Tests.Shared.Helpers;
using Xunit;

namespace CbmEngine.Tests.Integration;

[Trait("Speed", "Slow")]
public class SoundStrategyTests
{
    private static ICommodoreMachine BuildC64()
    {
        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        for (int i = 0; i < 120; i++) sys.RunFrame();
        return sys;
    }

    [Theory]
    [InlineData("c64", SidModel.Mos6581)]
    [InlineData("c64c", SidModel.Mos8580)]
    [InlineData("ntsc", SidModel.Mos6581)]
    [InlineData("newntsc", SidModel.Mos8580)]
    public void TEST_CBM_SOUND_001_ResolvesBySidModel(string id, SidModel expected)
    {
        var sys = CommodoreSystem.Build(id, TestRomProvider.Create());
        Assert.Equal(expected, sys.Sound.Model);
    }

    [Fact]
    public void TEST_CBM_SOUND_002_SetVoiceFrequency_WritesCorrectRegisterPair()
    {
        var sys = BuildC64();
        sys.Sound.SetVoiceFrequency(0, 440.0);

        ushort expected = (ushort)Math.Round(440.0 * (1L << 24) / sys.Sound.ClockHz);
        byte lo = sys.Bus.Read(0xD400);
        byte hi = sys.Bus.Read(0xD401);
        ushort observed = (ushort)(lo | (hi << 8));
        Assert.InRange(Math.Abs(observed - expected), 0, 1);
    }

    [Fact]
    public void TEST_CBM_SOUND_003_SetVoiceWaveform_BitsAreCorrect()
    {
        var sys = BuildC64();
        sys.Sound.SetVoiceWaveform(0, Waveform.Triangle, gate: true);
        byte ctrl = sys.Bus.Read(0xD404);
        Assert.Equal(0x11, ctrl);
    }

    [Fact]
    public void TEST_CBM_SOUND_004_SetVoiceAdsr_PacksNibblesCorrectly()
    {
        var sys = BuildC64();
        sys.Sound.SetVoiceAdsr(0, attack: 2, decay: 9, sustain: 8, release: 4);
        Assert.Equal(0x29, sys.Bus.Read(0xD405));
        Assert.Equal(0x84, sys.Bus.Read(0xD406));
    }

    [Fact]
    public void TEST_CBM_SOUND_005_SetFilter_WritesCutoffResonanceModeBits()
    {
        var sys = BuildC64();
        sys.Sound.SetFilter(cutoff: 0x080, resonance: 0xF, voiceRouting: 0x07, lowPass: true, bandPass: false, highPass: false, voice3Off: false);
        Assert.Equal((byte)(0xF7), sys.Bus.Read(0xD417));
        Assert.True((sys.Bus.Read(0xD418) & 0x10) != 0);
    }

    [Fact]
    public void TEST_CBM_SOUND_006_SilenceAll_DropsAudioBelowNoiseFloor()
    {
        var sys = BuildC64();
        sys.Sound.SetVoiceAdsr(0, 0, 9, 8, 4);
        sys.Sound.SetVoiceFrequency(0, 440.0);
        sys.Sound.SetVolume(0x0F);
        sys.Sound.SetVoiceWaveform(0, Waveform.Triangle, gate: true);

        var backend = new RecordingAudioBackend();
        var pump = new SidPump(sys.AudioChip!, backend, 44100, 50.125);
        for (int i = 0; i < 25; i++) { sys.RunFrame(); pump.PumpFrame(); }

        sys.Sound.SilenceAll();
        backend.Stop();

        for (int i = 0; i < 5; i++) { sys.RunFrame(); pump.PumpFrame(); }

        int audible = 0;
        for (int i = 0; i < backend.Samples.Count; i++)
            if (Math.Abs(backend.Samples[i]) > 0.001f) audible++;
        Assert.True(audible < backend.Samples.Count / 10, $"Post-Silence audible={audible} of {backend.Samples.Count} should be <10%.");
    }

    [Fact]
    public void TEST_CBM_SOUND_007_HzToRegister_6581And8580_RoundTrip()
    {
        var sys6581 = CommodoreSystem.Build("c64", TestRomProvider.Create());
        var sys8580 = CommodoreSystem.Build("c64c", TestRomProvider.Create());

        ushort r6 = sys6581.Sound.HzToRegister(440.0);
        ushort r8 = sys8580.Sound.HzToRegister(440.0);
        Assert.InRange(Math.Abs(r6 - r8), 0, 1);

        Assert.InRange(Math.Abs(sys6581.Sound.RegisterToHz(r6) - 440.0), 0, 0.5);
        Assert.InRange(Math.Abs(sys8580.Sound.RegisterToHz(r8) - 440.0), 0, 0.5);
    }
}
