using System.Linq;
using CbmEngine.Systems.Text;
using Xunit;

namespace CbmEngine.Tests.Unit.CbmFr;

// FR-CBM-TEXT-001 (CBMFR-008): char-mode text rendering helper.
[Trait("Speed", "Fast")]
public class TextServiceTests
{
    [Fact]
    public void TEST_CBM_044_Write_PlacesScreenCodesAtOffset()
    {
        var mem = new RecordingMemory();
        TextService.Write(mem, screenBase: 0x0400, colorBase: 0xD800, col: 2, row: 1, text: "HI", color: 1);
        int offset = 1 * 40 + 2; // 42
        Assert.Equal(8, mem.Ram[0x0400 + offset]);     // H => 8
        Assert.Equal(9, mem.Ram[0x0400 + offset + 1]); // I => 9
    }

    [Fact]
    public void TEST_CBM_045_Write_Color_UsesIoForColorRamElseRange()
    {
        // Colour RAM in the I/O page routes through WriteIo.
        var io = new RecordingMemory();
        TextService.Write(io, 0x0400, 0xD800, col: 0, row: 0, text: "AB", color: 7);
        Assert.Equal(2, io.IoWrites.Count(w => w.Address is 0xD800 or 0xD801 && w.Value == 7));

        // A non-I/O colour base uses WriteRange.
        var ram = new RecordingMemory();
        TextService.Write(ram, 0x0400, 0x0800, col: 0, row: 0, text: "AB", color: 7);
        Assert.Contains(ram.RangeWrites, r => r.Address == 0x0800 && r.Length == 2);
    }

    [Fact]
    public void TEST_CBM_046_Write_OverflowRow_Throws()
    {
        var mem = new RecordingMemory();
        Assert.Throws<ArgumentException>(() =>
            TextService.Write(mem, 0x0400, 0xD800, col: 38, row: 0, text: "HELLO", color: 1));
    }

    [Theory]
    [InlineData(40, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 25)]
    [InlineData(0, -1)]
    public void TEST_CBM_047_Write_ColRowOutOfRange_Throws(int col, int row)
    {
        var mem = new RecordingMemory();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TextService.Write(mem, 0x0400, 0xD800, col, row, "X", 1));
    }

    [Fact]
    public void TEST_CBM_049_Clear_FillsScreenAndColor()
    {
        var mem = new RecordingMemory();
        TextService.Clear(mem, 0x0400, 0xD800, fill: 0x20, color: 6);
        Assert.Contains(mem.RangeWrites, r => r.Address == 0x0400 && r.Length == 1000);
        Assert.Equal(1000, mem.IoWrites.Count(w => w.Address >= 0xD800 && w.Address <= 0xDBE7 && w.Value == 6));
    }
}
