using CbmEngine.Pipeline;
using Xunit;

namespace CbmEngine.Tests.Unit.CbmFr;

// FR-CBM-CANVAS-001 (CBMFR-004): bundled C64 8x8 font.
[Trait("Speed", "Fast")]
public class CbmFont8x8Tests
{
    [Fact]
    public void TEST_CBM_041_GetGlyph_CoversLettersDigitsSpace_MissingBlank()
    {
        Assert.Equal(8, CbmFont8x8.GetGlyph('A').Length);
        Assert.Equal(8, CbmFont8x8.GetGlyph('5').Length);

        var space = CbmFont8x8.GetGlyph(' ');
        Assert.Equal(8, space.Length);
        Assert.True(IsBlank(space));

        var missing = CbmFont8x8.GetGlyph('#');
        Assert.Equal(8, missing.Length);
        Assert.True(IsBlank(missing));

        // A real glyph is non-blank.
        Assert.False(IsBlank(CbmFont8x8.GetGlyph('A')));
    }

    private static bool IsBlank(ReadOnlySpan<byte> g)
    {
        foreach (var b in g) if (b != 0) return false;
        return true;
    }
}
