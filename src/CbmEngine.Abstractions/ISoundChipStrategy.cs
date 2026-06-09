namespace CbmEngine.Abstractions;

public interface ISoundChipStrategy
{
    SidModel Model { get; }
    long ClockHz { get; }
    void SetVoiceFrequency(int voice, double hz);
    void SetVoicePulseWidth(int voice, int width);
    void SetVoiceWaveform(int voice, Waveform waveform, bool gate, bool ringMod = false, bool sync = false, bool test = false);
    void SetVoiceAdsr(int voice, byte attack, byte decay, byte sustain, byte release);
    void SetFilter(int cutoff, byte resonance, byte voiceRouting, bool lowPass, bool bandPass, bool highPass, bool voice3Off);
    void SetVolume(byte volume);
    void SilenceAll();
    ushort HzToRegister(double hz);
    double RegisterToHz(ushort registerValue);
}
