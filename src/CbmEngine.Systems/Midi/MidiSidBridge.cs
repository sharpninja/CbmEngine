using CbmEngine.Abstractions;
using CbmEngine.Pipeline.Midi;

namespace CbmEngine.Systems.Midi;

public sealed class MidiSidBridge : IDisposable
{
    private readonly ISoundChipStrategy _strategy;
    private readonly double _refreshHz;
    private readonly VoiceAllocator _allocator = new();
    private readonly SidPatch[] _channelPatches = new SidPatch[16];
    private readonly byte[] _channelVolume = new byte[16];
    private readonly short[] _channelPitchBend = new short[16];

    private MidiEvent[] _scheduledEvents = Array.Empty<MidiEvent>();
    private double[] _scheduledFrames = Array.Empty<double>();
    private int _scheduledCount;
    private int _nextEventIndex;
    private bool _isPlaying;
    private long _frameCounter;
    private long _currentTick;

    public bool IsPlaying => _isPlaying;
    public bool IsFinished => _scheduledCount == 0 || _nextEventIndex >= _scheduledCount;
    public long CurrentTick => _currentTick;
    public int VoicesActive => _allocator.ActiveCount;

    public MidiSidBridge(ICommodoreMachine machine, double refreshHz = 50.125)
        : this((machine ?? throw new ArgumentNullException(nameof(machine))).Sound, refreshHz)
    { }

    public MidiSidBridge(ISoundChipStrategy strategy, double refreshHz = 50.125)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _refreshHz = refreshHz;
        for (int i = 0; i < 16; i++) { _channelPatches[i] = SidPatch.DefaultTriangle; _channelVolume[i] = 127; _channelPitchBend[i] = 0; }
    }

    public void Load(Stream midiStream) => Load(SmfReader.Load(midiStream));

    public void Load(SmfFile smf)
    {
        ArgumentNullException.ThrowIfNull(smf);
        var tempoEntries = new List<MidiTempoMap.Entry>();
        var allEvents = new List<(MidiEvent ev, int track)>(512);
        for (int t = 0; t < smf.Tracks.Count; t++)
        {
            int pos = 0;
            foreach (var ev in smf.Tracks[t])
            {
                if (ev is TempoEvent te) tempoEntries.Add(new MidiTempoMap.Entry(te.Tick, te.MicrosecondsPerQuarter));
                allEvents.Add((ev, t));
                pos++;
            }
        }
        var tempoMap = new MidiTempoMap(tempoEntries);
        var ordered = allEvents
            .OrderBy(x => x.ev.Tick)
            .ThenBy(x => x.track)
            .Select(x => x.ev)
            .ToArray();

        _scheduledEvents = ordered;
        _scheduledFrames = new double[ordered.Length];
        for (int i = 0; i < ordered.Length; i++)
            _scheduledFrames[i] = tempoMap.TickToFrame(ordered[i].Tick, smf.TicksPerQuarter, _refreshHz);
        _scheduledCount = ordered.Length;
        _nextEventIndex = 0;
        _frameCounter = 0;
        _currentTick = 0;
    }

    public void Play() { _isPlaying = true; }
    public void Pause() { _isPlaying = false; }
    public void Stop()
    {
        _isPlaying = false;
        _nextEventIndex = 0;
        _frameCounter = 0;
        _currentTick = 0;
        _allocator.Reset();
        _strategy.SilenceAll();
    }

    public void Tick(int frameIndex)
    {
        if (!_isPlaying || _scheduledCount == 0) return;
        long frame = frameIndex;
        _frameCounter = frame;
        while (_nextEventIndex < _scheduledCount && _scheduledFrames[_nextEventIndex] <= frame)
        {
            ApplyEvent(_scheduledEvents[_nextEventIndex], frame);
            _nextEventIndex++;
        }
        if (_nextEventIndex >= _scheduledCount) _isPlaying = false;
    }

    public SidPatch GetPatch(int channel) => _channelPatches[channel & 0x0F];
    public void SetPatch(int channel, SidPatch patch) => _channelPatches[channel & 0x0F] = patch;

    public void NoteOn(int voice, byte midiNote, byte velocity)
    {
        if ((uint)voice >= VoiceAllocator.VoiceCount) throw new ArgumentOutOfRangeException(nameof(voice));
        // Direct game-side API: skip the allocator (caller picked the voice explicitly).
        var patch = _channelPatches[0];
        ApplyVoiceNoteOn(voice, channel: 0, midiNote, velocity, patch);
    }

    public void NoteOff(int voice)
    {
        if ((uint)voice >= VoiceAllocator.VoiceCount) throw new ArgumentOutOfRangeException(nameof(voice));
        var patch = _channelPatches[0];
        _strategy.SetVoiceWaveform(voice, patch.Waveform, gate: false, patch.RingMod, patch.Sync);
    }

    public void Dispose() { /* nothing yet */ }

    private void ApplyEvent(MidiEvent ev, long frame)
    {
        _currentTick = ev.Tick;
        switch (ev)
        {
            case NoteOnEvent on:
            {
                var patch = _channelPatches[on.Channel & 0x0F];
                int voice = _allocator.NoteOn(on.Channel, on.Note, frame);
                ApplyVoiceNoteOn(voice, on.Channel, on.Note, on.Velocity, patch);
                break;
            }
            case NoteOffEvent off:
            {
                var patch = _channelPatches[off.Channel & 0x0F];
                int voice = _allocator.NoteOff(off.Channel, off.Note, frame);
                if (voice >= 0)
                    _strategy.SetVoiceWaveform(voice, patch.Waveform, gate: false, patch.RingMod, patch.Sync);
                break;
            }
            case ProgramChangeEvent pc:
                _channelPatches[pc.Channel & 0x0F] = SidPatchLibrary.ForProgram(pc.Program);
                break;
            case ControlChangeEvent cc when cc.Controller == 7:
                _channelVolume[cc.Channel & 0x0F] = cc.Value;
                break;
            case PitchBendEvent pb:
                _channelPitchBend[pb.Channel & 0x0F] = pb.Value;
                break;
        }
    }

    private void ApplyVoiceNoteOn(int voice, int channel, byte midiNote, byte velocity, SidPatch patch)
    {
        double hz = MidiNoteToHz(midiNote, _channelPitchBend[channel & 0x0F]);
        _strategy.SetVoiceFrequency(voice, hz);
        if ((patch.Waveform & Abstractions.Waveform.Pulse) != 0)
            _strategy.SetVoicePulseWidth(voice, patch.PulseWidth);
        byte sustain = ScaleSustain(patch.Sustain, velocity, _channelVolume[channel & 0x0F]);
        _strategy.SetVoiceAdsr(voice, patch.Attack, patch.Decay, sustain, patch.Release);
        _strategy.SetVoiceWaveform(voice, patch.Waveform, gate: true, patch.RingMod, patch.Sync);
    }

    private static double MidiNoteToHz(byte midiNote, short pitchBend)
    {
        double effective = midiNote + (pitchBend / 8192.0) * 2.0;   // +/- 2 semitones
        return 440.0 * Math.Pow(2.0, (effective - 69.0) / 12.0);
    }

    private static byte ScaleSustain(byte patchSustain, byte velocity, byte channelVol)
    {
        int scaled = patchSustain * velocity * channelVol / (127 * 127);
        if (scaled > 15) scaled = 15;
        return (byte)scaled;
    }
}
