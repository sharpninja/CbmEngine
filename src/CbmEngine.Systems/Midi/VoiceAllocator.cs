namespace CbmEngine.Systems.Midi;

public sealed class VoiceAllocator
{
    public const int VoiceCount = 3;

    private struct VoiceState
    {
        public bool InUse;
        public int Channel;
        public byte Note;
        public long StartFrame;
        public long ReleaseFrame;   // -1 while gate is on
    }

    private readonly VoiceState[] _voices = new VoiceState[VoiceCount];

    public int ActiveCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < VoiceCount; i++) if (_voices[i].InUse) n++;
            return n;
        }
    }

    public byte GetNote(int voice) => _voices[voice].Note;
    public bool IsActive(int voice) => _voices[voice].InUse;
    public int VoiceCountTotal => VoiceCount;

    public int NoteOn(int channel, byte midiNote, long startFrame)
    {
        // 1. Free slot wins.
        for (int i = 0; i < VoiceCount; i++)
            if (!_voices[i].InUse) return Activate(i, channel, midiNote, startFrame);

        // 2. Released slot (gate off) with oldest release wins.
        int releasedIdx = -1;
        long oldestRelease = long.MaxValue;
        for (int i = 0; i < VoiceCount; i++)
        {
            ref var v = ref _voices[i];
            if (v.ReleaseFrame >= 0 && v.ReleaseFrame < oldestRelease)
            {
                oldestRelease = v.ReleaseFrame;
                releasedIdx = i;
            }
        }
        if (releasedIdx >= 0) return Activate(releasedIdx, channel, midiNote, startFrame);

        // 3. Steal the longest-sustained voice.
        int oldestIdx = 0;
        long oldestStart = _voices[0].StartFrame;
        for (int i = 1; i < VoiceCount; i++)
            if (_voices[i].StartFrame < oldestStart) { oldestStart = _voices[i].StartFrame; oldestIdx = i; }
        return Activate(oldestIdx, channel, midiNote, startFrame);
    }

    /// <summary>
    /// Releases the voice matching (channel, midiNote) and returns its index. Returns -1 when no
    /// active voice matches (NoteOff arrived for a note already released or never gated).
    /// </summary>
    public int NoteOff(int channel, byte midiNote, long releaseFrame)
    {
        for (int i = 0; i < VoiceCount; i++)
        {
            ref var v = ref _voices[i];
            if (v.InUse && v.Channel == channel && v.Note == midiNote && v.ReleaseFrame < 0)
            {
                v.ReleaseFrame = releaseFrame;
                v.InUse = false;
                return i;
            }
        }
        return -1;
    }

    public void Reset()
    {
        for (int i = 0; i < VoiceCount; i++) _voices[i] = default;
    }

    private int Activate(int slot, int channel, byte midiNote, long startFrame)
    {
        ref var v = ref _voices[slot];
        v.InUse = true;
        v.Channel = channel;
        v.Note = midiNote;
        v.StartFrame = startFrame;
        v.ReleaseFrame = -1;
        return slot;
    }
}
