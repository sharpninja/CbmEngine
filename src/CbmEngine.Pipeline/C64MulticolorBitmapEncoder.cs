using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CbmEngine.Pipeline;

public enum SplashBitmapMode { Multicolor, HiRes }

public sealed record EncodedSplashBitmap(SplashBitmapMode Mode, byte BackgroundColorIndex, byte[] Bitmap, byte[] ScreenRam, byte[] ColorRam)
{
    public const int BitmapByteSize = 8000;
    public const int ScreenRamSize = 1000;
    public const int ColorRamSize = 1000;
}

public static class C64MulticolorBitmapEncoder
{
    private const int CellCols = 40;
    private const int CellRows = 25;
    private const int CellPixelWidth = 4;
    private const int CellPixelHeight = 8;
    private const int Width = CellCols * CellPixelWidth;
    private const int Height = CellRows * CellPixelHeight;

    public static EncodedSplashBitmap EncodeHiRes(string pngPath, string? debugDecodedPngPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pngPath);
        using var image = Image.Load<Rgba32>(pngPath);
        if (image.Width != 320 || image.Height != 200)
            throw new ArgumentException($"Splash image must be 320x200; got {image.Width}x{image.Height}.", nameof(pngPath));

        const int hrWidth = 320;
        const int hrHeight = 200;
        var pal = new int[hrWidth, hrHeight];
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < hrHeight; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < hrWidth; x++)
                    pal[x, y] = NearestPaletteIndex(row[x].R, row[x].G, row[x].B);
            }
        });

        var bitmap = new byte[EncodedSplashBitmap.BitmapByteSize];
        var screen = new byte[EncodedSplashBitmap.ScreenRamSize];
        var color = new byte[EncodedSplashBitmap.ColorRamSize];

        for (int cellY = 0; cellY < CellRows; cellY++)
        {
            for (int cellX = 0; cellX < CellCols; cellX++)
            {
                int cellIdx = cellY * CellCols + cellX;
                var cellHist = new int[16];
                for (int py = 0; py < 8; py++)
                    for (int px = 0; px < 8; px++)
                        cellHist[pal[cellX * 8 + px, cellY * 8 + py]]++;

                var ordered = Enumerable.Range(0, 16).OrderByDescending(i => cellHist[i]).ToArray();
                byte fg = (byte)ordered[0];
                byte bg = cellHist[ordered[1]] > 0 ? (byte)ordered[1] : fg;

                screen[cellIdx] = (byte)((fg << 4) | (bg & 0x0F));
                color[cellIdx] = 0;

                for (int py = 0; py < 8; py++)
                {
                    byte b = 0;
                    for (int px = 0; px < 8; px++)
                    {
                        int p = pal[cellX * 8 + px, cellY * 8 + py];
                        bool isFg = p == fg || (p != bg && ColorDistance(p, fg) <= ColorDistance(p, bg));
                        if (isFg) b |= (byte)(0x80 >> px);
                    }
                    bitmap[cellIdx * 8 + py] = b;
                }
            }
        }

        var encoded = new EncodedSplashBitmap(SplashBitmapMode.HiRes, 0, bitmap, screen, color);
        if (debugDecodedPngPath is not null) WriteDebugPreview(encoded, debugDecodedPngPath);
        return encoded;
    }

    private static int ColorDistance(int paletteA, int paletteB)
    {
        var a = VicPalette.Colors[paletteA];
        var b = VicPalette.Colors[paletteB];
        int dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
        return dr * dr + dg * dg + db * db;
    }

    public static EncodedSplashBitmap Encode(string pngPath, byte? forceBackgroundColor = null, string? debugDecodedPngPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pngPath);
        using var image = Image.Load<Rgba32>(pngPath);
        return Encode(image, forceBackgroundColor, debugDecodedPngPath);
    }

    /// <summary>
    /// In-memory multicolor encode of a 320x200 image (the multicolor sampler reads every second
    /// column, so the effective resolution is 160x200). Lets callers encode rendered frames without
    /// round-tripping through a PNG file. Delegates to the raw-span overload.
    /// </summary>
    public static EncodedSplashBitmap Encode(Image<Rgba32> image, byte? forceBackgroundColor = null, string? debugDecodedPngPath = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width != 320 || image.Height != 200)
            throw new ArgumentException($"Splash image must be 320x200; got {image.Width}x{image.Height}.", nameof(image));

        var pixels = new Rgba32[320 * 200];
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < 200; y++)
                accessor.GetRowSpan(y).CopyTo(pixels.AsSpan(y * 320, 320));
        });

        var encoded = Encode((ReadOnlySpan<Rgba32>)pixels, 320, 200, forceBackgroundColor);
        if (debugDecodedPngPath is not null)
            WriteDebugPreview(encoded, debugDecodedPngPath);
        return encoded;
    }

    /// <summary>
    /// In-memory multicolor encode from a raw row-major 320x200 <see cref="Rgba32"/> span (the
    /// multicolor sampler reads every second column, so the effective resolution is 160x200). Lets
    /// callers encode rendered frames without depending on an ImageSharp <see cref="Image{TPixel}"/>.
    /// </summary>
    public static EncodedSplashBitmap Encode(ReadOnlySpan<Rgba32> pixels, int width, int height, byte? forceBackgroundColor = null)
    {
        if (width != 320)
            throw new ArgumentException($"Splash image width must be 320; got {width}.", nameof(width));
        if (height != 200)
            throw new ArgumentException($"Splash image height must be 200; got {height}.", nameof(height));
        if (pixels.Length < width * height)
            throw new ArgumentException($"pixels must contain at least {width * height} entries; got {pixels.Length}.", nameof(pixels));

        var pal = new int[Width, Height];
        var globalHist = new int[16];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var p = pixels[y * width + x * 2];
                int idx = NearestPaletteIndex(p.R, p.G, p.B);
                pal[x, y] = idx;
                globalHist[idx]++;
            }
        }

        byte bg = forceBackgroundColor ?? PickBackground(globalHist);

        var bitmap = new byte[EncodedSplashBitmap.BitmapByteSize];
        var screen = new byte[EncodedSplashBitmap.ScreenRamSize];
        var color = new byte[EncodedSplashBitmap.ColorRamSize];

        for (int cellY = 0; cellY < CellRows; cellY++)
        {
            for (int cellX = 0; cellX < CellCols; cellX++)
            {
                int cellIdx = cellY * CellCols + cellX;
                var cellHist = new int[16];
                for (int py = 0; py < CellPixelHeight; py++)
                    for (int px = 0; px < CellPixelWidth; px++)
                        cellHist[pal[cellX * CellPixelWidth + px, cellY * CellPixelHeight + py]]++;

                cellHist[bg] = 0;
                var ordered = Enumerable.Range(0, 16).OrderByDescending(i => cellHist[i]).ToArray();
                byte c01 = (byte)ordered[0];
                byte c10 = cellHist[ordered[1]] > 0 ? (byte)ordered[1] : c01;
                byte c11 = cellHist[ordered[2]] > 0 ? (byte)ordered[2] : c01;

                screen[cellIdx] = (byte)((c01 << 4) | (c10 & 0x0F));
                color[cellIdx] = (byte)(c11 & 0x0F);

                for (int py = 0; py < CellPixelHeight; py++)
                {
                    byte bmp = 0;
                    for (int px = 0; px < CellPixelWidth; px++)
                    {
                        int p = pal[cellX * CellPixelWidth + px, cellY * CellPixelHeight + py];
                        int code = p == bg ? 0
                            : p == c01 ? 1
                            : p == c10 ? 2
                            : p == c11 ? 3
                            : NearestCode(p, bg, c01, c10, c11);
                        bmp |= (byte)(code << (6 - px * 2));
                    }
                    bitmap[cellIdx * CellPixelHeight + py] = bmp;
                }
            }
        }

        return new EncodedSplashBitmap(SplashBitmapMode.Multicolor, bg, bitmap, screen, color);
    }

    public static void WriteDebugPreview(EncodedSplashBitmap encoded, string outPath)
    {
        using var img = new Image<Rgba32>(320, Height);
        var paletteRgb = new Rgba32[16];
        for (int i = 0; i < 16; i++) paletteRgb[i] = new Rgba32(VicPalette.Colors[i].R, VicPalette.Colors[i].G, VicPalette.Colors[i].B);

        for (int cellY = 0; cellY < CellRows; cellY++)
        {
            for (int cellX = 0; cellX < CellCols; cellX++)
            {
                int cellIdx = cellY * CellCols + cellX;
                byte screen = encoded.ScreenRam[cellIdx];
                byte col = encoded.ColorRam[cellIdx];

                if (encoded.Mode == SplashBitmapMode.HiRes)
                {
                    int fg = (screen >> 4) & 0x0F;
                    int bg = screen & 0x0F;
                    for (int py = 0; py < 8; py++)
                    {
                        byte b = encoded.Bitmap[cellIdx * 8 + py];
                        for (int px = 0; px < 8; px++)
                        {
                            int p = (b & (0x80 >> px)) != 0 ? fg : bg;
                            img[cellX * 8 + px, cellY * 8 + py] = paletteRgb[p];
                        }
                    }
                }
                else
                {
                    int c01 = (screen >> 4) & 0x0F;
                    int c10 = screen & 0x0F;
                    int c11 = col & 0x0F;
                    for (int py = 0; py < CellPixelHeight; py++)
                    {
                        byte b = encoded.Bitmap[cellIdx * CellPixelHeight + py];
                        for (int px = 0; px < CellPixelWidth; px++)
                        {
                            int code = (b >> (6 - px * 2)) & 0x03;
                            int p = code switch { 0 => encoded.BackgroundColorIndex, 1 => c01, 2 => c10, _ => c11 };
                            var rgba = paletteRgb[p];
                            img[cellX * CellPixelWidth * 2 + px * 2, cellY * CellPixelHeight + py] = rgba;
                            img[cellX * CellPixelWidth * 2 + px * 2 + 1, cellY * CellPixelHeight + py] = rgba;
                        }
                    }
                }
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
        img.SaveAsPng(outPath);
    }

    private static byte PickBackground(int[] histogram)
    {
        int best = 0;
        for (int i = 1; i < histogram.Length; i++)
            if (histogram[i] > histogram[best]) best = i;
        return (byte)best;
    }

    private static int NearestPaletteIndex(byte r, byte g, byte b)
    {
        int best = 0; int bestDist = int.MaxValue;
        for (int i = 0; i < 16; i++)
        {
            var c = VicPalette.Colors[i];
            int dr = r - c.R, dg = g - c.G, db = b - c.B;
            int d = dr * dr + dg * dg + db * db;
            if (d < bestDist) { bestDist = d; best = i; }
        }
        if (best == 15 && r >= 0xA0 && g >= 0xA0 && b >= 0xA0 && Math.Abs(r - g) < 16 && Math.Abs(g - b) < 16)
        {
            int distToWhite = (255 - r) * (255 - r) + (255 - g) * (255 - g) + (255 - b) * (255 - b);
            if (distToWhite < 22500) return 1;
        }
        if (r <= 0x20 && g <= 0x20 && b <= 0x20) return 0;
        return best;
    }

    private static int NearestCode(int target, byte bg, byte c01, byte c10, byte c11)
    {
        var t = VicPalette.Colors[target];
        Span<int> dists = stackalloc int[4];
        Span<byte> candidates = stackalloc byte[4] { bg, c01, c10, c11 };
        for (int i = 0; i < 4; i++)
        {
            var c = VicPalette.Colors[candidates[i]];
            int dr = t.R - c.R, dg = t.G - c.G, db = t.B - c.B;
            dists[i] = dr * dr + dg * dg + db * db;
        }
        int best = 0;
        for (int i = 1; i < 4; i++) if (dists[i] < dists[best]) best = i;
        return best;
    }
}
