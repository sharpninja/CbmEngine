# Graphics: Sprites and Tilemap

CbmEngine renders through a real VIC-II. You set up video the way you would on a
C64 (write registers, lay out screen and color RAM, point the sprites), and the
emulated chip draws it. The engine adds two convenience services,
`SpriteService` and `TilemapService`, that wrap the register math, plus the
canonical 16-color palette in `VicPalette`.

## The palette

The C64 has 16 fixed colors. The engine's canonical RGB values live in
`CbmEngine.Pipeline.VicPalette.Colors` (indexed by VIC color number). Every
encoder, validator, and the GIF/host blit key off this exact table, so any image
you feed the pipeline must land on these values byte for byte.

| Index | Name | RGB hex |
|------:|------|---------|
| 0 | Black | `#000000` |
| 1 | White | `#FFFFFF` |
| 2 | Red | `#962835` |
| 3 | Cyan | `#5BD6C1` |
| 4 | Purple | `#9B27B1` |
| 5 | Green | `#5CB532` |
| 6 | Blue | `#1B1B8E` |
| 7 | Yellow | `#DFE56C` |
| 8 | Orange | `#9B521C` |
| 9 | Brown | `#5A3300` |
| 10 | Light Red | `#DA4644` |
| 11 | Dark Grey | `#444444` |
| 12 | Medium Grey | `#777777` |
| 13 | Light Green | `#ADFF6C` |
| 14 | Light Blue | `#6B5ED1` |
| 15 | Light Grey | `#AAAAAA` |

Helpers:

```csharp
// Exact (non-fuzzy) RGB to index. Returns false / index -1 if not on-palette.
bool ok = VicPalette.TryExact(0x96, 0x28, 0x35, out int index);  // index == 2 (red)

// Write a 16x16 palette PNG (256 cells) for ffmpeg's paletteuse filter:
VicPalette.WritePaletteImage("vic-palette.png");
```

The host exposes the same palette as packed BGRA `uint`s via
`machine.Capabilities.BgraPalette`, which is what the framebuffer uses.

## The text screen: `TilemapService`

`TilemapService` drives the standard 40x25 character screen: screen codes go to
screen RAM (`$0400`), colors go to color RAM (`$D800`). It handles the RAM/IO
split for you (screen RAM is RAM, color RAM is I/O).

```csharp
using CbmEngine.Systems.Services;

var tiles = new TilemapService(machine.Memory);   // or ctx.Tilemap on a GameContext

// Constants you can rely on:
//   TilemapService.ScreenRamBase = 0x0400
//   TilemapService.ColorRamBase  = 0xD800
//   TilemapService.Columns       = 40
//   TilemapService.Rows          = 25

tiles.Fill(glyph: 0x20, color: 6);                // clear to spaces, blue
tiles.SetCell(col: 0, row: 0, glyph: 8, color: 1); // 'H' (screen code 8), white
byte g = tiles.ReadGlyph(0, 0);                    // read a screen code back
```

| Method | Effect |
|--------|--------|
| `SetCell(col, row, glyph, color)` | Write one cell. `col` 0-39, `row` 0-24, `color` masked to 0-15. |
| `Fill(glyph, color)` | Fill the whole 40x25 screen and color RAM. |
| `ReadGlyph(col, row)` | Read the screen code at a cell. |

> **Screen codes are not PETSCII or ASCII.** A cell holds a VIC screen code (the
> index into the character generator), not a keyboard character. For the default
> uppercase character set, `A`-`Z` are screen codes 1-26 and space is `$20`.

## Hardware sprites: `SpriteService`

The VIC-II has 8 hardware sprites (slots 0-7). `SpriteService` wraps the `$D000`
register block plus the sprite pointers.

```csharp
using CbmEngine.Systems.Services;

var sprites = new SpriteService(machine.Memory);  // or ctx.Sprites

sprites.SetPointer(0, 0x0D);     // sprite 0 reads its 64 bytes from $0D*64 = $0340
sprites.SetColor(0, 2);          // 2 = red
sprites.SetPosition(0, 160, 120);// center-ish; X 0-511, Y 0-255
sprites.SetEnabled(0, true);     // turn the sprite on
```

| Method | Registers touched | Notes |
|--------|-------------------|-------|
| `SetPosition(slot, x, y)` | `$D000+slot*2` (X low), `$D001+slot*2` (Y), `$D010` (X MSB) | `x` 0-511 (the 9th bit goes into `$D010`), `y` 0-255. |
| `SetEnabled(slot, on)` | `$D015` | Read-modify-write of the slot's enable bit. |
| `SetColor(slot, color)` | `$D027+slot` | `color & 0x0F`. |
| `SetPointer(slot, blockIndex)` | `$07F8+slot` (screen RAM) | Sprite data lives at `blockIndex * 64`. |

### What the service does not wrap

