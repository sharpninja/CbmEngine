using CbmEngine.Abstractions;

namespace CbmEngine.Systems.Midi;

/// <summary>
/// One SID voice patch: waveform + ADSR envelope (4-bit per nibble) + optional pulse width
/// (12-bit) + ring/sync modulation flags. Maps onto the SID's per-voice $D4xx + $D4xx+5/+6 control
/// triple. ADSR fields are 0-15; the bridge scales sustain by NoteOn velocity at apply time.
/// </summary>
public readonly record struct SidPatch(
    Waveform Waveform,
    byte Attack,
    byte Decay,
    byte Sustain,
    byte Release,
    ushort PulseWidth = 0x0800,
    bool RingMod = false,
    bool Sync = false)
{
    // Lead voices (melody). Quick attack so notes punch in cleanly, moderate sustain + release
    // so they sing through the next note's start.

    /// <summary>Bright lead pulse with a fixed 50% duty so it sounds like the classic C64 lead.</summary>
    public static readonly SidPatch LeadPulse = new(
        Waveform.Pulse,
        Attack: 0, Decay: 5, Sustain: 13, Release: 6,
        PulseWidth: 0x0800);

    /// <summary>Bright sawtooth lead — slightly nasal, good for melodic lines.</summary>
    public static readonly SidPatch LeadSawtooth = new(
        Waveform.Sawtooth,
        Attack: 0, Decay: 6, Sustain: 12, Release: 6);

    /// <summary>Mellow triangle lead — clean, used by the bridge as the default channel patch.</summary>
    public static readonly SidPatch LeadTriangle = new(
        Waveform.Triangle,
        Attack: 0, Decay: 6, Sustain: 14, Release: 7);

    // Bass voices. Short envelope (Decay drops quickly, Sustain near zero) gives a plucked feel.

    /// <summary>Plucked sawtooth bass — fast attack, quick decay, very low sustain.</summary>
    public static readonly SidPatch BassPluck = new(
        Waveform.Sawtooth,
        Attack: 0, Decay: 8, Sustain: 1, Release: 4);

    /// <summary>Plucked triangle bass — warmer than sawtooth, useful for slower passages.</summary>
    public static readonly SidPatch BassWarm = new(
        Waveform.Triangle,
        Attack: 0, Decay: 7, Sustain: 2, Release: 5);

    // Harmony / pad. Slower attack, high sustain — fills the chord under the melody.

    /// <summary>Triangle pad — soft fade in, long sustain. Good for inner-voice harmony.</summary>
    public static readonly SidPatch Pad = new(
        Waveform.Triangle,
        Attack: 6, Decay: 8, Sustain: 13, Release: 9);

    // Percussion via noise.

    /// <summary>Snare-ish noise hit.</summary>
    public static readonly SidPatch NoiseHit = new(
        Waveform.Noise,
        Attack: 0, Decay: 6, Sustain: 0, Release: 3);

    // Backwards-compatible aliases used by SidPatchLibrary's GM mapping. Slightly tweaked
    // ADSR vs the original placeholder values to sound less anemic.
    public static readonly SidPatch DefaultTriangle = LeadTriangle;
    public static readonly SidPatch DefaultSawtooth = LeadSawtooth;
    public static readonly SidPatch DefaultPulse = LeadPulse;
    public static readonly SidPatch DefaultNoise = NoiseHit;
}
