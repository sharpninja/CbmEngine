# Tools

The `tools/` directory holds the content and diagnostic utilities. The headline
one is `cbmvid` (a .NET global tool with both a CLI and a GUI); the rest are small
fixture builders and capture analyzers.

## cbmvid (encoder CLI + GUI)

`tools/EncodeCbmVid` builds the `CbmEngine.Tools.CbmVid` package, command
`cbmvid`. It encodes video, animated GIFs, and PNG sequences into the `.cbmvid`
format (see [CbmVid Video Format](cbmvid-format.md)), validates existing files,
and can render a `.cbmvid` back to an animated GIF through a real emulated VIC.

Install as a global tool:

```bash
dotnet tool install --global CbmEngine.Tools.CbmVid
cbmvid --help
```

Or run it from source:

```bash
dotnet run --project tools/EncodeCbmVid -- --in-video movie.mp4 --out movie.cbmvid
```

### CLI flags

| Flag | Argument | Meaning |
|------|----------|---------|
| `--in` | `<png-dir>` | Input directory of 320x200, palette-clean `frame_NNNN.png` files. |
| `--in-video` | `<file>` | Input video (anything ffmpeg reads). Needs ffmpeg on `PATH` or `--ffmpeg`. |
| `--in-gif` | `<file.gif>` | Input animated GIF (native decoder, preserves per-frame timing; no ffmpeg). |
| `--in-cbmvid` | `<file.cbmvid>` | Re-export an existing `.cbmvid` as a GIF preview (with `--gif-out`). |
| `--out` | `<file.cbmvid>` | Output path (required for all encode modes). |
| `--gif-out` | `<preview.gif>` | Also render each frame through a real emulator into a looping GIF (needs ROMs). |
| `--rom-base` | `<path>` | VICE ROM data dir for GIF rendering (else resolved automatically or from `CBMVID_ROM_BASE`). |
| `--ffmpeg` | `<path>` | Explicit ffmpeg binary path. |
| `--keep-frames` | | Keep intermediate extracted frames (video mode). |
| `--fps` | `<n>` | Frame rate (default 50). For GIF input, a value other than 50 overrides the GIF timing. |
| `--mode` | `mc`/`hr` | Default frame mode (`mc` = multicolor, `hr` = hires). |
| `--validate` | | Validate an existing `.cbmvid` (with `--out <path>`); prints the header. |

Unknown flags throw; a missing `--out` or input source prints usage and exits 2.

### Examples

```bash
# Encode a movie at 50 fps:
cbmvid --in-video movie.mp4 --out movie.cbmvid --fps 50

# Encode an animated GIF (no ffmpeg needed):
cbmvid --in-gif anim.gif --out anim.cbmvid --mode mc

# Encode a folder of 320x200 on-palette PNGs:
cbmvid --in pngs/ --out frames.cbmvid

# Validate a .cbmvid header:
cbmvid --validate --out movie.cbmvid

# Render an existing .cbmvid through a real emulator into a preview GIF
# (the C64 ROMs download + cache automatically on first run):
cbmvid --in-cbmvid movie.cbmvid --gif-out preview.gif

# Encode and produce an emulator-rendered preview in one shot:
cbmvid --in-video movie.mp4 --out movie.cbmvid --gif-out preview.gif
```

### The GUI studio

Run `cbmvid` with no arguments (or `cbmvid gui [folder]`) to open **CbmVidStudio**
(`tools/CbmVidStudio`), a Myra-based desktop front end to the same encoder. It
lets you open a folder of frames (or a video/GIF), preview and adjust each frame's
encoding (mode, forced background, fps), preview frames through a real emulated
VIC, re-encode a selected frame, save the `.cbmvid`, and export a GIF. If the
VICE ROMs cannot be found, the emulator preview is disabled but encoding still
works. Diagnostics are written to `%TEMP%\cbmvid-studio.log`.

## Fixture builders

These regenerate the sample/test content checked into the repo.

### BuildMidiFixture

`tools/BuildMidiFixture` writes Standard MIDI File test fixtures to
`fixtures/midi/`: `test.mid` (a C-major arpeggio sanity file) and
`fur_elise.mid` (a 3-track arrangement: melody, Alberti bass, sustained harmony).
These are the inputs for the sample's `--midi` mode and the MIDI-to-SID tests.

```bash
dotnet run --project tools/BuildMidiFixture
```

### BuildSampleVideoFixture

`tools/BuildSampleVideoFixture` writes a 3-frame `.cbmvid` to
`assets/video/intro.cbmvid` (a title-PNG frame plus two synthetic solid frames).
It is also the reference for the programmatic `CbmVidWriter` API.

```bash
dotnet run --project tools/BuildSampleVideoFixture
```

### BuildFrostPointCrt

`tools/BuildFrostPointCrt` assembles the Frost Point PSID-player cartridge to
disk: it loads `assets/sid/Frost_Point.sid`, encodes the title splash, assembles
a `PsidPlayerCart`, and writes both a raw `.bin` and a 16K `.crt` into
`artifacts/carts/`. Requires the CC65 toolchain.

```bash
dotnet run --project tools/BuildFrostPointCrt
```

## Diagnostic / reverse-engineering tools

These work from captured C64 memory dumps (the kind you snapshot from a running
emulator or a real machine) and are useful when bringing real C64 content into
the engine.

### AnalyzeFrostCapture

`tools/AnalyzeFrostCapture` parses a VIC state capture (`all-ram.bin` plus
`color-vic.bin`), reads `$D018` to compute the screen-RAM and character-set bases,
and extracts `screen.bin`, `charset.bin`, `color.bin`, and `sprites.bin` along
with a VIC register dump. This is how a live C64 screen is reverse-engineered into
the assets a `CapturedSplashCart` needs.

### DumpSprites

`tools/DumpSprites` reads a captured `all-ram.bin`, scans 64-byte blocks for
sprite-shaped bit density, and dumps candidate sprite blocks as 48x42 PNGs (using
the VIC palette) into `artifacts/captures/sprites-preview/`. Useful for finding
where sprite data lives in an unknown memory dump.

## A note on generated output

Encoded videos, GIF previews, capture dumps, and assembled carts land under
`artifacts/`, which is git-ignored. The `tools/EncodeCbmVid/nupkg/` packaged
output is also git-ignored. Treat both as disposable build output.
