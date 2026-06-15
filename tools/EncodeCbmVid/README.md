# cbmvid

A .NET global tool that encodes video, animated GIFs, or PNG sequences into the `.cbmvid` format consumed by CbmEngine — a stream of C64 multicolor / hi-res bitmap frames playable through a real VIC-II emulator.

## Install

```
dotnet tool install --global CbmEngine.Tools.CbmVid
```

## Usage

```
cbmvid --in-video movie.mp4 --out movie.cbmvid --fps 50
cbmvid --in-gif anim.gif    --out anim.cbmvid
cbmvid --in pngs/           --out frames.cbmvid
cbmvid --validate --out movie.cbmvid
cbmvid --in-cbmvid movie.cbmvid --gif-out preview.gif --rom-base <path-to-vice-roms>
```

The `.gif` output path runs each frame through a real C64 emulator and writes a looping animated GIF preview — requires `--rom-base` pointing at a VICE ROM directory (e.g. `external/vice-sharp/native/vice/vice/data` in a CbmEngine checkout).

Video ingest (`--in-video`) requires `ffmpeg` on `PATH` (or pass `--ffmpeg <path>`). Animated GIFs use a native ImageSharp decoder; no external dependency.
