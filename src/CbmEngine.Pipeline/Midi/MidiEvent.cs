namespace CbmEngine.Pipeline.Midi;

public abstract record MidiEvent(long Tick);

public sealed record NoteOnEvent(long Tick, int Channel, byte Note, byte Velocity) : MidiEvent(Tick);
public sealed record NoteOffEvent(long Tick, int Channel, byte Note) : MidiEvent(Tick);
public sealed record TempoEvent(long Tick, int MicrosecondsPerQuarter) : MidiEvent(Tick);
public sealed record ProgramChangeEvent(long Tick, int Channel, byte Program) : MidiEvent(Tick);
public sealed record ControlChangeEvent(long Tick, int Channel, byte Controller, byte Value) : MidiEvent(Tick);
public sealed record PitchBendEvent(long Tick, int Channel, short Value) : MidiEvent(Tick);
public sealed record EndOfTrackEvent(long Tick) : MidiEvent(Tick);
