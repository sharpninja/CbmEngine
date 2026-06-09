using CbmEngine.Pipeline;
using Xunit;

namespace CbmEngine.Tests.Unit.Phase3;

[Trait("Speed", "Fast")]
public class BandedAllocatorTests
{
    private static List<CellPacker.PackedCell> MakeUniformCells(byte glyphByte)
    {
        var cells = new List<CellPacker.PackedCell>(BandedScreenAllocator.ScreenWidth * BandedScreenAllocator.ScreenHeight);
        var bytes = new byte[8];
        for (int i = 0; i < 8; i++) bytes[i] = glyphByte;
        for (int i = 0; i < cells.Capacity; i++)
            cells.Add(new CellPacker.PackedCell(CharMode.HiRes, bytes, Ink: 1, BackgroundIndex: 0));
        return cells;
    }

    [Fact]
    public void UniformScreen_FitsInOneBandOneSlot()
    {
        var cells = MakeUniformCells(0xFF);
        var compiled = BandedScreenAllocator.Allocate(cells, charsetBudget: 4);
        Assert.Single(compiled.Bands);
        Assert.Equal(0, compiled.Bands[0].FirstRowInclusive);
        Assert.Equal(24, compiled.Bands[0].LastRowInclusive);
        Assert.Single(compiled.SplitTable);
        Assert.All(compiled.ScreenRam, slot => Assert.Equal(0, slot));
        Assert.All(compiled.ColorRam, ink => Assert.Equal(1, ink));
    }

    [Fact]
    public void HighVariety_RequiresMultipleBands()
    {
        var cells = new List<CellPacker.PackedCell>();
        for (int row = 0; row < BandedScreenAllocator.ScreenHeight; row++)
            for (int col = 0; col < BandedScreenAllocator.ScreenWidth; col++)
            {
                var bytes = new byte[8];
                int seed = (row * 40 + col) & 0xFFFF;
                for (int i = 0; i < 8; i++) bytes[i] = (byte)((seed >> (i & 7)) ^ i);
                cells.Add(new CellPacker.PackedCell(CharMode.HiRes, bytes, 1, 0));
            }

        var compiled = BandedScreenAllocator.Allocate(cells, charsetBudget: 8);
        Assert.True(compiled.Bands.Count >= 3, $"Expected >=3 bands for high variety; got {compiled.Bands.Count}.");
    }

    [Fact]
    public void OverflowingBudget_ThrowsWithRowRange()
    {
        var cells = new List<CellPacker.PackedCell>();
        for (int row = 0; row < BandedScreenAllocator.ScreenHeight; row++)
            for (int col = 0; col < BandedScreenAllocator.ScreenWidth; col++)
            {
                var bytes = new byte[8];
                int seed = (row * 40 + col) & 0xFFFF;
                for (int i = 0; i < 8; i++) bytes[i] = (byte)((seed >> i) ^ (row * 7 + col + i));
                cells.Add(new CellPacker.PackedCell(CharMode.HiRes, bytes, 1, 0));
            }

        var ex = Assert.Throws<BandedAllocationException>(() => BandedScreenAllocator.Allocate(cells, charsetBudget: 1));
        Assert.True(ex.OverflowingRowStart >= 0);
        Assert.Contains("budget", ex.Message);
    }
}
