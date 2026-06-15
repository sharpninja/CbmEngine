# CbmEngine

CbmEngine is a .NET 10 game and media engine for the Commodore 64. Unlike a
sprite framework that merely *looks* like a C64, CbmEngine runs a real,
cycle-level VIC-II / SID / 6510 emulation (via the
[ViceSharp](https://github.com/sharpninja/vice-sharp) core) and gives you a
C#-friendly surface over the actual hardware: real screen RAM at `$0400`, real
sprite registers at `$D000`, a real SID at `$D400`, and real `.CRT` cartridges
assembled with `ca65`/`ld65`.

If you already know the C64, you already know most of this engine. You write
game logic in C#, but you write it *against the machine*: poke the registers
you know, lay out the memory the way you always have, and let the emulator do
the rest. The engine adds the conveniences (sprite/tilemap/music services, a
windowed host, a SID-backed MIDI synth, and a custom full-motion video format)
without hiding the hardware from you.

## What you can build

- **Native C64 games** rendered by a real VIC-II, hosted in a resizable
  MonoGame window with point-sampled (crisp) scaling, keyboard input, and live
  SID audio.
- **Bootable `.CRT` cartridges** (16K standard carts) that run in VICE or on
  real hardware: bitmap splash carts, PSID music-player carts, and full
  captured-screen-plus-sprites carts.
- **SID music playback** from standard `.sid` (PSID/RSID) files, driven either
  per-frame from C# or by a native CIA-timer IRQ on the emulated 6510.
- **MIDI-to-SID synthesis**: load a standard `.mid` file and hear it played
  through the three SID voices with General MIDI patch mapping, velocity
  response, and pitch bend.
- **CbmVid full-motion video**: encode any video, animated GIF, or PNG sequence
  into a stream of VIC-II bitmap frames (`.cbmvid`) and play it back through the
  emulated VIC.

## Documentation

Start here, then dive into the subsystem you need:

| Document | What it covers |
|----------|----------------|
| [Getting Started](docs/getting-started.md) | Prerequisites, building, running the sample, and your first game. |
| [Architecture](docs/architecture.md) | Projects, layers, the game lifecycle, the threading model. |
| [Memory and I/O](docs/memory-and-io.md) | `IMemoryService`, the RAM-vs-I/O rule, the C64 memory map. |
| [Graphics: Sprites and Tilemap](docs/graphics-sprites-tilemap.md) | VIC-II bitmap modes, the 8 hardware sprites, the 40x25 text screen, the palette. |
| [Audio and SID](docs/audio-and-sid.md) | The SID strategy API, PSID playback, `MusicService`. |
| [MIDI to SID](docs/midi-to-sid.md) | The MIDI bridge, patches, voice allocation, the SMF reader. |
| [CbmVid Video Format](docs/cbmvid-format.md) | The `.cbmvid` binary spec, the encoder pipeline, the player. |
| [Cartridges and .CRT](docs/cartridges-and-crt.md) | The `.CRT` container, the cart builders, the 6502 boot stub, `ca65`. |
| [Tools](docs/tools.md) | The `cbmvid` CLI, CbmVidStudio GUI, and the fixture/diagnostic tools. |
| [API Reference](docs/api-reference.md) | A condensed index of public types and members. |

## Quick start

```bash
# Prerequisites: .NET 10 SDK, and (for cartridges) the CC65 toolchain on PATH.
git clone --recurse-submodules https://github.com/sharpninja/CbmEngine.git
cd CbmEngine
dotnet build CbmEngine.slnx

# Run the Frost Point demo (assembles a PSID cart, boots it, opens a window):
dotnet run --project src/CbmEngine.Game.Sample

# Play a MIDI file through the SID:
dotnet run --project src/CbmEngine.Game.Sample -- --midi=fixtures/midi/fur_elise.mid

# Play a CbmVid video:
dotnet run --project src/CbmEngine.Game.Sample -- --video=assets/video/intro.cbmvid
```

See [Getting Started](docs/getting-started.md) for the full setup, including
where the VICE ROMs come from and how to install the `cbmvid` content tool.

## Solution layout

```
src/
  CbmEngine.Abstractions     Interfaces and value types (IGame, ICommodoreMachine, ...)
  CbmEngine.Systems          The engine: machine, memory, sprites, tilemap, SID, cartridges, video
  CbmEngine.Pipeline         Content pipeline: VIC palette, bitmap encoder, MIDI reader, CbmVid format
  CbmEngine.Host.MonoGame    The windowed host (window, blit, input, audio, threaded pump)
  CbmEngine.Game.Sample      The runnable sample (Frost Point demo, MIDI, video)
tools/                       cbmvid CLI/GUI, fixture builders, capture analyzers
tests/                       Unit and integration tests
external/vice-sharp          The ViceSharp emulation core (git submodule)
```

## License and credits

CbmEngine is built on the ViceSharp emulation core. The C64, VIC-II, and SID
are trademarks of their respective owners; this project is an independent,
educational engine for the platform.
