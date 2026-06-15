using System.Diagnostics;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace CbmEngine.Pipeline.CbmVid;

public sealed record CbmVidEncodeManifest(
    string OutputPath,
    IReadOnlyList<CbmVidEncodeManifest.Entry> Frames,
    ushort FrameRate = 50,
    CbmVidFrameMode DefaultMode = CbmVidFrameMode.Multicolor,
    byte Flags = 0,
    bool StrictPalette = true)
{
    public sealed record Entry(string PngPath, CbmVidFrameMode? ModeOverride = null, byte? ForcedBackground = null);
}

public static class CbmVidEncoder
{
    public static void EncodeAnimatedGif(
        string gifPath,
        string outputPath,
        CbmVidFrameMode defaultMode = CbmVidFrameMode.Multicolor,
        ushort? overrideFrameRate = null,
        Action<string>? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gifPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (!File.Exists(gifPath)) throw new FileNotFoundException($"GIF not found: {gifPath}", gifPath);
        log ??= _ => { };

        using var src = Image.Load<Rgba32>(gifPath);
        if (src.Frames.Count == 0) throw new CbmVidEncodeException(-1, gifPath, "Animated GIF contains no frames.");
        log($"Loaded {gifPath}: {src.Width}x{src.Height}, {src.Frames.Count} frame(s)");

        // Per-frame delays (centiseconds). Falls back to 10 cs (10 fps) when GIF has no metadata.
        int firstDelayCs = 0;
        for (int i = 0; i < src.Frames.Count; i++)
        {
            var meta = src.Frames[i].Metadata.GetGifMetadata();
            int d = meta.FrameDelay > 0 ? meta.FrameDelay : 10;
            if (i == 0) firstDelayCs = d;
        }
        ushort effectiveFps = overrideFrameRate ?? (ushort)Math.Clamp((int)Math.Round(100.0 / Math.Max(1, firstDelayCs)), 1, 60);
        log($"Effective frame rate: {effectiveFps} Hz (first-frame delay {firstDelayCs} cs; override={overrideFrameRate?.ToString() ?? "none"})");

        var paletteColors = new SixLabors.ImageSharp.Color[16];
        for (int i = 0; i < 16; i++)
        {
            var c = VicPalette.Colors[i];
            paletteColors[i] = SixLabors.ImageSharp.Color.FromRgb(c.R, c.G, c.B);
        }
        var quantizer = new PaletteQuantizer(paletteColors, new QuantizerOptions { Dither = KnownDitherings.FloydSteinberg, DitherScale = 1.0f });

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        using var fs = File.Create(outputPath);
        var header = new CbmVidHeader(320, 200, effectiveFps, (uint)src.Frames.Count, defaultMode, Flags: 1);
        using var writer = new CbmVidWriter(fs, header, leaveOpen: true);

        string scratchDir = Path.Combine(Path.GetTempPath(), $"cbmvid-gif-{Guid.NewGuid():N}");
        Directory.CreateDirectory(scratchDir);
        try
        {
            for (int i = 0; i < src.Frames.Count; i++)
            {
                using var frame = src.Frames.CloneFrame(i);
                frame.Mutate(ctx =>
                {
                    ctx.Resize(new ResizeOptions { Size = new Size(320, 200), Mode = ResizeMode.Stretch, Sampler = KnownResamplers.Lanczos3 });
                    ctx.Quantize(quantizer);
                });
                var tmpPng = Path.Combine(scratchDir, $"frame_{i:D4}.png");
                frame.SaveAsPng(tmpPng);
                EncodedSplashBitmap encoded = defaultMode == CbmVidFrameMode.HiRes
                    ? C64MulticolorBitmapEncoder.EncodeHiRes(tmpPng)
                    : C64MulticolorBitmapEncoder.Encode(tmpPng, forceBackgroundColor: null);
                writer.WriteFrame(encoded);
                if ((i + 1) % 20 == 0) log($"  encoded {i + 1}/{src.Frames.Count}");
            }
            writer.FinalizeFrameCount();
            log($"Wrote {outputPath} ({src.Frames.Count} frames, {effectiveFps} fps)");
        }
        finally
        {
            try { Directory.Delete(scratchDir, recursive: true); } catch { }
        }
    }

