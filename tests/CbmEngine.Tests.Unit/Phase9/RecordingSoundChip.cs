using CbmEngine.Abstractions;

namespace CbmEngine.Tests.Unit.Phase9;

internal sealed class RecordingSoundChip : ISoundChipStrategy
{
    public SidModel Model { get; }
    public long ClockHz { get; }

    public readonly List<(int Voice, double Hz)> Freqs = new();
    public readonly List<(int Voice, int Width)> Pulses = new();
    public readonly List<(int Voice, Waveform Wave, bool Gate, bool Ring, bool Sync, bool Test)> Waveforms = new();
    public readonly List<(int Voice, byte A, byte D, byte S, byte R)> Adsrs = new();
    public readonly List<(int Cutoff, byte Res, byte Routing, bool Lp, bool Bp, bool Hp, bool V3Off)> Filters = new();
    public readonly List<byte> Volumes = new();
    public int SilenceAllCalls;

    public RecordingSoundChip(SidModel model = SidModel.Mos6581, long clockHz = 985_248L)
    {
        Model = model;
        ClockHz = clockHz;
    }

    public void SetVoiceFrequency(int voice, double hz) => Freqs.Add((voice, hz));
    public void SetVoicePulseWidth(int voice, int width) => Pulses.Add((voice, width));
    public void SetVoiceWaveform(int voice, Waveform waveform, bool gate, bool ringMod = false, bool sync = false, bool test = false)
        => Waveforms.Add((voice, waveform, gate, ringMod, sync, test));
    public void SetVoiceAdsr(int voice, byte attack, byte decay, byte sustain, byte release)
        => Adsrs.Add((voice, attack, decay, sustain, release));
    public void SetFilter(int cutoff, byte resonance, byte voiceRouting, bool lowPass, bool bandPass, bool highPass, bool voice3Off)
        => Filters.Add((cutoff, resonance, voiceRouting, lowPass, bandPass, highPass, voice3Off));
    public void SetVolume(byte volume) => Volumes.Add(volume);
    public void SilenceAll() => SilenceAllCalls++;
    public ushort HzToRegister(double hz) => (ushort)Math.Round(hz * (1L << 24) / ClockHz);
    public double RegisterToHz(ushort registerValue) => (double)registerValue * ClockHz / (1L << 24);
}
