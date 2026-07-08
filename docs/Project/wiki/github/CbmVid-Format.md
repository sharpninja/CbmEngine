# CbmVid Video Format

CbmVid is a custom full-motion video format for the C64. A `.cbmvid` file is a
stream of VIC-II bitmap-mode frames: each frame is a complete bitmap + screen RAM
+ color RAM snapshot, played back through a real emulated VIC. There is no
compression and no inter-frame delta; every frame is a full 320x200 picture.

This document is the authoritative byte-level spec, plus the encoder pipeline and
the runtime player. To encode files from the command line, see the `cbmvid` tool
in [Tools](tools.md).

## File layout at a glance

```
+--------------------------+  offset 0
|  64-byte header          |
+--------------------------+  offset 64
|  frame record 0 (10004B) |
+--------------------------+
|  frame record 1 (10004B) |
+--------------------------+
|  ...                     |
+--------------------------+
```

Total file size = `64 + frameCount * 10004`. The player validates this exactly
for seekable streams. Frames are randomly seekable:
`frameOffset = 64 + index * 10004`.

All multi-byte integers are **little-endian** (matching the 6502/VIC-II).

## Header (64 bytes)

| Offset | Size | Type | Field | Value / notes |
|-------:|-----:|------|-------|---------------|
| 0 | 7 | bytes | Magic | `"CBMVID\0"` = `43 42 4D 56 49 44 00` (includes a trailing NUL) |
| 7 | 1 | u8 | Version | `0x01` |
| 8 | 2 | u16 | Width | Always `320` |
| 10 | 2 | u16 | Height | Always `200` |
| 12 | 2 | u16 | FrameRate | Frames per second (for example, 50) |
| 14 | 2 | u16 | Reserved | `0x0000` |
| 16 | 4 | u32 | FrameCount | Number of frame records. Back-patched on finalize (see below) |
| 20 | 1 | u8 | DefaultMode | `0` = Multicolor, `1` = HiRes |
| 21 | 1 | u8 | Flags | Producer-defined. Bit 0 is a loop hint; the encoder sets `1` for animated-GIF sources |
| 22 | 2 | u16 | FrameRecordSize | Always `10004`. The player rejects any other value |
| 24 | 40 | bytes | Padding | Zero-filled remainder of the 64-byte header |

A reader rejects a version greater than 1 (`NotSupportedException`) and any frame
record size other than 10004.

### FrameCount back-patching

A producer can stream frames without knowing the count up front: write a
placeholder `FrameCount`, write frames, then call `FinalizeFrameCount()`, which
seeks to offset 16 and rewrites the true count. This works only for seekable
output. For a non-seekable stream (a pipe), you must supply the exact count when
you write the header.

## Frame record (10004 bytes)

| Offset in record | Size | Field | Maps to (engine VIC bank 1) |
|-----------------:|-----:|-------|-----------------------------|
| 0 | 1 | Mode | `0` = Multicolor, `1` = HiRes (per frame; may differ from the header default) |
| 1 | 1 | BackgroundColorIndex | VIC background color 0-15 (meaningful in multicolor) |
| 2 | 2 | (gap) | Unused / zero |
| 4 | 8000 | Bitmap | `$6000` |
| 8004 | 1000 | Screen RAM | `$4400` |
| 9004 | 1000 | Color RAM | `$D800` |

The three regions are raw VIC-II bitmap-mode memory, so the pixel encoding is
exactly the hardware layout (see the bitmap-mode section in
[Graphics](graphics-sprites-tilemap.md)). A player simply copies them into the
right banks. There is no per-frame timestamp; timing is global via the header's
`FrameRate`.

> The per-frame `BackgroundColorIndex` is carried in the record but the runtime
> player does **not** write it to `$D021`; the displaying cartridge owns the
> background register. The bitmap player cart sets it from frame 0.

## The encoder pipeline

The encoder lives in `CbmEngine.Pipeline.CbmVid`. Three ingest paths converge on
one core encoder, and all produce on-palette VIC bitmap frames.

```
animated GIF  --\
video (ffmpeg) ---> resize 320x200 --> snap to VIC palette --> pack to bitmap --> write records
PNG sequence  --/         (Floyd-Steinberg dither)            (EncodedSplashBitmap)
```

| Entry point | Source | Palette snapping |
|-------------|--------|------------------|
| `CbmVidEncoder.EncodeAnimatedGif(gif, out, ...)` | Animated GIF | In-process ImageSharp quantizer with Floyd-Steinberg dither. Derives fps from the GIF frame delay. Sets `Flags` bit 0. |
| `CbmVidEncoder.EncodeVideo(video, out, ...)` | Any ffmpeg-readable video | ffmpeg `fps` + `scale` + `paletteuse` in one pass against the VIC palette image. Requires ffmpeg. |
| `CbmVidEncoder.EncodeDirectory(pngDir, out, ...)` | A folder of `frame_*.png` | None; PNGs must already be exactly on-palette. |
| `CbmVidEncoder.Encode(manifest)` | A `CbmVidEncodeManifest` | Per-frame strict-palette validation (configurable). |