    public static void EncodeVideo(
        string videoPath,
        string outputPath,
        ushort frameRate = 50,
        CbmVidFrameMode defaultMode = CbmVidFrameMode.Multicolor,
        string? ffmpegPath = null,
        string? scratchDirectory = null,
        bool keepIntermediateFrames = false,
        Action<string>? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (!File.Exists(videoPath)) throw new FileNotFoundException($"Video not found: {videoPath}", videoPath);

        var ffmpeg = ResolveFfmpeg(ffmpegPath);
        log ??= _ => { };

        var scratch = scratchDirectory ?? Path.Combine(Path.GetTempPath(), $"cbmvid-ingest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(scratch);
        var palettePath = Path.Combine(scratch, "vic-palette.png");
        VicPalette.WritePaletteImage(palettePath);

        try
        {
            // Extract frames at target fps, scaled to 320x200, snapped to VIC palette via paletteuse.
            string filter = $"[0:v]fps={frameRate},scale=320:200:flags=lanczos[scl];[scl][1:v]paletteuse=dither=floyd_steinberg";
            string outPattern = Path.Combine(scratch, "frame_%04d.png");
            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-y");
            psi.ArgumentList.Add("-i"); psi.ArgumentList.Add(videoPath);
            psi.ArgumentList.Add("-i"); psi.ArgumentList.Add(palettePath);
            psi.ArgumentList.Add("-lavfi"); psi.ArgumentList.Add(filter);
            psi.ArgumentList.Add(outPattern);

            log($"ffmpeg {string.Join(' ', psi.ArgumentList)}");
            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start ffmpeg.");
            var stderr = new StringBuilder();
            proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
            proc.BeginErrorReadLine();
            proc.WaitForExit();
            if (proc.ExitCode != 0)
                throw new CbmVidEncodeException(-1, videoPath, $"ffmpeg exited with {proc.ExitCode}.\n{stderr}");

            var frames = Directory.EnumerateFiles(scratch, "frame_*.png", SearchOption.TopDirectoryOnly).ToArray();
            if (frames.Length == 0)
                throw new CbmVidEncodeException(-1, videoPath, $"ffmpeg produced no frames. stderr:\n{stderr}");

            log($"ffmpeg extracted {frames.Length} frame(s) into {scratch}");
            EncodeDirectory(scratch, outputPath, frameRate, defaultMode);
        }
        finally
        {
            if (!keepIntermediateFrames)
            {
                try { Directory.Delete(scratch, recursive: true); }
                catch (Exception ex) { log($"WARN  could not delete scratch dir {scratch}: {ex.Message}"); }
            }
        }
    }

    private static string ResolveFfmpeg(string? hint)
    {
        if (!string.IsNullOrWhiteSpace(hint))
        {
            if (File.Exists(hint)) return hint;
            throw new FileNotFoundException($"ffmpeg not found at: {hint}", hint);
        }
        var pathExt = Environment.OSVersion.Platform == PlatformID.Win32NT ? ".exe" : string.Empty;
        var name = "ffmpeg" + pathExt;
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
        foreach (var dir in paths)
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        throw new FileNotFoundException("ffmpeg not found on PATH. Install ffmpeg (https://ffmpeg.org/) or pass ffmpegPath explicitly.");
    }

    public static void EncodeDirectory(
        string pngDirectory,
        string outputPath,
        ushort frameRate = 50,
        CbmVidFrameMode defaultMode = CbmVidFrameMode.Multicolor)
    {
        var files = Directory.EnumerateFiles(pngDirectory, "frame_*.png", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(p => new CbmVidEncodeManifest.Entry(p))
            .ToArray();
        if (files.Length == 0) throw new InvalidOperationException($"No frame_*.png files found in {pngDirectory}.");
        Encode(new CbmVidEncodeManifest(outputPath, files, frameRate, defaultMode));
    }

    public static void Encode(CbmVidEncodeManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.Frames.Count == 0) throw new ArgumentException("Manifest must contain at least one frame.", nameof(manifest));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(manifest.OutputPath))!);
        using var fs = File.Create(manifest.OutputPath);
        var header = new CbmVidHeader(320, 200, manifest.FrameRate, (uint)manifest.Frames.Count, manifest.DefaultMode, manifest.Flags);
        using var writer = new CbmVidWriter(fs, header, leaveOpen: true);
        for (int i = 0; i < manifest.Frames.Count; i++)
        {
            var entry = manifest.Frames[i];
            if (manifest.StrictPalette) ValidatePalette(entry.PngPath, i);
            var mode = entry.ModeOverride ?? manifest.DefaultMode;
            EncodedSplashBitmap encoded;
            try
            {
                encoded = mode == CbmVidFrameMode.HiRes
                    ? C64MulticolorBitmapEncoder.EncodeHiRes(entry.PngPath)
                    : C64MulticolorBitmapEncoder.Encode(entry.PngPath, entry.ForcedBackground ?? (byte?)null);
            }
            catch (Exception ex) when (ex is not CbmVidEncodeException)
            {
                throw new CbmVidEncodeException(i, entry.PngPath, $"frame={i} encode failed: {ex.Message}", ex);
            }
            writer.WriteFrame(encoded);
        }
        writer.FinalizeFrameCount();
    }

    private static void ValidatePalette(string pngPath, int frameIndex)
    {
        using var image = Image.Load<Rgba32>(pngPath);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    var p = row[x];
                    if (!VicPalette.TryExact(p.R, p.G, p.B, out _))
                    {
                        throw new CbmVidEncodeException(
                            frameIndex,
                            pngPath,
                            $"frame={frameIndex} {Path.GetFileName(pngPath)}: out-of-palette pixel at x={x}, y={y} #{p.R:X2}{p.G:X2}{p.B:X2}");
                    }
                }
            }
        });
    }
}
