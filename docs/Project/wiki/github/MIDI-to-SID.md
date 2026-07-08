# MIDI to SID

CbmEngine can play a Standard MIDI File (`.mid`) through the three SID voices in
real time. It reads the SMF, maps General MIDI program changes to SID patches,
converts notes to SID frequencies, responds to velocity and pitch bend, and
allocates the three voices with a defined stealing policy.

This builds on the SID strategy in [Audio and SID](audio-and-sid.md).

## Quick start

```csharp
using CbmEngine.Pipeline.Midi;   // SmfReader
using CbmEngine.Systems.Midi;    // MidiSidBridge

// Build a machine and let it settle, then turn the SID up.
var sys = CommodoreSystem.Build("c64", roms);
for (int i = 0; i < 120; i++) sys.RunFrame();
sys.Sound.SetVolume(15);

// Load and play a MIDI file.
using var fs = File.OpenRead("fixtures/midi/fur_elise.mid");
SmfFile smf = SmfReader.Load(fs);

using var bridge = new MidiSidBridge(sys);   // pulls sys.Sound; default PAL 50.125 Hz
bridge.Load(smf);
bridge.Play();
```

Then pump it once per frame with a frame index. **Use a wall-clock-derived index,
not the raw pump frame counter**, so tempo stays correct even when the emulator
does not run at exactly the refresh rate:

```csharp
public sealed class MidiGame : IGame
{
    private readonly MidiSidBridge _bridge;
    private readonly double _refreshHz;
    private readonly Stopwatch _clock = new();

    public MidiGame(MidiSidBridge bridge, double refreshHz = 50.125)
    {
        _bridge = bridge;
        _refreshHz = refreshHz;
    }

    public void Initialize(IGameContext context) => _clock.Restart();

    public void Update(IGameContext context, int frameIndex)
    {
        int virtualFrame = (int)(_clock.Elapsed.TotalSeconds * _refreshHz);
        _bridge.Tick(virtualFrame);
    }

    public void Draw(IGameContext context, int frameIndex) { }
}
```

This is exactly the sample's `MidiGame`. Hand it to a `MonoGameHost` like any
other game.

## The bridge API

```csharp
public MidiSidBridge(ICommodoreMachine machine, double refreshHz = 50.125);
public MidiSidBridge(ISoundChipStrategy strategy, double refreshHz = 50.125);
```

`refreshHz` is the frame rate your `Tick` index counts in. The default `50.125`
is the PAL VIC-II frame rate; use about `59.826` for NTSC.

| Member | Effect |
|--------|--------|
| `Load(Stream)` / `Load(SmfFile)` | Parse and schedule a MIDI file. Flattens all tracks, builds a tempo map, and precomputes each event's fractional frame index. |
| `Play()` / `Pause()` / `Stop()` | Start, pause (keep position), or stop (rewind, silence all voices). |
| `Tick(frameIndex)` | Apply every event scheduled at or before `frameIndex`. Call once per rendered frame with an increasing index. |
| `GetPatch(channel)` / `SetPatch(channel, patch)` | Read or override the SID patch for a MIDI channel (0-15). |
| `NoteOn(voice, midiNote, velocity)` / `NoteOff(voice)` | Direct game-side control that bypasses the allocator: you pick the SID voice (0-2). Uses channel 0's patch. |
| `IsPlaying`, `IsFinished`, `CurrentTick`, `VoicesActive` | State. |

## How MIDI maps to SID

### Notes to frequency

A MIDI note becomes a frequency by equal temperament, with pitch bend applied as
a fixed plus/minus 2 semitones over the 14-bit bend range:

```
effectiveNote = midiNote + (pitchBend / 8192) * 2
hz            = 440 * 2 ^ ((effectiveNote - 69) / 12)
```

MIDI note 69 is A4 = 440 Hz. The resulting Hz goes through
`ISoundChipStrategy.SetVoiceFrequency`.

> Pitch bend affects **subsequent** note-ons on that channel; it does not retune
> a note that is already sounding.

### Velocity and channel volume to loudness

The SID has no per-voice volume register, so loudness is expressed through the
envelope sustain level. The patch's sustain nibble is scaled by note velocity and
by the channel's CC7 volume:

```
sustain = patchSustain * velocity * channelVolume / (127 * 127)   // clamped to 0..15
```

