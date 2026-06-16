using CbmEngine.Abstractions;

namespace CbmEngine.Systems.Text;

/// <summary>
/// Writes char-mode text into screen RAM + colour RAM using C64 screen codes, for status/menu bars.
/// Colour RAM in the I/O page ($D000-$DFFF, which includes $D800) is routed through <c>WriteIo</c>;
/// otherwise colour is written with <c>WriteRange</c> (e.g. a shadow buffer).
/// </summary>
public static class TextService
{
    public const int Columns = 40;
    public const int Rows = 25;
    private const int CellCount = Columns * Rows; // 1000

    /// <summary>Write <paramref name="text"/> at (col,row) with the given colour.</summary>
    public static void Write(
        IMemoryService memory, ushort screenBase, ushort colorBase, int col, int row, string text, byte color)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(text);
        if (col is < 0 or >= Columns) throw new ArgumentOutOfRangeException(nameof(col), col, "col must be 0-39.");
        if (row is < 0 or >= Rows) throw new ArgumentOutOfRangeException(nameof(row), row, "row must be 0-24.");
        if (col + text.Length > Columns)
            throw new ArgumentException($"text of length {text.Length} at column {col} exceeds the {Columns}-column row.", nameof(text));

        int offset = row * Columns + col;
        Span<byte> codes = text.Length <= 256 ? stackalloc byte[text.Length] : new byte[text.Length];
        ScreenCode.Encode(text, codes);
        memory.WriteRange((ushort)(screenBase + offset), codes);

        Span<byte> colors = text.Length <= 256 ? stackalloc byte[text.Length] : new byte[text.Length];
        colors.Fill((byte)(color & 0x0F));
        WriteColor(memory, (ushort)(colorBase + offset), colors);
    }

    /// <summary>Fill the full 40x25 screen + colour region.</summary>
    public static void Clear(IMemoryService memory, ushort screenBase, ushort colorBase, byte fill = ScreenCode.Space, byte color = 0)
    {
        ArgumentNullException.ThrowIfNull(memory);
        var screen = new byte[CellCount];
        Array.Fill(screen, fill);
        memory.WriteRange(screenBase, screen);

        var colors = new byte[CellCount];
        Array.Fill(colors, (byte)(color & 0x0F));
        WriteColor(memory, colorBase, colors);
    }

    private static void WriteColor(IMemoryService memory, ushort address, ReadOnlySpan<byte> colors)
    {
        if (address is >= 0xD000 and < 0xE000)
            memory.WriteIo(address, colors);
        else
            memory.WriteRange(address, colors);
    }
}
