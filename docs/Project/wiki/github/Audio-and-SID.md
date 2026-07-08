# Audio and SID

CbmEngine drives a real emulated SID. There are two ways to make sound:

1. **Program the SID directly** through `ISoundChipStrategy` (`machine.Sound`):
   you think in Hz, voices, ADSR, and waveforms; the engine handles the `$D400`
   register layout.
2. **Play a `.sid` file** through `MusicService`: it loads the PSID/RSID payload
   into the emulated C64 and runs the tune's own 6502 init/play code.

For playing MIDI files through the SID, see [MIDI to SID](midi-to-sid.md), which
builds on the strategy described here.

## The SID strategy

`machine.Sound` is an `ISoundChipStrategy`. It writes the SID registers for you
through the bus, so you never poke `$D4xx` by hand.

```csharp
var sid = machine.Sound;

sid.SetVolume(15);                                   // master volume 0-15 ($D418 low nibble)

// Voice 0: a 50% pulse lead at A4 (440 Hz).
sid.SetVoiceAdsr(0, attack: 0, decay: 5, sustain: 13, release: 6);
sid.SetVoicePulseWidth(0, 0x0800);                   // 12-bit pulse width (50% duty)
sid.SetVoiceFrequency(0, 440.0);                     // Hz -> $D400/$D401
sid.SetVoiceWaveform(0, Waveform.Pulse, gate: true); // note on (gate raises the envelope)

// ... later, release the note:
sid.SetVoiceWaveform(0, Waveform.Pulse, gate: false);
```

### The voice API

The SID has 3 voices (0, 1, 2). Each call below targets one voice; passing
anything outside 0-2 throws.

| Method | What it sets |
|--------|--------------|
| `SetVoiceFrequency(voice, hz)` | Oscillator frequency in Hz (converted to the 16-bit register). |
| `SetVoicePulseWidth(voice, width)` | 12-bit pulse width, 0-4095. Only audible with the Pulse waveform. |
| `SetVoiceWaveform(voice, waveform, gate, ringMod=false, sync=false, test=false)` | The control register: waveform select plus gate/ring/sync/test bits. |
| `SetVoiceAdsr(voice, attack, decay, sustain, release)` | The envelope. Each value is a 4-bit nibble, 0-15. |

`Waveform` is a `[Flags]` enum matching the SID's waveform bits, so you can
combine them (for example `Waveform.Triangle | Waveform.Pulse`):

| Value | Bit |
|-------|-----|
| `Waveform.Triangle` | 0x01 |
| `Waveform.Sawtooth` | 0x02 |
| `Waveform.Pulse` | 0x04 |
| `Waveform.Noise` | 0x08 |

**Gate semantics, exactly as on hardware:** `gate: true` starts the attack phase
(note on); `gate: false` starts the release phase (note off). `test` halts and
resets the oscillator. `sync` and `ringMod` use the adjacent voice as the
modulator.

### The filter and master volume

```csharp
// Low-pass filter on voices 0 and 1, mid cutoff, light resonance:
sid.SetFilter(
    cutoff: 0x400,        // 11-bit, 0-2047
    resonance: 4,         // 0-15
    voiceRouting: 0x03,   // bitmask: route voices 0 and 1 through the filter
    lowPass: true, bandPass: false, highPass: false,
    voice3Off: false);

sid.SetVolume(15);        // 0-15
sid.SilenceAll();         // gate off every voice and zero the volume (panic)
```

`voiceRouting` is the per-voice filter-enable bitmask in the low nibble (bit 0 =
voice 1, bit 1 = voice 2, bit 2 = voice 3, bit 3 = external). The filter-mode
flags map to the high bits of `$D418`; `voice3Off` mutes voice 3's direct output
(useful when you use voice 3 only for modulation).

### Frequency conversion

If you need the raw register value (for example, to write a table into a cart):

```csharp
ushort reg = sid.HzToRegister(440.0);     // Hz -> 16-bit SID frequency value
double hz  = sid.RegisterToHz(reg);       // and back
```

The math is the standard SID relation `Fout = Fn * Fclk / 2^24`, where `Fclk` is
`sid.ClockHz` (the system clock for the active profile: PAL is about 985248 Hz,
NTSC about 1022730 Hz).

### 6581 versus 8580

`sid.Model` reports `Mos6581` or `Mos8580` based on the machine profile. In the
current engine the two strategies differ **only** in the model they report; the
register math is identical. Any audible difference between the chips (the 6581's
nonlinear filter, combined-waveform behavior, the `$D418` volume-click sample
trick) is the emulator's responsibility, not the strategy's. Treat `Model` as a
label your code can branch on, not as a behavior switch.

