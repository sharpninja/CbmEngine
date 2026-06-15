# Getting Started

This guide takes you from a clean checkout to a running game window, then to
your own first game.

## 1. Prerequisites

| Requirement | Why | Notes |
|-------------|-----|-------|
| .NET 10 SDK | Everything targets `net10.0`. | `dotnet --version` should report 10.x. |
| Git (with submodules) | The ViceSharp emulation core lives in `external/vice-sharp`. | Clone with `--recurse-submodules`, or run `git submodule update --init --recursive`. |
| CC65 toolchain (`ca65`, `ld65`) | Required only to build `.CRT` cartridges (the default demo, PSID carts, bitmap carts). | Install from [cc65.github.io](https://cc65.github.io) and put `ca65`/`ld65` on your `PATH`. Not needed for MIDI or for the engine API itself. |
| FFmpeg | Required only to encode video (not GIF or PNG) into CbmVid. | On `PATH`, or pass an explicit path to the tool. |

The VICE ROMs (`kernal`, `basic`, `chargen`) ship inside the `vice-sharp`
submodule at `external/vice-sharp/native/vice/vice/data`. The sample app finds
them automatically by walking up from the executable to the directory holding
`CbmEngine.slnx`. If you embed the engine elsewhere, point `RomProvider` at that
data directory yourself (see step 5).

## 2. Build

```bash
git clone --recurse-submodules https://github.com/sharpninja/CbmEngine.git
cd CbmEngine
dotnet build CbmEngine.slnx
```

## 3. Run the sample

The sample project (`src/CbmEngine.Game.Sample`) is the reference for everything
the engine does. With no arguments it runs the **Frost Point** demo: it loads a
PSID tune, optionally encodes a title splash, assembles a 16K PSID-player
cartridge with `ca65`, attaches it to the emulated C64, waits for the cartridge
boot marker, and then opens a window.

```bash
dotnet run --project src/CbmEngine.Game.Sample
```

Press `Esc` to exit. If you see
`ERROR  ca65/ld65 not found on PATH`, install CC65 (the cartridge path needs it).

### Other sample modes

| Command | What it does |
|---------|--------------|
| `dotnet run --project src/CbmEngine.Game.Sample -- --midi=fixtures/midi/fur_elise.mid` | Plays a MIDI file through the SID (no CC65 needed). |
| `dotnet run --project src/CbmEngine.Game.Sample -- --video=assets/video/intro.cbmvid` | Streams a CbmVid video into the VIC. |
| `... -- --headless --frames=120 --dump-png=out.png` | Runs without a window and writes the final framebuffer to a PNG. Useful for CI and screenshots. |

Command-line flags accepted by the sample:

| Flag | Meaning |
|------|---------|
| `--headless` | Run the frame loop without opening a window. |
| `--dump-png=<path>` | In headless mode, write the final framebuffer to a PNG. |
| `--frames=<n>` | Number of frames to run headless (default 60). |
| `--video=<asset>` | Run the CbmVid video player instead of the demo. |
| `--midi=<file.mid>` | Run the MIDI-to-SID player. |

## 4. Your first game

A game is any class that implements `IGame` (three methods). The host calls
`Initialize` once, then `Update` and `Draw` once per emulated frame.

```csharp
using CbmEngine.Abstractions;
using CbmEngine.Systems;            // GameContext
using CbmEngine.Systems.Services;   // SpriteService, TilemapService

public sealed class HelloGame : IGame
{
    public void Initialize(IGameContext context)
    {
        // IGameContext only exposes .Machine. The sprite/tilemap/music
        // services live on the concrete GameContext, so cast to reach them.
        var ctx = (GameContext)context;

        // Clear the 40x25 text screen to spaces, light-blue on the default bg.
        ctx.Tilemap.Fill(glyph: 0x20, color: 14);

        // Write "HI" at the top-left. Screen codes: H = 8, I = 9.
        ctx.Tilemap.SetCell(col: 0, row: 0, glyph: 8, color: 1);  // white
        ctx.Tilemap.SetCell(col: 1, row: 0, glyph: 9, color: 1);

        // Set the border ($D020) and background ($D021) colors directly.
        ctx.Machine.Memory.WriteIo(0xD020, new byte[] { 0x00 }); // black border
        ctx.Machine.Memory.WriteIo(0xD021, new byte[] { 0x06 }); // blue background
    }

    public void Update(IGameContext context, int frameIndex)
    {
        // Per-frame logic. Animate the border by poking $D020.
        byte color = (byte)((frameIndex / 8) & 0x0F);
        context.Machine.Memory.WriteIo(0xD020, new[] { color });
    }

    public void Draw(IGameContext context, int frameIndex) { }
}
```

> **Why the cast?** `IGameContext` deliberately exposes only `Machine`. The
> higher-level helpers (`Sprites`, `Tilemap`, `Music`) are properties on the
> concrete `GameContext`. Either cast as above, or keep your own reference to
> the `GameContext` you constructed.

## 5. Host it

Build a C64 system, wrap it in a `GameContext`, and hand both to a
`MonoGameHost`:

```csharp
using CbmEngine.Host.MonoGame;
using CbmEngine.Systems;
using ViceSharp.RomFetch;

// Point RomProvider at the VICE data directory (kernal/basic/chargen).
var roms = new RomProvider(@"external/vice-sharp/native/vice/vice/data");

// Build a PAL C64. Supported profile ids: "c64", "c64c", "ntsc", "newntsc".
var sys = CommodoreSystem.Build("c64", roms);

var ctx  = new GameContext(sys);
var game = new HelloGame();

// MonoGameHost takes sys.Underlying (the raw IMachine), NOT the CommodoreSystem.
using var host = new MonoGameHost(sys.Underlying, game: game, gameContext: ctx);
host.Run();   // blocks until Esc or window close
```

That is the whole contract. Everything else in the engine is a convenience on
top of `sys.Memory` (RAM and registers), `sys.Sound` (the SID), and the
emulated CPU.

## 6. Headless rendering (tests and screenshots)

For tests or batch rendering you can drive the loop yourself and snapshot the
VIC framebuffer:

```csharp
using CbmEngine.Systems.Boot;   // FramebufferPng

game.Initialize(ctx);
for (int i = 0; i < 120; i++)
{
    game.Update(ctx, i);
    sys.RunFrame();             // advance the emulator one full frame
}
FramebufferPng.Write("frame.png",
    sys.VideoChip.FrameBuffer, sys.VideoChip.FrameWidth, sys.VideoChip.FrameHeight);
```

## Next steps

- Learn the one rule that governs all memory access: [Memory and I/O](memory-and-io.md).
- Understand how the pieces fit and the threading model: [Architecture](architecture.md).
- Draw sprites and text: [Graphics: Sprites and Tilemap](graphics-sprites-tilemap.md).
- Make sound: [Audio and SID](audio-and-sid.md).
