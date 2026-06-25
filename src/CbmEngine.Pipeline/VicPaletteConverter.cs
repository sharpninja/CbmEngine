using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CbmEngine.Pipeline;

/// <summary>
/// CBMFR-011: snap an arbitrary RGB or RGBA color to the nearest VIC-II palette index. The
/// distance metric is sum-of-squared RGB channel differences (Euclidean^2 in linear RGB), which is
/// adequate for the 16-entry C64 palette where colors are well separated; perceptual color spaces
/// (HSL, LAB) add cost without changing the picks materially.
///
/// Callers can restrict the selection to a curated subset of palette indices (the "allowed" span)
/// to bias the result toward saturated hues or specific colors. Empty subset is treated as
/// "no restriction" so it is safe to pass <see cref="ReadOnlySpan{T}.Empty"/> at the call site.
/// Invalid indices (above 15) in a non-empty subset throw <see cref="ArgumentOutOfRangeException"/>.
/// </summary>
public static class VicPaletteConverter
{
    private const int PaletteSize = 16;

    /// <summary>Snap an RGB color to the nearest VIC palette index (0-15).</summary>
    public static byte NearestIndex(byte r, byte g, byte b)
        => NearestIndex(r, g, b, ReadOnlySpan<byte>.Empty);

    /// <summary>Snap an <see cref="Rgba32"/> color to the nearest VIC palette index (alpha is ignored).</summary>
    public static byte NearestIndex(Rgba32 color)
        => NearestIndex(color.R, color.G, color.B, ReadOnlySpan<byte>.Empty);

    /// <summary>
    /// Snap an RGB color to the nearest VIC palette index, restricted to <paramref name="allowedIndices"/>.
    /// An empty span falls back to the full 16-entry palette. Throws if any index in the span is
    /// out of range (must be 0-15).
    /// </summary>
    public static byte NearestIndex(byte r, byte g, byte b, ReadOnlySpan<byte> allowedIndices)
    {
        if (allowedIndices.IsEmpty)
        {
            return NearestFromFullPalette(r, g, b);
        }
        ValidateIndices(allowedIndices);
        return NearestFromSubset(r, g, b, allowedIndices);
    }

    /// <summary>
    /// Snap an <see cref="Rgba32"/> color to the nearest VIC palette index, restricted to
    /// <paramref name="allowedIndices"/>. Alpha is ignored.
    /// </summary>
    public static byte NearestIndex(Rgba32 color, ReadOnlySpan<byte> allowedIndices)
        => NearestIndex(color.R, color.G, color.B, allowedIndices);

    private static byte NearestFromFullPalette(byte r, byte g, byte b)
    {
        byte best = 0;
        var bestDist = int.MaxValue;
        for (var i = 0; i < PaletteSize; i++)
        {
            var d = SquaredDistance(r, g, b, VicPalette.Colors[i]);
            if (d < bestDist)
            {
                bestDist = d;
                best = (byte)i;
            }
        }
        return best;
    }

