using SixLabors.ImageSharp.PixelFormats;

namespace CbmEngine.Pipeline;

/// <summary>
/// A paletted drawing surface for C64 multicolor bitmap mode. The logical resolution is 160x200
/// (each logical pixel is two physical C64 pixels wide, matching the multicolor sampler); each pixel
/// holds a VIC palette index 0-15. <see cref="Encode"/> feeds the result to the bitmap encoder, which
/// applies the 4-colors-per-4x8-cell reduction.
/// </summary>
public sealed class MulticolorCanvas
{
    private const int PhysicalWidth = 320;
    private readonly byte[] _pixels;

    public int Width { get; }
    public int Height { get; }

    public MulticolorCanvas(int logicalWidth = 160, int height = 200)
    {
        if (logicalWidth <= 0) throw new ArgumentOutOfRangeException(nameof(logicalWidth));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = logicalWidth;
        Height = height;
        _pixels = new byte[logicalWidth * height];
    }

    /// <summary>Set every pixel to <paramref name="colorIndex"/>.</summary>
    public void Clear(byte colorIndex) => Array.Fill(_pixels, (byte)(colorIndex & 0x0F));

    /// <summary>Get the palette index at (x,y). Out-of-bounds returns 0.</summary>
    public byte GetPixel(int x, int y) =>
        (uint)x < (uint)Width && (uint)y < (uint)Height ? _pixels[y * Width + x] : (byte)0;

    /// <summary>Set a pixel; out-of-bounds is ignored (no throw).</summary>
    public void SetPixel(int x, int y, byte colorIndex)
    {
        if ((uint)x < (uint)Width && (uint)y < (uint)Height)
            _pixels[y * Width + x] = (byte)(colorIndex & 0x0F);
    }

    /// <summary>Fill a rectangle, clipped to the canvas bounds.</summary>
    public void FillRect(int x, int y, int w, int h, byte colorIndex)
    {
        int x0 = Math.Max(0, x), y0 = Math.Max(0, y);
        int x1 = Math.Min(Width, x + w), y1 = Math.Min(Height, y + h);
        byte c = (byte)(colorIndex & 0x0F);
        for (int yy = y0; yy < y1; yy++)
            for (int xx = x0; xx < x1; xx++)
                _pixels[yy * Width + xx] = c;
    }

    /// <summary>Draw an 8x8 glyph (MSB = leftmost). Set bits become <paramref name="colorIndex"/>; clear bits are untouched.</summary>
    public void DrawGlyph(int x, int y, ReadOnlySpan<byte> glyph8x8, byte colorIndex)
    {
        int rows = Math.Min(8, glyph8x8.Length);
        for (int row = 0; row < rows; row++)
        {
            byte bits = glyph8x8[row];
            for (int col = 0; col < 8; col++)
                if ((bits & (0x80 >> col)) != 0)
                    SetPixel(x + col, y + row, colorIndex);
        }
    }

    /// <summary>Draw text using the bundled <see cref="CbmFont8x8"/>, advancing 8 logical px per char.</summary>
    public void DrawText(int x, int y, string text, byte colorIndex)
    {
        ArgumentNullException.ThrowIfNull(text);
        for (int i = 0; i < text.Length; i++)
            DrawGlyph(x + i * 8, y, CbmFont8x8.GetGlyph(text[i]), colorIndex);
    }

    /// <summary>Encode the canvas to a C64 multicolor bitmap via <see cref="C64MulticolorBitmapEncoder"/>.</summary>
    public EncodedSplashBitmap Encode(byte? forceBackgroundColor = null)
    {
        var pixels = new Rgba32[PhysicalWidth * Height];
        for (int y = 0; y < Height; y++)
        {
            for (int lx = 0; lx < Width && lx < 160; lx++)
            {
                var rgb = VicPalette.Colors[_pixels[y * Width + lx]];
                var rgba = new Rgba32(rgb.R, rgb.G, rgb.B, 255);
                int px = lx * 2;
                pixels[y * PhysicalWidth + px] = rgba;
                pixels[y * PhysicalWidth + px + 1] = rgba;
            }
        }
        return C64MulticolorBitmapEncoder.Encode((ReadOnlySpan<Rgba32>)pixels, PhysicalWidth, Height, forceBackgroundColor);
    }
}
