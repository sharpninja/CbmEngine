namespace CbmEngine.Pipeline.Midi;

public sealed record SmfFile(
    int Format,
    int TrackCount,
    int TicksPerQuarter,
    IReadOnlyList<IReadOnlyList<MidiEvent>> Tracks);
