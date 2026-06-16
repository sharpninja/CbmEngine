using CbmEngine.Pipeline;
using Xunit;

namespace CbmEngine.Tests.Unit.CbmFr;

// FR-CBM-CANVAS-001 (CBMFR-004): multicolor drawing canvas + bundled font.
[Trait("Speed", "Fast")]
public class MulticolorCanvasTests
{
    [Fact]
    public void TEST_CBM_035_NewCanvas_DefaultsZero_ClearSetsAll()
    {
        var c = new MulticolorCanvas();
        Assert.Equal(0, c.GetPixel(0, 0));
        c.Clear(5);
        Assert.Equal(5, c.GetPixel(0, 0));
        Assert.Equal(5, c.GetPixel(c.Width - 1, c.Height - 1));
    }

    [Fact]
    public void TEST_CBM_036_FillRect_SetsRegion_ClippedToBounds()
    {
        var c = new MulticolorCanvas();
        c.FillRect(10, 10, 4, 4, 7);
        Assert.Equal(7, c.GetPixel(10, 10));
        Assert.Equal(7, c.GetPixel(13, 13));
        Assert.Equal(0, c.GetPixel(14, 14));
        // Clipping: a rect crossing the right edge writes only in-bounds pixels and does not throw.
        c.FillRect(158, 0, 10, 2, 3);
        Assert.Equal(3, c.GetPixel(159, 0));
    }

    [Fact]
    public void TEST_CBM_037_SetPixel_OutOfBounds_Ignored()
    {
        var c = new MulticolorCanvas();
        c.SetPixel(-1, 0, 4);
        c.SetPixel(c.Width, 0, 4);
        c.SetPixel(0, c.Height, 4);
        Assert.Equal(0, c.GetPixel(0, 0));
    }

    [Fact]
    public void TEST_CBM_038_DrawGlyph_SetBitsColored_ClearBitsUntouched()
    {
        var c = new MulticolorCanvas();
        var glyph = new byte[] { 0x80, 0, 0, 0, 0, 0, 0, 0 }; // only top-left pixel set
        c.DrawGlyph(0, 0, glyph, 6);
        Assert.Equal(6, c.GetPixel(0, 0));
        Assert.Equal(0, c.GetPixel(1, 0)); // clear bit untouched
    }

    [Fact]
    public void TEST_CBM_039_DrawText_RendersGlyphs_AdvancesAndBlanksUnknown()
    {
        var c = new MulticolorCanvas();
        c.DrawText(0, 0, "AB", 1);
        // 'A' row0 = 0x18 -> cols 3,4 set.
        Assert.Equal(1, c.GetPixel(3, 0));
        Assert.Equal(1, c.GetPixel(4, 0));
        Assert.Equal(0, c.GetPixel(0, 0));
        // 'B' advanced by 8: row0 = 0x7C -> col 1 (x=9) set.
        Assert.Equal(1, c.GetPixel(9, 0));

        // Unknown char renders blank.
        c.DrawText(20, 20, "#", 7);
        for (int x = 20; x < 28; x++) Assert.Equal(0, c.GetPixel(x, 20));
    }

    [Fact]
    public void TEST_CBM_040_Encode_ReturnsValidBitmap()
    {
        var c = new MulticolorCanvas();
        c.Clear(0);
        c.FillRect(0, 0, 20, 20, 2);
        c.DrawText(0, 0, "HI", 1);
        var enc = c.Encode();
        Assert.Equal(EncodedSplashBitmap.BitmapByteSize, enc.Bitmap.Length);
        Assert.Equal(EncodedSplashBitmap.ScreenRamSize, enc.ScreenRam.Length);
        Assert.Equal(EncodedSplashBitmap.ColorRamSize, enc.ColorRam.Length);
    }

    [Fact]
    public void TEST_CBM_042_ManyColorsInCell_StillEncodes()
    {
        var c = new MulticolorCanvas();
        // One 4x8 multicolor cell = logical x 0..3, y 0..7. Put 5 distinct colours in it.
        c.SetPixel(0, 0, 1);
        c.SetPixel(1, 0, 2);
        c.SetPixel(2, 0, 3);
        c.SetPixel(3, 0, 4);
        c.SetPixel(0, 1, 5);
        var enc = c.Encode();   // encoder reduces to 4 colours/cell; must not throw
        Assert.Equal(EncodedSplashBitmap.BitmapByteSize, enc.Bitmap.Length);
    }
}
