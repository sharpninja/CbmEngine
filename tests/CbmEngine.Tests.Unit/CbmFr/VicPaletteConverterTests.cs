using CbmEngine.Pipeline;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace CbmEngine.Tests.Unit.CbmFr;

// FR-CBM-PALETTE-001 (CBMFR-011): VicPaletteConverter selects a VIC palette index for any RGB or
// Rgba32 color, with optional curated subset to bias the choice toward saturated hues.
[Trait("Speed", "Fast")]
public class VicPaletteConverterTests
{
    [Fact]
    public void TEST_CBM_056_NearestIndex_ExactPaletteColor_ReturnsItsIndex()
    {
        for (byte i = 0; i < 16; i++)
        {
            var c = VicPalette.Colors[i];
            Assert.Equal(i, VicPaletteConverter.NearestIndex(c.R, c.G, c.B));
        }
    }

    [Fact]
    public void TEST_CBM_057_NearestIndex_OffPaletteColor_SnapsToClosest()
    {
        // (250, 250, 250) is very close to white (VIC 1 = 0xFF,0xFF,0xFF) and far from all other
        // VIC colors; the nearest selection must be 1.
        Assert.Equal(1, VicPaletteConverter.NearestIndex(250, 250, 250));

        // (10, 10, 10) is very close to black (VIC 0); selection must be 0.
        Assert.Equal(0, VicPaletteConverter.NearestIndex(10, 10, 10));
    }

    [Fact]
    public void TEST_CBM_058_NearestIndex_Rgba32Overload_IgnoresAlpha()
    {
        var c = VicPalette.Colors[7]; // yellow
        Assert.Equal(7, VicPaletteConverter.NearestIndex(new Rgba32(c.R, c.G, c.B, 255)));
        Assert.Equal(7, VicPaletteConverter.NearestIndex(new Rgba32(c.R, c.G, c.B, 0)));
        Assert.Equal(7, VicPaletteConverter.NearestIndex(new Rgba32(c.R, c.G, c.B, 128)));
    }

    [Fact]
    public void TEST_CBM_059_NearestIndex_RestrictedToSubset_PicksWithinSubset()
    {
        // Brown (VIC 9 = 0x5A,0x33,0x00) is the exact-match for itself; restricting the subset to
        // {0=black, 7=yellow, 8=orange} forces the algorithm to pick something else. Orange
        // (0x9B,0x52,0x1C) is by far the closest among the three.
        ReadOnlySpan<byte> allowed = stackalloc byte[] { 0, 7, 8 };
        var brown = VicPalette.Colors[9];
        Assert.Equal(8, VicPaletteConverter.NearestIndex(brown.R, brown.G, brown.B, allowed));
    }

    [Fact]
    public void TEST_CBM_060_NearestIndex_RestrictedToSubset_BlueGrayPicksBlueNotGray()
    {
        // A blue-tinged gray (110, 120, 160) without a subset usually maps to medium gray (VIC 12).
        // Restricting the subset to {0=black, 6=blue, 14=light blue, 15=light gray} excludes gray
        // and forces a saturated-hue choice. The expected pick is one of the blues.
        ReadOnlySpan<byte> allowed = stackalloc byte[] { 0, 6, 14, 15 };
        var picked = VicPaletteConverter.NearestIndex(110, 120, 160, allowed);
        Assert.True(picked == 6 || picked == 14, $"expected blue (6) or light blue (14), got {picked}");
    }

    [Fact]
    public void TEST_CBM_061_NearestIndex_EmptySubset_FallsBackToFullPalette()
    {
        // An empty allowedIndices span means "no restriction"; behavior must match the unrestricted
        // overload.
        var c = VicPalette.Colors[5]; // green
        var unrestricted = VicPaletteConverter.NearestIndex(c.R, c.G, c.B);
        var withEmptySubset = VicPaletteConverter.NearestIndex(c.R, c.G, c.B, ReadOnlySpan<byte>.Empty);
        Assert.Equal(unrestricted, withEmptySubset);
    }

    [Fact]
    public void TEST_CBM_062_NearestIndex_InvalidIndexInSubset_Throws()
    {
        // Indices above 15 are not valid VIC palette entries; the converter must refuse to use them
        // rather than silently misbehave.
        var span = new byte[] { 0, 16 };
        Assert.Throws<ArgumentOutOfRangeException>(() => VicPaletteConverter.NearestIndex(0, 0, 0, span));
    }
}
