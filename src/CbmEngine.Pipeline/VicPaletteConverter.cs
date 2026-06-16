using SixLabors.ImageSharp.PixelFormats;

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
}