## Playing a .sid file: `MusicService`

`MusicService` plays PSID/RSID tunes by loading the payload into emulated RAM and
executing the tune's own init and play routines on the emulated 6510. This is the
real thing: the SID is driven by 6502 code, exactly as on a C64.

### Loading and playing

```csharp
using CbmEngine.Systems.Audio;     // PsidLoader
using CbmEngine.Systems.Services;  // MusicService (also at ctx.Music)

using var fs = File.OpenRead("assets/sid/Frost_Point.sid");
PsidProgram psid = PsidLoader.Load(fs);

var music = ctx.Music;             // or new MusicService(machine)
music.Install(psid, song: 1);      // load payload, run init for sub-tune 1
```

Then advance playback. You have two options:

**Option A: drive it per frame from C#.** Call `Tick()` once per emulated frame
(the play routine runs each frame):

```csharp
public void Update(IGameContext context, int frameIndex)
{
    ctx.Music.Tick();   // calls the PSID play routine this frame
}
```

**Option B: install a native IRQ driver.** A small 6502 stub is written into RAM
and hooked to the IRQ, so the tune plays from a CIA timer interrupt on the
emulated CPU without per-frame calls from C#:

```csharp
music.InstallNativeIrqDriver(stubAddress: 0xC000);
// No Tick() needed; the emulated IRQ drives play. Tick() becomes a no-op.
```

### Controls

| Member | Effect |
|--------|--------|
| `Install(program, song = 1)` | Load and initialize a tune (1-based sub-tune). |
| `Tick()` | Run the play routine once (no-op if the native IRQ driver is active). |
| `InstallNativeIrqDriver(stubAddress = 0xC000)` | Install and start an IRQ-driven player. |
| `InjectIrqStubBytes(stubAddress = 0xC000)` | Write the IRQ stub without starting it; returns the bytes. |
| `SetSong(subTune)` | Switch sub-tune (re-runs init). |
| `Stop()` | Stop playback, restore the KERNAL IRQ vector if needed, silence the SID. |
| `IsPlaying`, `CurrentSong`, `CurrentHeader`, `NativeIrqDriverInstalled`, `NativeIrqStubAddress` | State. |

### How it runs the tune

`MusicService` calls the tune's `init` and `play` addresses by setting up the CPU
(accumulator = sub-tune index for `init`), pushing a sentinel return address, and
single-stepping the 6510 until the routine returns. An infinite-loop guard aborts
after 200000 cycles with a `PsidExecutionException`, so a misbehaving tune cannot
hang the host. Payloads are validated so they cannot overflow `$FFFF` or collide
with the engine-reserved zero page (`$0002`-`$00FF`), which would throw a
`PsidPlacementException`.

## The PSID / .sid file format

`PsidLoader.Load(stream)` parses a PSID or RSID file into a `PsidProgram` (a
`PsidHeader` plus the raw payload). All multi-byte header fields are **big-endian**
(the historic quirk of the SID format).

| Offset | Size | Field | Notes |
|-------:|-----:|-------|-------|
| `$00` | 4 | Magic | `"PSID"` or `"RSID"`. |
| `$04` | 2 | Version | |
| `$06` | 2 | Data offset | Where the C64 payload begins. |
| `$08` | 2 | Load address | If 0, taken from the first 2 bytes of the payload (little-endian) and stripped. |
| `$0A` | 2 | Init address | If 0, defaults to the load address. |
| `$0C` | 2 | Play address | 0 means the tune installs its own IRQ. |
| `$0E` | 2 | Song count | Number of sub-tunes. |
| `$10` | 2 | Start song | Default sub-tune (1-based; 0 normalized to 1). |
| `$12` | 4 | Speed flags | PAL/NTSC + CIA/VBI bitfield (stored verbatim). |
| `$16` | 32 | Name | ASCII, NUL-trimmed. |
| `$36` | 32 | Author | ASCII, NUL-trimmed. |
| `$56` | 32 | Released | ASCII, NUL-trimmed. |
| data offset | rest | Payload | The raw 6502 program. |

The loader honors the embedded load address and the init-default rules, and
normalizes a `start song` of 0 to 1. PSID v2NG extension fields (SID model/clock
flags, start page, page length) are skipped: only the core v1-era header plus the
raw speed dword are surfaced.

Errors throw `PsidFormatException` (carrying the offending byte offset and the
expected/observed magic).

## Shipping music as a cartridge

To bake a tune into a bootable `.CRT` that plays under a CIA-timer IRQ (with an
optional bitmap splash), use `PsidPlayerCart`. See
[Cartridges and .CRT](cartridges-and-crt.md).
