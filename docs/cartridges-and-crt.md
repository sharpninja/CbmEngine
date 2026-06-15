# Cartridges and .CRT

CbmEngine can build real, bootable 16K Commodore 64 cartridges: a raw ROM image
with the CBM80 autostart header, a 6502 boot stub assembled with `ca65`/`ld65`,
and a `.CRT` container that VICE (and real hardware via a cartridge flasher) can
load. Three ready-made cart types are provided, and you can assemble your own.

Building carts requires the **CC65 toolchain** (`ca65`, `ld65`) on your `PATH`.
See [Getting Started](getting-started.md).

## The two layers

There are two distinct artifacts, and it helps to keep them straight:

1. **The raw ROM image** is the 16384-byte (`$4000`) binary that maps to
   `$8000`-`$BFFF` on a C64. It starts with the CBM80 autostart header and
   contains your boot stub plus embedded data. This is what the `*Cart.Build`
   methods produce.
2. **The `.CRT` container** is the VICE/CCS64 file format that wraps that ROM with
   a 64-byte header and a `CHIP` packet. This is what you save to disk and load in
   an emulator. `CrtFile.WrapStandard16K` produces it.

## Ready-made cart builders

All three return a raw 16K ROM `byte[]`. Wrap it with `CrtFile.WrapStandard16K`
to get a loadable `.CRT`.

### Bitmap splash cart

Displays a single full-screen bitmap (hi-res or multicolor) and parks. No audio.

```csharp
using CbmEngine.Pipeline;            // C64MulticolorBitmapEncoder
using CbmEngine.Systems.Cartridge;   // BitmapPlayerCart, CrtFile

EncodedSplashBitmap splash = C64MulticolorBitmapEncoder.Encode("title.png");
byte[] rom = BitmapPlayerCart.Build(splash);
byte[] crt = CrtFile.WrapStandard16K(rom, "MY SPLASH");
File.WriteAllBytes("splash.crt", crt);
```

### PSID music-player cart

Plays a `.sid` tune under a CIA-timer IRQ, with an optional bitmap splash and a
border-color "heartbeat" that cycles every N frames.

```csharp
using CbmEngine.Systems.Audio;       // PsidLoader
using CbmEngine.Systems.Cartridge;   // PsidPlayerCart

using var fs = File.OpenRead("tune.sid");
PsidProgram psid = PsidLoader.Load(fs);

byte[] rom = PsidPlayerCart.Build(
    psid,
    backgroundColor: 0x00,
    initialBorderColor: 0x00,
    borderCyclePeriodFrames: 50,     // border increments every 50 IRQs
    splash: C64MulticolorBitmapEncoder.Encode("title.png"));  // optional

byte[] crt = CrtFile.WrapStandard16K(rom, "MY TUNE");
File.WriteAllBytes("tune.crt", crt);
```

The SID payload must fit the cartridge's payload slot (the guard allows up to
`0x1700` bytes when a splash is present). The cartridge hooks the KERNAL IRQ
vector, programs CIA #1 Timer A for the PAL frame rate, and calls the tune's
play routine each interrupt.

### Captured-screen cart

Freezes a live text-mode screen, with a custom character set, screen RAM, color
RAM, and up to 8 sprites, into a bootable cart that also plays a tune. Used to
turn a captured C64 screen into a self-contained demo.

```csharp
var assets = new CapturedSplashAssets(
    Charset2048: charset,          // 2048 bytes
    Screen1000: screen,            // 1000 bytes
    Color1000: color,              // 1000 bytes
    Sprites832: sprites,           // 512, 832, or 2048 bytes (512 copied to RAM)
    SpritePointers8: pointers,     // 8 bytes  -> $07F8
    SpriteXY16: spriteXy,          // 16 bytes -> $D000-$D00F
    SpriteEnable: enable,          // $D015
    SpriteMulticolor: mc,          // $D01C
    SpriteMc1: mc1, SpriteMc2: mc2,// $D025/$D026
    SpriteColors8: spriteColors,   // 8 bytes  -> $D027-$D02E
    D011: d011, D016: d016, D018: d018,
    BgColor: bg, BorderColor: border);

byte[] rom = CapturedSplashCart.Build(psid, assets);
```

## The boot sequence (all carts)

Every assembled cart shares the same prologue and a marker handshake the engine
uses to detect a successful boot:

1. The CBM80 header at `$8000` makes the C64 autostart the cart on reset.
2. The boot stub runs the standard init: `SEI / LDX #$FF / TXS / CLD / LDA #$37 /
   STA $01` (disable IRQ, reset the stack, clear decimal mode, make ROML + BASIC
   + KERNAL + I/O all visible).
