namespace CbmEngine.Pipeline;

public sealed record CharsetBand(int FirstRowInclusive, int LastRowInclusive, byte[] CharsetBytes);

public sealed record CompiledBandedScreen(
    IReadOnlyList<CharsetBand> Bands,
    byte[] ScreenRam,
    byte[] ColorRam,
    IReadOnlyList<(int rasterLine, byte d018Value)> SplitTable);

public static class BandedScreenAllocator
{
    public const int ScreenWidth = 40;
    public const int ScreenHeight = 25;
    public const int MaxSlotsPerCharset = 256;
    public const int CharsetSizeBytes = 2048;

    public static CompiledBandedScreen Allocate(
        IReadOnlyList<CellPacker.PackedCell> cellsRowMajor,
        int charsetBudget,
        int displayTopRasterLine = 51)
    {
        ArgumentNullException.ThrowIfNull(cellsRowMajor);
        if (charsetBudget <= 0 || charsetBudget > 8) throw new ArgumentOutOfRangeException(nameof(charsetBudget));
        if (cellsRowMajor.Count != ScreenWidth * ScreenHeight)
            throw new ArgumentException($"Expected {ScreenWidth * ScreenHeight} cells; got {cellsRowMajor.Count}.", nameof(cellsRowMajor));

        var bands = new List<CharsetBand>();
        var splitTable = new List<(int, byte)>();
        var screenRam = new byte[ScreenWidth * ScreenHeight];
        var colorRam = new byte[ScreenWidth * ScreenHeight];

        int row = 0;
        int bandIndex = 0;
        while (row < ScreenHeight)
        {
            if (bandIndex >= charsetBudget)
                throw new BandedAllocationException(row, ScreenHeight - 1,
                    $"Banded allocator exceeded budget of {charsetBudget} charsets (rows {row}..{ScreenHeight - 1} remaining).");

            int bandEnd = AddRowsUntilFull(row, cellsRowMajor, out var charsetBytes, out var slotByKey, out var perCellSlot);
            if (bandEnd == row && slotByKey.Count > MaxSlotsPerCharset)
                throw new BandedAllocationException(row, row,
                    $"Single row {row} requires {slotByKey.Count} distinct glyphs, exceeding {MaxSlotsPerCharset} slots per charset.");

            for (int r = row; r <= bandEnd; r++)
                for (int c = 0; c < ScreenWidth; c++)
                {
                    int cellIdx = r * ScreenWidth + c;
                    int packedIdx = (r - row) * ScreenWidth + c;
                    screenRam[cellIdx] = (byte)perCellSlot[packedIdx];
                    var cell = cellsRowMajor[cellIdx];
                    colorRam[cellIdx] = cell.Mode == CharMode.Multicolor ? (byte)(0x08 | cell.Ink) : cell.Ink;
                }

            bands.Add(new CharsetBand(row, bandEnd, charsetBytes));
            byte d018 = ComputeD018(bandIndex);
            int splitRasterLine = displayTopRasterLine + row * 8;
            splitTable.Add((splitRasterLine, d018));

            bandIndex++;
            row = bandEnd + 1;
        }

        return new CompiledBandedScreen(bands, screenRam, colorRam, splitTable);
    }

    private static int AddRowsUntilFull(
        int startRow,
        IReadOnlyList<CellPacker.PackedCell> cells,
        out byte[] charsetBytes,
        out Dictionary<(CharMode mode, ulong key), int> slotByKey,
        out int[] perCellSlot)
    {
        slotByKey = new Dictionary<(CharMode, ulong), int>();
        var perCellSlotList = new List<int>();
        int lastFittingRow = startRow;
        var bandSlots = new byte[MaxSlotsPerCharset * 8];
        int slotCount = 0;

        for (int r = startRow; r < ScreenHeight; r++)
        {
            var rowSlots = new int[ScreenWidth];
            var rowDictionary = new Dictionary<(CharMode, ulong), int>(slotByKey);
            int rowSlotCount = slotCount;
            bool overflow = false;

            for (int c = 0; c < ScreenWidth; c++)
            {
                var cell = cells[r * ScreenWidth + c];
                var key = (cell.Mode, GlyphKey(cell.Bytes));
                if (rowDictionary.TryGetValue(key, out var slot))
                {
                    rowSlots[c] = slot;
                }
                else
                {
                    if (rowSlotCount >= MaxSlotsPerCharset) { overflow = true; break; }
                    rowDictionary[key] = rowSlotCount;
                    rowSlots[c] = rowSlotCount;
                    rowSlotCount++;
                }
            }

            if (overflow) break;

            foreach (var kv in rowDictionary)
            {
                if (slotByKey.ContainsKey(kv.Key)) continue;
                slotByKey[kv.Key] = kv.Value;
                var matchingCell = FindCellByKey(cells, r, ScreenWidth, kv.Key);
                Buffer.BlockCopy(matchingCell, 0, bandSlots, kv.Value * 8, 8);
            }
            slotCount = rowSlotCount;
            perCellSlotList.AddRange(rowSlots);
            lastFittingRow = r;
        }

        charsetBytes = new byte[CharsetSizeBytes];
        Array.Copy(bandSlots, charsetBytes, Math.Min(bandSlots.Length, CharsetSizeBytes));
        perCellSlot = perCellSlotList.ToArray();
        return lastFittingRow;
    }

    private static byte[] FindCellByKey(IReadOnlyList<CellPacker.PackedCell> cells, int upToRowInclusive, int screenWidth, (CharMode mode, ulong key) key)
    {
        for (int r = 0; r <= upToRowInclusive; r++)
            for (int c = 0; c < screenWidth; c++)
            {
                var cell = cells[r * screenWidth + c];
                if (cell.Mode == key.mode && GlyphKey(cell.Bytes) == key.key) return cell.Bytes;
            }
        throw new InvalidOperationException("Glyph key not found in source cells (allocator bug).");
    }

    private static ulong GlyphKey(byte[] glyph)
    {
        ulong k = 0;
        for (int i = 0; i < 8; i++) k |= ((ulong)glyph[i]) << (i * 8);
        return k;
    }

    private static byte ComputeD018(int bandIndex)
    {
        int slot = (bandIndex + 4) & 0x07;
        return (byte)((slot << 1) | 0x10);
    }
}
