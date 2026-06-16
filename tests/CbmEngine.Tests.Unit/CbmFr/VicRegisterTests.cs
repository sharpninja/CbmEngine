using CbmEngine.Systems.Video;
using Xunit;

namespace CbmEngine.Tests.Unit.CbmFr;

// FR-CBM-VIC-001 (CBMFR-005): typed VIC mode/register helpers.
[Trait("Speed", "Fast")]
public class VicRegisterTests
{
    [Fact]
    public void TEST_CBM_007_MulticolorBitmap_Registers()
    {
        var r = Vic.MulticolorBitmap(1, 0x6000, 0x4400);
        Assert.Equal(0x3B, r.D011);
        Assert.Equal(0xD8, r.D016);
        Assert.Equal(0x18, r.D018);
    }

    [Fact]
    public void TEST_CBM_008_HiResBitmap_Registers()
    {
        var r = Vic.HiResBitmap(1, 0x6000, 0x4400);
        Assert.Equal(0xC8, r.D016);
        Assert.Equal(0x3B, r.D011);
        Assert.Equal(0x18, r.D018);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(3, 0)]
    public void TEST_CBM_009_BankBits(int bank, byte expected) =>
        Assert.Equal(expected, Vic.BankBits(bank));

    [Fact]
    public void TEST_CBM_010_SetMulticolorBitmap_WritesRegisters()
    {
        var mem = new RecordingMemory();
        Vic.SetMulticolorBitmap(mem, 1, 0x6000, 0x4400);
        Assert.Equal(0x3B, mem.ReadIo(0xD011));
        Assert.Equal(0xD8, mem.ReadIo(0xD016));
        Assert.Equal(0x18, mem.ReadIo(0xD018));
        Assert.Equal(2, mem.ReadIo(0xDD00) & 0x03);
    }

    [Fact]
    public void TEST_CBM_011_SetBorderBackground_Masked()
    {
        var mem = new RecordingMemory();
        Vic.SetBorder(mem, 0x1E);      // masked to 0x0E
        Vic.SetBackground(mem, 0x16);  // masked to 0x06
        Assert.Equal(0x0E, mem.ReadIo(0xD020));
        Assert.Equal(0x06, mem.ReadIo(0xD021));
    }

    [Theory]
    [InlineData(4, 0x6000, 0x4400, "bank")]        // bank out of 0-3
    [InlineData(1, 0x5000, 0x4400, "bitmapBase")]  // bitmap offset not 0/0x2000
    [InlineData(1, 0x6000, 0x4500, "screenBase")]  // screen not on 0x400 boundary
    public void TEST_CBM_012_InvalidArgs_Throw(int bank, int bitmap, int screen, string param)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Vic.MulticolorBitmap(bank, (ushort)bitmap, (ushort)screen));
        Assert.Equal(param, ex.ParamName);
    }
}