### Strict palette validation

`EncodeDirectory`/`Encode` validate every pixel of every PNG against the VIC
palette by default. A pixel that is not exactly one of the 16 colors throws a
`CbmVidEncodeException` carrying the frame index, the path, and the offending
x/y and color. The GIF and video paths do their own quantization, so they bypass
this check. If you pre-render frames yourself, snap them to the exact RGB values
in [the palette table](graphics-sprites-tilemap.md#the-palette) first, or you
will fail validation.

### Writing frames programmatically

You can author a `.cbmvid` directly with `CbmVidWriter` (this is what
`tools/BuildSampleVideoFixture` does):

```csharp
using CbmEngine.Pipeline;
using CbmEngine.Pipeline.CbmVid;

var header = new CbmVidHeader(
    Width: 320, Height: 200, FrameRate: 50,
    FrameCount: 3, DefaultMode: CbmVidFrameMode.Multicolor, Flags: 1);

using var stream = File.Create("intro.cbmvid");
using var writer = new CbmVidWriter(stream, header);

// Each frame is an EncodedSplashBitmap (8000 + 1000 + 1000 bytes).
EncodedSplashBitmap frame0 = C64MulticolorBitmapEncoder.Encode(
    "title.png", forceBackgroundColor: 0x00);
writer.WriteFrame(frame0);
// ... write more frames ...

writer.FinalizeFrameCount();   // back-patch the count (also done on Dispose)
```

`CbmVidWriter` validates that the stream is writable and the geometry is 320x200,
and that each frame's three buffers are the right sizes (8000/1000/1000).

## The runtime player

`CbmEngine.Systems.Video.VideoPlayer` streams a `.cbmvid` and DMAs each frame
into emulated C64 memory.

```csharp
using CbmEngine.Systems.Video;

using var stream = File.OpenRead("intro.cbmvid");
using var player = new VideoPlayer(stream);

player.Loop = true;                       // rewind at the end instead of stopping

// Per frame: copy the current frame into VIC memory, then advance the machine.
public void Update(IGameContext context, int frameIndex)
{
    _player.PumpFrame(context.Machine.Memory);
    // the host calls sys.RunFrame() after Update
}
```

This is the sample's `VideoGame`. To actually see the video you must first put
the machine into bitmap mode; the usual approach is to boot a `BitmapPlayerCart`
built from the video's first frame, which sets up VIC bank 1 and bitmap mode,
then stream subsequent frames into the same banks. The sample's `--video` path
does exactly this (see `RunVideo` in the sample's `Program.cs`).

### Player members

| Member | Effect |
|--------|--------|
| `PumpFrame(memory)` | Write the current frame to `$6000`/`$4400`/`$D800` and advance. Returns false when finished and not looping. Only re-pokes `$D016` when the mode changes. |
| `Header` | The parsed header. |
| `CurrentFrame`, `IsFinished`, `Loop` | State. |
| `Reset()` | Seek to frame 0 (seekable streams). |
| `Seek(index)` | Jump to a frame (seekable streams). |
| `PeekFrame(index)` | Read a frame without moving the cursor (returns an `EncodedSplashBitmap`). |
| `PeekFrame0AsSplash()` | Frame 0 as a splash bitmap (handy for building the boot cart). |
| `Validate(stream)` / `ValidateFile(path)` | Parse the header and (for seekable input) check the file length matches `64 + frameCount * 10004`. |

The only playback optimization is that `$D016` (the multicolor/hires control bit)
is written only when a frame's mode differs from the previous one.

## Round-tripping to GIF

`CbmVidGifExporter.Export` renders a `.cbmvid` back to an animated GIF by actually
booting a bitmap-player cart in the emulator and capturing the VIC framebuffer per
frame. It is both a preview tool and a correctness check: it proves the file
renders on a real VIC-II, not just that the bytes are well formed.

```csharp
using CbmEngine.Systems.Video;
CbmVidGifExporter.Export("intro.cbmvid", "preview.gif", roms);
```

The same capability is exposed by the `cbmvid` CLI (`--gif-out` / `--in-cbmvid`).

## Summary of invariants

- Geometry is fixed at 320x200; the writer rejects anything else.
- Every frame is a full 10004-byte snapshot; file size is linear in frame count.
- `FrameCount` is authoritative and self-correcting on seekable output.
- Bitmap maps to `$6000`, screen RAM to `$4400`, color RAM to `$D800` (the last
  written through the I/O bus).
- Frames must be exactly on the VIC palette to pass strict validation.
