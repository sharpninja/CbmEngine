using CbmEngine.Pipeline;
using Xunit;

namespace CbmEngine.Tests.Unit.Phase3;

[Trait("Speed", "Fast")]
public class CellPackerTests
{
    [Fact]
    public void HiRes_SingleInkPerCell_PacksToCorrectBytes()
    {
        var pixels = new int[64];
        for (int x = 0; x < 8; x++) pixels[x] = 1;
        var packed = CellPacker.PackHiRes(pixels, backgroundIndex: 0);
        Assert.Equal(CharMode.HiRes, packed.Mode);
        Assert.Equal(0xFF, packed.Bytes[0]);
        for (int y = 1; y < 8; y++) Assert.Equal(0x00, packed.Bytes[y]);
        Assert.Equal(1, packed.Ink);
    }

    [Fact]
    public void HiRes_TwoNonBgInks_ThrowsContentProcessingException()
    {
        var pixels = new int[64];
        pixels[0] = 1;
        pixels[1] = 2;
        var ex = Assert.Throws<ContentProcessingException>(() => CellPacker.PackHiRes(pixels, 0));
        Assert.Contains("hi-res", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Multicolor_PacksFourBitPairsCorrectly()
    {
        var pixels = new int[64];
        for (int x = 0; x < 8; x += 2) { pixels[x] = 2; pixels[x + 1] = 2; }
        var screen = new ScreenColorConfig(BackgroundD021: 0, McBackgroundD022: 1, McBorderD023: 2);
        var packed = CellPacker.PackMulticolor(pixels, screen);
        Assert.Equal(CharMode.Multicolor, packed.Mode);
        Assert.Equal(0xAA, packed.Bytes[0]);
    }

    [Fact]
    public void Multicolor_InkAbove7_Throws()
    {
        var pixels = new int[64];
        for (int x = 0; x < 8; x += 2) pixels[x] = 9;
        var screen = new ScreenColorConfig(0, 1, 2);
        var ex = Assert.Throws<ContentProcessingException>(() => CellPacker.PackMulticolor(pixels, screen));
        Assert.Contains("0..7", ex.Message);
    }

    [Fact]
    public void InferMode_ReturnsHiResWhenOneNonBgColor()
    {
        var pixels = new int[64];
        for (int x = 0; x < 8; x++) pixels[x] = 1;
        Assert.Equal(CharMode.HiRes, CellPacker.InferMode(pixels, 0));
    }

    [Fact]
    public void InferMode_ReturnsMulticolorWhenMultipleNonBgColors()
    {
        var pixels = new int[64];
        pixels[0] = 1; pixels[1] = 2;
        Assert.Equal(CharMode.Multicolor, CellPacker.InferMode(pixels, 0));
    }
}
