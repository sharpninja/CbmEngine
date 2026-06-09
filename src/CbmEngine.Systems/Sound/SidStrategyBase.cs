using CbmEngine.Abstractions;

namespace CbmEngine.Systems.Sound;

public abstract class SidStrategyBase : ISoundChipStrategy
{
    private const ushort SidBase = 0xD400;
    private readonly IMemoryService _memory;
    private static readonly int[] VoiceBase = { 0x00, 0x07, 0x0E };

    protected SidStrategyBase(IMemoryService memory, long clockHz)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        if (clockHz <= 0) throw new ArgumentOutOfRangeException(nameof(clockHz));
        ClockHz = clockHz;
    }

    public abstract SidModel Model { get; }
    public long ClockHz { get; }

    public ushort HzToRegister(double hz)
    {
        if (hz < 0) hz = 0;
        double raw = hz * (1L << 24) / ClockHz;
        return (ushort)Math.Round(Math.Min(raw, 0xFFFF));
    }

    public double RegisterToHz(ushort registerValue) => registerValue * (double)ClockHz / (1L << 24);

    public void SetVoiceFrequency(int voice, double hz)
    {
        EnsureVoice(voice);
        ushort r = HzToRegister(hz);
        ushort baseAddr = (ushort)(SidBase + VoiceBase[voice]);
        Span<byte> buf = stackalloc byte[2];
        buf[0] = (byte)(r & 0xFF);
        buf[1] = (byte)((r >> 8) & 0xFF);
        _memory.WriteIo(baseAddr, buf);
    }

    public void SetVoicePulseWidth(int voice, int width)
    {
        EnsureVoice(voice);
        if (width < 0 || width > 0x0FFF) throw new ArgumentOutOfRangeException(nameof(width));
        ushort baseAddr = (ushort)(SidBase + VoiceBase[voice] + 2);
        Span<byte> buf = stackalloc byte[2];
        buf[0] = (byte)(width & 0xFF);
        buf[1] = (byte)((width >> 8) & 0x0F);
        _memory.WriteIo(baseAddr, buf);
    }

    public void SetVoiceWaveform(int voice, Waveform waveform, bool gate, bool ringMod = false, bool sync = false, bool test = false)
    {
        EnsureVoice(voice);
        byte ctrl = 0;
        if (gate) ctrl |= 0x01;
        if (sync) ctrl |= 0x02;
        if (ringMod) ctrl |= 0x04;
        if (test) ctrl |= 0x08;
        ctrl |= (byte)(((byte)waveform & 0x0F) << 4);
        ushort addr = (ushort)(SidBase + VoiceBase[voice] + 4);
        Span<byte> buf = stackalloc byte[1] { ctrl };
        _memory.WriteIo(addr, buf);
    }

    public void SetVoiceAdsr(int voice, byte attack, byte decay, byte sustain, byte release)
    {
        EnsureVoice(voice);
        if ((attack | decay | sustain | release) > 0x0F) throw new ArgumentOutOfRangeException("ADSR nibbles must be 0..15.");
        ushort addr = (ushort)(SidBase + VoiceBase[voice] + 5);
        Span<byte> buf = stackalloc byte[2];
        buf[0] = (byte)((attack << 4) | decay);
        buf[1] = (byte)((sustain << 4) | release);
        _memory.WriteIo(addr, buf);
    }

    public void SetFilter(int cutoff, byte resonance, byte voiceRouting, bool lowPass, bool bandPass, bool highPass, bool voice3Off)
    {
        if (cutoff < 0 || cutoff > 0x7FF) throw new ArgumentOutOfRangeException(nameof(cutoff));
        if (resonance > 0x0F) throw new ArgumentOutOfRangeException(nameof(resonance));
        if (voiceRouting > 0x0F) throw new ArgumentOutOfRangeException(nameof(voiceRouting));

        Span<byte> cutoffBytes = stackalloc byte[2];
        cutoffBytes[0] = (byte)(cutoff & 0x07);
        cutoffBytes[1] = (byte)((cutoff >> 3) & 0xFF);
        _memory.WriteIo(0xD415, cutoffBytes);

        byte resReg = (byte)((resonance << 4) | (voiceRouting & 0x0F));
        _memory.WriteIo(0xD417, new[] { resReg });

        byte vol = _memory.ReadIo(0xD418);
        byte modeBits = 0;
        if (lowPass) modeBits |= 0x10;
        if (bandPass) modeBits |= 0x20;
        if (highPass) modeBits |= 0x40;
        if (voice3Off) modeBits |= 0x80;
        byte newVol = (byte)((vol & 0x0F) | modeBits);
        _memory.WriteIo(0xD418, new[] { newVol });
    }

    public void SetVolume(byte volume)
    {
        if (volume > 0x0F) throw new ArgumentOutOfRangeException(nameof(volume));
        byte current = _memory.ReadIo(0xD418);
        byte updated = (byte)((current & 0xF0) | (volume & 0x0F));
        _memory.WriteIo(0xD418, new[] { updated });
    }

    public void SilenceAll()
    {
        for (int v = 0; v < 3; v++)
        {
            ushort ctrlAddr = (ushort)(SidBase + VoiceBase[v] + 4);
            _memory.WriteIo(ctrlAddr, new byte[] { 0 });
        }
        SetVolume(0);
    }

    private static void EnsureVoice(int voice)
    {
        if (voice < 0 || voice > 2) throw new ArgumentOutOfRangeException(nameof(voice), "Voice must be 0, 1, or 2.");
    }
}