`SpriteService` covers position (including the 9-bit X), enable, color, and the
pointer indirection. The remaining sprite registers you set directly with
`WriteIo`:

| Register | Function |
|----------|----------|
| `$D017` | Y expand (per sprite) |
| `$D01D` | X expand (per sprite) |
| `$D01B` | Sprite-background priority |
| `$D01C` | Multicolor sprite enable |
| `$D025` / `$D026` | Shared multicolor sprite colors 1 and 2 |

```csharp
// Make sprite 0 double-height and double-width:
byte yexp = (byte)(machine.Memory.ReadIo(0xD017) | 0x01);
byte xexp = (byte)(machine.Memory.ReadIo(0xD01D) | 0x01);
machine.Memory.WriteIo(0xD017, new[] { yexp });
machine.Memory.WriteIo(0xD01D, new[] { xexp });
```

### Putting sprite data in memory

A sprite is 24x21 pixels = 63 bytes, stored in a 64-byte block. Write the bytes
into RAM with `WriteRange`, then point a slot at the block:

```csharp
byte[] spriteBytes = /* 63 bytes of sprite bitmap */;
machine.Memory.WriteRange(0x0340, spriteBytes);   // block $0D = $0340
sprites.SetPointer(0, 0x0D);
```

## Bitmap modes

The VIC-II has two bitmap modes; the engine and its CbmVid format use both.

### Hi-res bitmap (320x200, 1 bit per pixel)

- Each bitmap byte is 8 horizontal pixels, MSB first.
- Per 8x8 cell, the screen-RAM byte supplies two colors: high nibble for set
  bits, low nibble for clear bits.
- Control register `$D016` is set to `$C8` (multicolor off).

### Multicolor bitmap (160x200, 2 bits per pixel)

- Each bitmap byte is 4 double-wide pixels.
- The four 2-bit codes select color: `00` = background (`$D021`), `01` =
  screen-RAM high nibble, `10` = screen-RAM low nibble, `11` = color-RAM low
  nibble.
- Control register `$D016` is set to `$D8` (multicolor on).

### Encoding an image to a bitmap

The pipeline turns a 320x200 PNG (already snapped to the VIC palette) into the
three memory blocks a bitmap needs:

```csharp
using CbmEngine.Pipeline;

// Multicolor (auto-picks a background color, or force one):
EncodedSplashBitmap mc = C64MulticolorBitmapEncoder.Encode(
    "title.png", forceBackgroundColor: 0x00);

// Hi-res:
EncodedSplashBitmap hr = C64MulticolorBitmapEncoder.EncodeHiRes("title.png");

// The result holds the three blocks plus mode/background:
//   mc.Bitmap    (8000 bytes)  -> $6000
//   mc.ScreenRam (1000 bytes)  -> $4400
//   mc.ColorRam  (1000 bytes)  -> $D800
//   mc.Mode, mc.BackgroundColorIndex
```

`EncodedSplashBitmap` is the currency of the bitmap path: it is what you feed to
`BitmapPlayerCart.Build` (see [Cartridges](cartridges-and-crt.md)) and what the
CbmVid player produces per frame (see [CbmVid](cbmvid-format.md)).

To display one yourself in bitmap mode, copy the three blocks into VIC bank 1 and
set the control registers:

```csharp
machine.Memory.WriteRange(0x6000, mc.Bitmap);     // bitmap
machine.Memory.WriteRange(0x4400, mc.ScreenRam);  // screen color pairs
machine.Memory.WriteIo  (0xD800, mc.ColorRam);    // color RAM (I/O)
machine.Memory.WriteIo(0xD011, new byte[] { 0x3B }); // bitmap mode on, display on
machine.Memory.WriteIo(0xD016, new byte[] { mc.Mode == SplashBitmapMode.HiRes ? (byte)0xC8 : (byte)0xD8 });
machine.Memory.WriteIo(0xD018, new byte[] { 0x18 }); // screen $4400, bitmap $6000 in bank 1
machine.Memory.WriteIo(0xD021, new[] { mc.BackgroundColorIndex });
```

> Bitmap mode also requires selecting VIC bank 1 via CIA #2 (`$DD00`/`$DD02`).
> The engine's cartridge boot stubs do this for you; if you set up bitmap mode
> from scratch in RAM, replicate that bank switch. See the cart source in
> `BitmapPlayerCartSource` for the exact sequence.

## The framebuffer

After `machine.RunFrame()`, the VIC framebuffer is available as BGRA bytes:

```csharp
ReadOnlySpan<byte> bgra = sys.VideoChip.FrameBuffer;
int w = sys.VideoChip.FrameWidth;
int h = sys.VideoChip.FrameHeight;
```

The host uploads this to a texture each frame; for headless use, `FramebufferPng.Write`
saves it to a PNG (see [Getting Started](getting-started.md)).
