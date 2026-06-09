using CbmEngine.Abstractions;

namespace CbmEngine.Systems.Services;

public sealed class TilemapService
{
    public const int ScreenRamBase = 0x0400;
    public const int ColorRamBase = 0xD800;
    public const int Columns = 40;
    public const int Rows = 25;

    private readonly IMemoryService _memory;

    public TilemapService(IMemoryService memory)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
    }

    public void SetCell(int col, int row, byte glyph, byte color)
    {
        if ((uint)col >= Columns) throw new ArgumentOutOfRangeException(nameof(col));
        if ((uint)row >= Rows) throw new ArgumentOutOfRangeException(nameof(row));
        int offset = row * Columns + col;
        var screen = _memory.View((ushort)(ScreenRamBase + offset), 1);
        screen[0] = glyph;
        _memory.WriteIo((ushort)(ColorRamBase + offset), new[] { (byte)(color & 0x0F) });
    }

    public void Fill(byte glyph, byte color)
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
                SetCell(c, r, glyph, color);
    }

    public byte ReadGlyph(int col, int row)
    {
        if ((uint)col >= Columns || (uint)row >= Rows) throw new ArgumentOutOfRangeException();
        var span = _memory.SnapshotRange((ushort)(ScreenRamBase + row * Columns + col), 1);
        return span[0];
    }
}
