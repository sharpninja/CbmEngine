# Architecture

CbmEngine is layered so that the parts that know about the C64 are separated
from the parts that know about your host (window, threads, audio device). At the
bottom is a real emulator; everything above it is a thin, hardware-honest
convenience layer.

## Projects and layers

```
+--------------------------------------------------------------+
|  CbmEngine.Game.Sample        your game + the runnable host  |
+--------------------------------------------------------------+
|  CbmEngine.Host.MonoGame      window, blit, input, audio,    |
|                               threaded emulator pump         |
+--------------------------------------------------------------+
|  CbmEngine.Systems            the engine:                    |
|    Strategy/  Memory/  Services/  Sound/  Audio/             |
|    Cartridge/ Boot/  Video/  Midi/  LineDirector/            |
+--------------------------------------------------------------+
|  CbmEngine.Pipeline           content build-time:            |
|    VicPalette, bitmap encoder, CbmVid format, MIDI reader    |
+--------------------------------------------------------------+
|  CbmEngine.Abstractions       interfaces + value types       |
+--------------------------------------------------------------+
|  ViceSharp (NuGet)            VIC-II / SID / 6510 emulation  |
+--------------------------------------------------------------+
```

- **CbmEngine.Abstractions** holds the contracts: `IGame`, `IGameContext`,
  `ICommodoreMachine`, `IMemoryService`, `ISoundChipStrategy`, `IBlitTarget`,
  `IClockSource`, `IInputScript`, plus value types like `MachineCapabilities`,
  `Waveform`, `SidModel`, and `LineProgram`. It has no behavior, only shape.
- **CbmEngine.Pipeline** is the build-time content layer: the canonical 16-color
  VIC palette, the PNG-to-VIC-bitmap encoder, the CbmVid video format, and the
  Standard MIDI File reader. It does not touch the emulator.
- **CbmEngine.Systems** is the engine proper: it wraps a ViceSharp machine
  (`CommodoreMachine`), exposes safe memory access (`MemoryService`), the
  gameplay services (`SpriteService`, `TilemapService`, `MusicService`), the SID
  strategy, the cartridge builders, the boot harness, the CbmVid player, and the
  MIDI-to-SID bridge.
- **CbmEngine.Host.MonoGame** turns the engine into an interactive app: a
  window, the framebuffer blit, keyboard bridging, SID audio output, and a
  background thread that runs the emulator at a steady rate.

Target framework is `net10.0` with nullable reference types and implicit usings
enabled (see `Directory.Build.props`).

## The object graph

Construction flows top-down from a single factory call:

```
CommodoreSystem.Build("c64", roms)                 // static factory / entry point
   |
   +-- ArchitectureBuilder(roms).Build(C64Descriptor(profile))   -> ViceSharp IMachine
   +-- new CommodoreMachine(machine, profile)
          +-- new MemoryService(systemRam, bus)     // safe RAM + I/O access
          +-- new Sid6581Strategy | Sid8580Strategy // high-level SID control
          +-- MachineCapabilities                   // timing + BGRA palette

new GameContext(machine)                            // you create this per game
   +-- new SpriteService(machine.Memory)
   +-- new TilemapService(machine.Memory)
   +-- new MusicService(machine)                    // grabs the emulated 6510

new MonoGameHost(machine.Underlying, game, gameContext)   // wraps it in a window
```

`CommodoreSystem.Build` validates the profile id against the supported set
(`c64`, `c64c`, `ntsc`, `newntsc`), constructs the ViceSharp machine, and wraps
it in a `CommodoreMachine`. `ICommodoreMachine` is your handle to the hardware:

| Member | What it gives you |
|--------|-------------------|
| `Underlying` | The raw ViceSharp `IMachine` (escape hatch). |
| `Memory` | `IMemoryService`: safe RAM and register access. |
| `Sound` | `ISoundChipStrategy`: high-level SID programming. |
| `VideoChip` | The VIC-II: `FrameBuffer`, `FrameWidth`, `FrameHeight`. |
| `Bus` | The system bus for direct `Read`/`Write`. |
| `Clock` | The cycle clock (steppable). |
| `Capabilities` | Timing constants, SID model, the BGRA palette. |
| `RunFrame()` | Advance the emulator by one full video frame. |

