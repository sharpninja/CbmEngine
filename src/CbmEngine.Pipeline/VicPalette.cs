using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace CbmEngine.Pipeline;

public static class VicPalette
{
    public readonly record struct Rgb(byte R, byte G, byte B);

    public static readonly Rgb[] Colors =
    {
        new(0x00, 0x00, 0x00), new(0xFF, 0xFF, 0xFF), new(0x96, 0x28, 0x35), new(0x5B, 0xD6, 0xC1),
        new(0x9B, 0x27, 0xB1), new(0x5C, 0xB5, 0x32), new(0x1B, 0x1B, 0x8E), new(0xDF, 0xE5, 0x6C),
        new(0x9B, 0x52, 0x1C), new(0x5A, 0x33, 0x00), new(0xDA, 0x46, 0x44), new(0x44, 0x44, 0x44),
        new(0x77, 0x77, 0x77), new(0xAD, 0xFF, 0x6C), new(0x6B, 0x5E, 0xD1), new(0xAA, 0xAA, 0xAA),
    };

    public static bool TryExact(byte r, byte g, byte b, out int index)
    {
        for (int i = 0; i < 16; i++)
            if (Colors[i].R == r && Colors[i].G == g && Colors[i].B == b)
            {
                index = i;
                return true;
            }
        index = -1;
        return false;
    }

    public static void WritePaletteImage(string outPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outPath);
        // ffmpeg's paletteuse filter requires exactly 256 pixels: each cell occupies a 4x4 block in a 16x16 image.
        using var img = new Image<Rgba32>(16, 16);
        for (int i = 0; i < 256; i++)
        {
            int colorIdx = i % 16;
            var c = Colors[colorIdx];
            img[i % 16, i / 16] = new Rgba32(c.R, c.G, c.B, 255);
        }
        var dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        img.SaveAsPng(outPath, new PngEncoder());
    }
}