3. The stub copies embedded data from ROM into RAM (bitmap to `$6000`, screen to
   `$4400`, color RAM to `$D800`, the SID payload to its load address, etc.),
   sets up the VIC (and the bank switch via CIA #2 for bitmap modes), and installs
   an IRQ if there is audio.
4. As its last meaningful step it writes a 2-byte marker, `$CB $42`, to
   `$0334`/`$0335` (in the unused cassette buffer), then `CLI` and parks.

The marker is how `CartridgeBoot.AttachAndWaitForMarker` knows the cart booted.

## Booting a cart in the emulator

```csharp
using CbmEngine.Systems.Cartridge;

var sys = CommodoreSystem.Build("c64", roms);
CartridgeBootResult boot = CartridgeBoot.AttachAndWaitForMarker(sys, rom, maxFrames: 600);

if (!boot.MarkerSeen)
    throw new InvalidOperationException(
        $"Cart did not boot within {boot.FramesUntilMarker} frames.");
```

`AttachAndWaitForMarker` attaches the ROM through the machine's cartridge port
(16K or 8K mapping based on length), resets, and steps frames until the marker
appears or `maxFrames` is reached. After it returns, the cart's IRQ (if any) is
live and you can continue running frames; the sample then hands control to a
normal `IGame` for game-side rendering.

## The CBM80 ROM header

`CartridgeImage.Build16K` lays out the raw ROM. Offsets relative to `$8000`:

| Offset | Size | Contents |
|--------|------|----------|
| `$8000` | 2 | Cold-start vector (little-endian) |
| `$8002` | 2 | Warm-start vector (defaults to cold) |
| `$8004` | 5 | `$C3 $C2 $CD $38 $30` = "CBM80" autostart signature |
| `$8009` | ... | Executable code begins here |

Useful constants: `CartridgeImage.Size16K = 0x4000`, `RomBase = 0x8000`,
`CodeStart = 0x8009`. Code is limited to `Size16K - 9` bytes.

## The .CRT container

`CrtFile.WrapStandard16K(rom16K, name)` wraps a 16384-byte ROM into a CRT v1.0
Standard (type 0) cartridge with a single `CHIP` packet at `$8000`. The result is
`64 + 16 + 16384 = 16464` bytes. All multi-byte fields in the CRT format are
**big-endian** (unlike the rest of the little-endian C64 world).

### 64-byte CRT header

| Offset | Size | Value | Meaning |
|--------|------|-------|---------|
| `$00` | 16 | `"C64 CARTRIDGE   "` | Signature (space-padded to 16) |
| `$10` | 4 | `0x00000040` | Header length (64) |
| `$14` | 2 | `0x0100` | Version 1.0 |
| `$16` | 2 | `0x0000` | Cartridge type 0 (Normal/Standard) |
| `$18` | 1 | `0x00` | EXROM line (asserted) |
| `$19` | 1 | `0x00` | GAME line (asserted) |
| `$1A` | 2 | `0x0000` | Reserved |
| `$1C` | 4 | `0` | Reserved |
| `$20` | 32 | name | Cartridge name (truncated to 32 bytes) |

EXROM=0 and GAME=0 with a 16K ROM is the C64 "16K cartridge" configuration:
ROML (`$8000`-`$9FFF`) and ROMH (`$A000`-`$BFFF`) are both mapped, which is why
these carts can place data across the whole `$8000`-`$BFFF` window.

### 16-byte CHIP packet (at file offset `$40`)

| Rel. offset | Size | Value | Meaning |
|-------------|------|-------|---------|
| `$00` | 4 | `"CHIP"` | Packet magic |
| `$04` | 4 | `0x00004010` | Packet length (16 + 16384) |
| `$08` | 2 | `0x0000` | Chip type 0 (ROM) |
| `$0A` | 2 | `0x0000` | Bank 0 |
| `$0C` | 2 | `0x8000` | Load address |
| `$0E` | 2 | `0x4000` | ROM size (16384) |
| `$10` | 16384 | ROM | The raw ROM payload |

## Assembling your own cart

The cart builders use `Ca65Assembler`, a thin wrapper over `ca65` and `ld65`. You
can use it directly to assemble arbitrary ca65 source with binary includes:

```csharp
using CbmEngine.Systems.Cartridge;

if (!Ca65Assembler.IsAvailable())
    throw new InvalidOperationException("Install CC65 (ca65/ld65) and put it on PATH.");

var asm = new Ca65Assembler();   // resolves ca65/ld65 on PATH
byte[] rom = asm.Build(
    asmSource,        // your ca65 source string
    linkerConfig,     // your ld65 link.cfg string
    includeBinaries:  // files referenced by .incbin "name"
        new Dictionary<string, byte[]> { ["data.bin"] = myData });
```

`Build` writes the source, the link config, and each include into a temp
directory, runs `ca65 -t none` then `ld65 -C link.cfg`, and returns the linked
binary (with a 30-second per-tool timeout and full stdout/stderr on failure). The
filenames you use as dictionary keys must match the names in your `.incbin`
directives, and the linker config's segments give each include its load address.

The provided `*CartSource` classes (`BitmapPlayerCartSource`,
`PsidPlayerCartSource`) are good references for writing your own: they show the
segment layout, the boot stub, the VIC bank switch, and the IRQ installer. See
the [API Reference](api-reference.md) for the segment maps.

## End-to-end

```
PNG splash --> C64MulticolorBitmapEncoder --> EncodedSplashBitmap --\
SID file   --> PsidLoader ----------------------> PsidProgram -------+--> *Cart.Build
                                                                     |    (ca65 + ld65)
                                                                     v
                                                          16384-byte raw ROM
                                                                     |
                                                       CrtFile.WrapStandard16K
                                                                     v
                                                             16464-byte .CRT
                                                                     |
                                              load in VICE / CartridgeBoot.AttachAndWaitForMarker
```

To write both a raw `.bin` and a `.crt` to disk in one shot, see
`tools/BuildFrostPointCrt` in [Tools](tools.md).
