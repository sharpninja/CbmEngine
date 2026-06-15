using CbmEngine.Abstractions;

namespace CbmEngine.Systems.Midi;

public static class SidPatchLibrary
{
    /// <summary>
    /// GM-ish patch defaults. Triangle for melodic content unless the program
    /// hints at brass/bass/percussion.
    /// </summary>
    public static SidPatch ForProgram(byte program)
    {
        if (program <= 7) return SidPatch.DefaultTriangle;              // pianos
        if (program is >= 32 and <= 39) return SidPatch.DefaultSawtooth; // basses
        if (program is >= 40 and <= 55) return SidPatch.DefaultTriangle; // strings
        if (program is >= 56 and <= 63) return SidPatch.DefaultPulse;    // brass
        if (program is >= 80 and <= 87) return SidPatch.DefaultSawtooth; // lead synth
        if (program is >= 88 and <= 95) return SidPatch.DefaultPulse;    // pad synth
        if (program is >= 112 and <= 119) return SidPatch.DefaultNoise;  // percussion
        return SidPatch.DefaultTriangle;
    }

    public static SidPatch DrumPatch => SidPatch.DefaultNoise;
}
