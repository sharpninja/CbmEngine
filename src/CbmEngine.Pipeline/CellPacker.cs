namespace CbmEngine.Pipeline;

public static class CellPacker
{
    public readonly record struct PackedCell(CharMode Mode, byte[] Bytes, byte Ink, int BackgroundIndex);

    public static PackedCell PackHiRes(ReadOnlySpan<int> palettePixels8x8, int backgroundIndex, int? sampleX = null, int? sampleY = null)
    {
        EnsureSize(palettePixels8x8);
        int ink = -1;
        for (int i = 0; i < 64; i++)
        {
            int p = palettePixels8x8[i];
            if (p == backgroundIndex) continue;
            if (ink == -1) ink = p;
            else if (ink != p)
                throw new ContentProcessingException(
                    $"Cell at sample (x={sampleX},y={sampleY}) requires hi-res but uses >1 non-background color (background=#{backgroundIndex:X}, found ink #{ink:X} and #{p:X}).");
        }
        var bytes = new byte[8];
        for (int y = 0; y < 8; y++)
        {
            byte row = 0;
            for (int x = 0; x < 8; x++)
                if (palettePixels8x8[y * 8 + x] != backgroundIndex) row |= (byte)(0x80 >> x);
            bytes[y] = row;
        }
        return new PackedCell(CharMode.HiRes, bytes, (byte)(ink < 0 ? 0 : ink), backgroundIndex);
    }

    public static PackedCell PackMulticolor(ReadOnlySpan<int> palettePixels8x8, ScreenColorConfig screen, int? sampleX = null, int? sampleY = null)
    {
        EnsureSize(palettePixels8x8);
        int bg = screen.BackgroundD021;
        int mc1 = screen.McBackgroundD022;
        int mc2 = screen.McBorderD023;
        int ink = -1;
        for (int i = 0; i < 64; i += 2)
        {
            int p = palettePixels8x8[i];
            if (p == bg || p == mc1 || p == mc2) continue;
            if (ink == -1) ink = p;
            else if (ink != p)
                throw new ContentProcessingException(
                    $"Multicolor cell at (x={sampleX},y={sampleY}) uses >1 ink color beyond shared $D021/$D022/$D023 (bg={bg:X} mc1={mc1:X} mc2={mc2:X} found #{ink:X} and #{p:X}).");
        }
        if (ink == -1) ink = 0;
        if (ink >= 8)
            throw new ContentProcessingException($"Multicolor cell ink color {ink} >= 8; multicolor inks must be 0..7 (bit 3 reserved for mode).");

        var bytes = new byte[8];
        for (int y = 0; y < 8; y++)
        {
            byte row = 0;
            for (int pair = 0; pair < 4; pair++)
            {
                int p = palettePixels8x8[y * 8 + pair * 2];
                byte code = p == mc1 ? (byte)0b01
                    : p == mc2 ? (byte)0b10
                    : p == ink ? (byte)0b11
                    : (byte)0b00;
                row |= (byte)(code << (6 - pair * 2));
            }
            bytes[y] = row;
        }
        return new PackedCell(CharMode.Multicolor, bytes, (byte)ink, bg);
    }

    public static CharMode InferMode(ReadOnlySpan<int> palettePixels8x8, int backgroundIndex)
    {
        int nonBg = -1;
        for (int i = 0; i < 64; i++)
        {
            int p = palettePixels8x8[i];
            if (p == backgroundIndex) continue;
            if (nonBg == -1) nonBg = p;
            else if (p != nonBg) return CharMode.Multicolor;
        }
        return CharMode.HiRes;
    }

    private static void EnsureSize(ReadOnlySpan<int> pixels)
    {
        if (pixels.Length != 64) throw new ArgumentException("Expected 64 pixels (8x8).", nameof(pixels));
    }
}