Attack, decay, and release come from the patch unscaled. On a note-on the bridge
sets frequency, pulse width (if the patch uses the Pulse waveform), and ADSR
**before** raising the gate, so the envelope triggers with the right parameters.

### Program changes to patches

A General MIDI program change selects a built-in SID patch via
`SidPatchLibrary.ForProgram`:

| GM program range | Family | SID patch |
|------------------|--------|-----------|
| 0-7 | Pianos | Triangle lead |
| 32-39 | Basses | Sawtooth |
| 40-55 | Strings | Triangle lead |
| 56-63 | Brass | Pulse |
| 80-87 | Synth lead | Sawtooth |
| 88-95 | Synth pad | Pulse |
| 112-119 | Percussion | Noise |
| anything else | (fallback) | Triangle lead |

This is a coarse bucketing; gaps fall through to the triangle lead. The drum
channel (MIDI channel 10) is **not** auto-routed to drums; if you want that,
call `SetPatch(9, SidPatchLibrary.DrumPatch)` yourself.

### Control changes and pitch bend

Only CC7 (channel volume) is acted on; other control changes are ignored. Pitch
bend is stored per channel and applied to later note-ons as described above.

## Patches

A `SidPatch` is one voice's timbre: a waveform, a 4-bit ADSR, a 12-bit pulse
width, and ring/sync flags.

```csharp
public readonly record struct SidPatch(
    Waveform Waveform,
    byte Attack, byte Decay, byte Sustain, byte Release,
    ushort PulseWidth = 0x0800,   // 50% duty
    bool RingMod = false,
    bool Sync = false);
```

Built-in patches (on `SidPatch`):

| Patch | Character |
|-------|-----------|
| `LeadPulse` | Bright 50%-duty pulse lead. |
| `LeadSawtooth` | Nasal saw lead. |
| `LeadTriangle` | Mellow clean lead (the engine default). |
| `BassPluck` | Plucked saw bass, fast decay. |
| `BassWarm` | Warmer triangle bass. |
| `Pad` | Soft attack, long sustain. |
| `NoiseHit` | Snare-ish percussion. |

Override a channel's patch at any time:

```csharp
bridge.SetPatch(channel: 0, SidPatch.LeadPulse);

// Or build your own:
var myLead = new SidPatch(Waveform.Pulse, attack: 0, decay: 4, sustain: 12,
                          release: 7, pulseWidth: 0x0600);
bridge.SetPatch(0, myLead);
```

## Voice allocation and stealing

Three SID voices serve all 16 MIDI channels. `VoiceAllocator` picks a voice for
each note-on in this priority order:

1. **A free voice** (not currently gated on).
2. **The oldest released voice** (a note in its release tail is the cheapest to
   interrupt).
3. **Steal the longest-held voice** (smallest start frame) if everything is still
   sounding.

There is no per-channel reservation: all channels compete for the same 3 voices.
Dense, polyphonic MIDI will drop and steal voices aggressively. For best results,
arrange or pre-thin your MIDI to 3 simultaneous notes, the way a C64 musician
would.

`bridge.VoicesActive` reports how many voices are currently gated on.

## The SMF reader

`SmfReader.Load(stream)` parses a Standard MIDI File into an `SmfFile`
(format, track count, ticks-per-quarter, and per-track event lists). Supported:

- Formats 0, 1, and 2; the `MThd` header must declare a length of 6.
- Tick-based timing only. **SMPTE timing is rejected** with a
  `NotSupportedException` (the high bit of the division field).
- Variable-length delta times and running status.
- Channel-voice messages: note on/off (note-on with velocity 0 becomes a note
  off), control change, program change, pitch bend (centered to -8192..+8191).
  Aftertouch (poly and channel) is parsed and ignored.
- Meta events: only Set Tempo (`FF 51`) and End Of Track (`FF 2F`) become events;
  other meta types and SysEx are skipped.

Tempo changes are honored through a `MidiTempoMap`, which converts ticks to
fractional frame indices at your refresh rate, integrating piecewise across tempo
segments. If a file has no tempo at tick 0, 120 BPM is assumed.

Malformed or truncated files throw `InvalidDataException` with the track index
and byte offset.

## Generating test MIDI

The `tools/BuildMidiFixture` project writes two test files to `fixtures/midi/`:
`test.mid` (a C-major arpeggio) and `fur_elise.mid` (a 3-track arrangement). Run
it to regenerate the MIDI inputs used by the sample and the tests. See
[Tools](tools.md).
