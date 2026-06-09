using ViceSharp.Chips.VicIi;

namespace CbmEngine.Systems.Boot;

public static class PaletteAssertions
{
    public static int CountPixelsOfIndex(ReadOnlySpan<byte> bgra, int width, int height, int paletteIndex, int yMin, int yMax)
    {
        if (bgra.Length != width * height * 4)
            throw new ArgumentException($"BGRA buffer length {bgra.Length} != width*height*4.", nameof(bgra));
        if ((paletteIndex & ~0x0F) != 0)
            throw new ArgumentOutOfRangeException(nameof(paletteIndex), "Palette index must be 0..15.");
        if (yMin < 0 || yMax >= height || yMin > yMax)
            throw new ArgumentOutOfRangeException(nameof(yMin), $"Band [{yMin},{yMax}] invalid for height {height}.");

        var color = VicPalette.Colors[paletteIndex];
        int count = 0;
        for (int y = yMin; y <= yMax; y++)
        {
            int rowStart = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                int o = rowStart + x * 4;
                if (bgra[o] == color.B && bgra[o + 1] == color.G && bgra[o + 2] == color.R)
                    count++;
            }
        }
        return count;
    }
}