    private static byte NearestFromSubset(byte r, byte g, byte b, ReadOnlySpan<byte> allowed)
    {
        var best = allowed[0];
        var bestDist = int.MaxValue;
        foreach (var i in allowed)
        {
            var d = SquaredDistance(r, g, b, VicPalette.Colors[i]);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }

    private static int SquaredDistance(byte r, byte g, byte b, VicPalette.Rgb p)
    {
        int dr = r - p.R, dg = g - p.G, db = b - p.B;
        return dr * dr + dg * dg + db * db;
    }

    private static void ValidateIndices(ReadOnlySpan<byte> indices)
    {
        foreach (var i in indices)
        {
            if (i >= PaletteSize)
            {
                throw new ArgumentOutOfRangeException(nameof(indices), i,
                    $"VIC palette index must be 0-{PaletteSize - 1}.");
            }
        }
    }

    /// <summary>
    /// Flatten the colors of <paramref name="source"/> to the nearest entries in the VIC-II palette,
    /// then fit the result into a 320x200 canvas while preserving the source aspect ratio. The fitted
    /// content is centered; any padding regions (pillarbox on the sides or letterbox on top/bottom)
    /// are filled with the exact RGB of the given <paramref name="backgroundIndex"/> (default 0 = black).
    /// The returned image is always exactly 320x200 and every pixel color is an exact member of
    /// <see cref="VicPalette.Colors"/>.
    /// The input image is not mutated.
    /// </summary>
    public static Image<Rgba32> ToC64Frame(Image<Rgba32> source, byte backgroundIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (backgroundIndex >= PaletteSize)
            throw new ArgumentOutOfRangeException(nameof(backgroundIndex), backgroundIndex,
                $"VIC palette index must be 0-{PaletteSize - 1}.");

        var pixels = new Rgba32[320 * 200];
        RenderToC64Frame(pixels, source, backgroundIndex);

        var img = new Image<Rgba32>(320, 200);
        img.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < 200; y++)
                pixels.AsSpan(y * 320, 320).CopyTo(acc.GetRowSpan(y));
        });
        return img;
    }

    /// <summary>
    /// Load the image from <paramref name="imagePath"/>, then <see cref="ToC64Frame(Image{Rgba32},byte)"/>.
    /// </summary>
    public static Image<Rgba32> ToC64Frame(string imagePath, byte backgroundIndex = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        using var src = Image.Load<Rgba32>(imagePath);
        return ToC64Frame(src, backgroundIndex);
    }

    /// <summary>
    /// Same as <see cref="ToC64Frame(Image{Rgba32},byte)"/> but returns the flattened 320x200
    /// result directly as a row-major <see cref="Rgba32"/> array (the "bitmap" form).
    /// Useful for callers that prefer to work with raw pixel buffers (e.g. the span overloads
    /// in <see cref="C64MulticolorBitmapEncoder"/>).
    /// </summary>
    public static Rgba32[] ToC64FramePixels(Image<Rgba32> source, byte backgroundIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (backgroundIndex >= PaletteSize)
            throw new ArgumentOutOfRangeException(nameof(backgroundIndex), backgroundIndex,
                $"VIC palette index must be 0-{PaletteSize - 1}.");

        var pixels = new Rgba32[320 * 200];
        RenderToC64Frame(pixels, source, backgroundIndex);
        return pixels;
    }

    /// <summary>
    /// Load from path then return the pixel array form.
    /// </summary>
    public static Rgba32[] ToC64FramePixels(string imagePath, byte backgroundIndex = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        using var src = Image.Load<Rgba32>(imagePath);
        return ToC64FramePixels(src, backgroundIndex);
    }

    /// <summary>
    /// Core implementation: renders the AR-fitted, centered, VIC-flattened 320x200 frame
    /// (with bg padding) directly into the provided destination span (must be exactly 320*200 long).
    /// Both the Image and raw-pixels public APIs delegate here.
    /// </summary>
    private static void RenderToC64Frame(Span<Rgba32> dest, Image<Rgba32> source, byte backgroundIndex)
    {
        const int targetW = 320;
        const int targetH = 200;

        if (dest.Length != targetW * targetH)
            throw new ArgumentException($"dest must be exactly {targetW * targetH} elements.", nameof(dest));

        var bg = VicPalette.Colors[backgroundIndex];
        var bgPixel = new Rgba32(bg.R, bg.G, bg.B, 255);
        dest.Fill(bgPixel);

        if (source.Width <= 0 || source.Height <= 0)
            return;

        double scale = Math.Min(targetW / (double)source.Width, targetH / (double)source.Height);
        int cw = Math.Max(1, (int)Math.Round(source.Width * scale));
        int ch = Math.Max(1, (int)Math.Round(source.Height * scale));
        cw = Math.Min(cw, targetW);
        ch = Math.Min(ch, targetH);

        int ox = (targetW - cw) / 2;
        int oy = (targetH - ch) / 2;

        using var scaled = source.Clone(ctx =>
            ctx.Resize(cw, ch, KnownResamplers.Lanczos3));

        // Read scaled pixels into a regular managed array first (avoids capturing ref-like Span<Rgba32>
        // inside the ProcessPixelRows lambda).
        var content = new Rgba32[cw * ch];
        scaled.ProcessPixelRows(sAcc =>
        {
            for (int sy = 0; sy < ch; sy++)
            {
                var srow = sAcc.GetRowSpan(sy);
                srow.CopyTo(content.AsSpan(sy * cw, cw));
            }
        });

        for (int sy = 0; sy < ch; sy++)
        {
            for (int sx = 0; sx < cw; sx++)
            {
                var p = content[sy * cw + sx];
                byte idx = NearestIndex(p.R, p.G, p.B);
                var vc = VicPalette.Colors[idx];
                dest[(oy + sy) * targetW + (ox + sx)] = new Rgba32(vc.R, vc.G, vc.B, 255);
            }
        }
    }
}
