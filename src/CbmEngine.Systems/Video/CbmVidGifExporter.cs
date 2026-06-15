using CbmEngine.Pipeline;
using CbmEngine.Pipeline.CbmVid;
using CbmEngine.Systems.Cartridge;
using CbmEngine.Systems.Strategy;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using ViceSharp.Abstractions;

namespace CbmEngine.Systems.Video;

public static class CbmVidGifExporter
{
    public static void Export(string cbmvidPath, string gifPath, IRomProvider roms, Action<string>? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cbmvidPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(gifPath);
        ArgumentNullException.ThrowIfNull(roms);

        using var stream = File.OpenRead(cbmvidPath);
        Export(stream, gifPath, roms, log);
    }

    public static void Export(Stream cbmvidStream, string gifPath, IRomProvider roms, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(cbmvidStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(gifPath);
        ArgumentNullException.ThrowIfNull(roms);
        log ??= _ => { };

        using var player = new VideoPlayer(cbmvidStream, leaveOpen: true);
        var header = player.Header;
        log($"Loaded .cbmvid: {header.FrameCount} frames, fps={header.FrameRate}, defaultMode={header.DefaultMode}");

        var sys = CommodoreSystem.Build("c64", roms);
        var splash = player.PeekFrame(0);
        var cart = BitmapPlayerCart.Build(splash);
        var boot = CartridgeBoot.AttachAndWaitForMarker(sys, cart, maxFrames: 600);
        if (!boot.MarkerSeen) throw new InvalidOperationException($"BitmapPlayerCart did not boot within 600 frames (saw at {boot.FramesUntilMarker}).");
        log($"Cart booted after {boot.FramesUntilMarker} frames; rendering...");

        int w = sys.VideoChip.FrameWidth;
        int h = sys.VideoChip.FrameHeight;
        // GIF frame delay is in centiseconds; clamp to >= 2 (50fps) for compatibility.
        int delayCs = Math.Max(2, (int)Math.Round(100.0 / Math.Max(1, (int)header.FrameRate)));

        using var gifImage = new Image<Rgba32>(w, h);
        var pal = new Rgba32[16];
        for (int i = 0; i < 16; i++) pal[i] = new Rgba32(VicPalette.Colors[i].R, VicPalette.Colors[i].G, VicPalette.Colors[i].B, 255);

        player.Reset();
        bool firstFrame = true;
        int rendered = 0;
        while (!player.IsFinished)
        {
            if (!player.PumpFrame(sys.Memory)) break;
            sys.RunFrame();
            sys.RunFrame();   // second pass to settle the bitmap latch
            CopyFrameBufferToImage(sys.VideoChip.FrameBuffer, w, h, gifImage, firstFrame, delayCs);
            firstFrame = false;
            rendered++;
            if (rendered % 50 == 0) log($"  rendered {rendered}/{header.FrameCount} frames...");
        }

        var gifMeta = gifImage.Metadata.GetGifMetadata();
        gifMeta.RepeatCount = 0;   // 0 = loop forever
        log($"Writing {gifPath} ({rendered} frames, {delayCs}cs delay each)...");
        var dir = Path.GetDirectoryName(Path.GetFullPath(gifPath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        gifImage.SaveAsGif(gifPath, new GifEncoder());
        log($"Done: {new FileInfo(gifPath).Length} B");
    }

    private static void CopyFrameBufferToImage(byte[] bgra, int w, int h, Image<Rgba32> dest, bool isFirstFrame, int delayCs)
    {
        Image<Rgba32>? frame = null;
        if (isFirstFrame)
        {
            dest.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < h; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    int rowStart = y * w * 4;
                    for (int x = 0; x < w; x++)
                    {
                        int i = rowStart + x * 4;
                        row[x] = new Rgba32(bgra[i + 2], bgra[i + 1], bgra[i], 255);
                    }
                }
            });
            var meta = dest.Frames.RootFrame.Metadata.GetGifMetadata();
            meta.FrameDelay = delayCs;
            meta.DisposalMethod = GifDisposalMethod.RestoreToBackground;
            return;
        }

        frame = new Image<Rgba32>(w, h);
        frame.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(y);
                int rowStart = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int i = rowStart + x * 4;
                    row[x] = new Rgba32(bgra[i + 2], bgra[i + 1], bgra[i], 255);
                }
            }
        });
        var added = dest.Frames.AddFrame(frame.Frames.RootFrame);
        var addedMeta = added.Metadata.GetGifMetadata();
        addedMeta.FrameDelay = delayCs;
        addedMeta.DisposalMethod = GifDisposalMethod.RestoreToBackground;
        frame.Dispose();
    }
}
