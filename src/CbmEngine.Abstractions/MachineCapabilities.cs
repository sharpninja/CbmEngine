using System.Collections.Immutable;
using ViceSharp.Abstractions;

namespace CbmEngine.Abstractions;

public sealed record MachineCapabilities(
    string ProfileId,
    string DisplayName,
    VideoStandard VideoStandard,
    int CyclesPerLine,
    int RasterLines,
    long NominalClockHz,
    double RefreshRateHz,
    SidModel SidModel,
    ImmutableArray<uint> BgraPalette);

public enum SidModel
{
    Mos6581,
    Mos8580
}

[Flags]
public enum Waveform : byte
{
    None = 0,
    Triangle = 1 << 0,
    Sawtooth = 1 << 1,
    Pulse = 1 << 2,
    Noise = 1 << 3
}