## The game lifecycle

`IGame` has exactly three methods and no teardown hook:

```csharp
public interface IGame
{
    void Initialize(IGameContext context);          // called once
    void Update(IGameContext context, int frameIndex);  // per frame, before the machine steps
    void Draw(IGameContext context, int frameIndex);    // per frame
}
```

Per frame, the host:

1. Drains pending keyboard input into the emulated keyboard matrix.
2. Calls `game.Update(context, frameIndex)`.
3. Calls `machine.RunFrame()` to advance the VIC/SID/CPU one frame.
4. Pumps SID audio samples to the audio device.
5. Copies the VIC framebuffer for display.

Because the VIC-II does the actual rendering, `Draw` is frequently a no-op: you
"draw" by writing to screen RAM, sprite registers, and the bitmap during
`Update`, and the emulated VIC paints them when `RunFrame` runs. `Draw` exists
for code that wants a distinct render phase.

> **Note on `frameIndex`:** it is a monotonically increasing frame counter. With
> the default threaded pump it advances at whatever rate the emulator sustains,
> which is not guaranteed to be exactly the refresh rate. If you need
> wall-clock-accurate timing (for example, MIDI tempo), derive your own time
> base from a stopwatch rather than from `frameIndex`. The sample's `MidiGame`
> does exactly this.

## The threading model

By default `MonoGameHost` runs the emulator on a dedicated background thread
(`EmulatorPump`, the "hybrid pump"), decoupling emulation cadence from the
render thread:

- The **pump thread** drains input, calls `game.Update`, runs `machine.RunFrame`,
  pumps SID, and copies the framebuffer under a lock, pacing itself to the target
  Hz (PAL `50.125` by default).
- The **render thread** (MonoGame `Draw`) acquires the latest framebuffer,
  uploads it to a texture, and blits it to the window with
  `SamplerState.PointClamp` (crisp, no blur), scaled by `windowScale` (default 2x).

Practical consequence: **your `IGame.Update` runs on the pump thread, not the UI
thread.** Keep it free of UI-framework calls. If you need to inject extra input
from your own UI, use `CbmViewport.EnqueueKey` / `EmulatorPump.EnqueueKey`, which
are thread-safe.

The pump also tracks `FramesCompleted`, `LateFrames` (frames that missed their
deadline), and `AverageEmulatorStepMs` for diagnostics. The `MonoGameHost` title
bar shows an FPS overlay.

You can opt out of the threaded pump (`useHybridPump: false`) to run the
emulator synchronously on the render thread, which is simpler to reason about but
couples emulation speed to rendering.

## Embedding without MonoGameHost

If you have your own MonoGame `Game` subclass (for example, one that also draws a
Myra UI), use `CbmViewport` directly. It composes the pump, the BGRA-to-RGBA
blit target, the keyboard bridge, and SID audio, but does not own the window, so
it embeds cleanly:

```csharp
// In LoadContent:
_viewport = new CbmViewport(machine, GraphicsDevice, game: myGame, gameContext: ctx);

// In Update:
_viewport.Update(gameTime);

// In Draw (you own Begin/End):
_spriteBatch.Begin(samplerState: SamplerState.PointClamp);
_viewport.Draw(_spriteBatch, new Rectangle(0, 0, w, h));
// ... draw your own UI on top ...
_spriteBatch.End();
```

## The one rule: RAM versus I/O

The single most important concept in the engine is that **direct RAM access and
I/O-register access are strictly separated**, and mixing them throws. This is
covered in full in [Memory and I/O](memory-and-io.md), but in short:

- RAM (`$0000`-`$CFFF`, `$E000`-`$FFFF`, screen RAM `$0400`, sprite pointers
  `$07F8`): use `Memory.View` / `WriteRange` / `Snapshot`.
- I/O registers (`$D000`-`$DDFF`: VIC, SID, color RAM, CIA): use `Memory.WriteIo`
  / `ReadIo`, or the higher-level services that route correctly.

## Where to go next

- [Memory and I/O](memory-and-io.md) for the memory model in detail.
- [Graphics: Sprites and Tilemap](graphics-sprites-tilemap.md) to put pixels on screen.
- [Cartridges and .CRT](cartridges-and-crt.md) to ship a bootable cart.
