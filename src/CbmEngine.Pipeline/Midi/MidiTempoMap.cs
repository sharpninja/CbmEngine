namespace CbmEngine.Pipeline.Midi;

public sealed class MidiTempoMap
{
    public readonly record struct Entry(long Tick, int MicrosecondsPerQuarter);

    private readonly Entry[] _entries;

    public MidiTempoMap(IEnumerable<Entry> entries)
    {
        var ordered = entries.OrderBy(e => e.Tick).ToArray();
        if (ordered.Length == 0 || ordered[0].Tick != 0)
        {
            var list = new List<Entry>(ordered.Length + 1) { new(0, 500_000) };
            list.AddRange(ordered);
            ordered = list.ToArray();
        }
        _entries = ordered;
    }

    public int MicrosecondsPerQuarterAt(long tick)
    {
        if (_entries.Length == 1) return _entries[0].MicrosecondsPerQuarter;
        int lo = 0, hi = _entries.Length - 1, best = 0;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (_entries[mid].Tick <= tick) { best = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        return _entries[best].MicrosecondsPerQuarter;
    }

    /// <summary>
    /// Convert a MIDI tick to a fractional frame index for the supplied refresh rate.
    /// </summary>
    public double TickToFrame(long tick, int ticksPerQuarter, double refreshHz)
    {
        double frames = 0;
        long prevTick = 0;
        int prevUspq = _entries[0].MicrosecondsPerQuarter;
        for (int i = 1; i < _entries.Length; i++)
        {
            var e = _entries[i];
            if (e.Tick >= tick) break;
            long delta = e.Tick - prevTick;
            frames += delta * (prevUspq / (double)ticksPerQuarter) * (refreshHz / 1_000_000.0);
            prevTick = e.Tick;
            prevUspq = e.MicrosecondsPerQuarter;
        }
        long tail = tick - prevTick;
        frames += tail * (prevUspq / (double)ticksPerQuarter) * (refreshHz / 1_000_000.0);
        return frames;
    }
}
