using CbmEngine.Pipeline;
using Xunit;

namespace CbmEngine.Tests.Unit.Phase3;

[Trait("Speed", "Fast")]
public class VicPaletteTests
{
    [Fact]
    public void Palette_HasSixteenEntries()
    {
        Assert.Equal(16, VicPalette.Colors.Length);
    }

    [Fact]
    public void Palette_MatchesViceSharpReference()
    {
        Assert.Equal(new VicPalette.Rgb(0x00, 0x00, 0x00), VicPalette.Colors[0]);
        Assert.Equal(new VicPalette.Rgb(0xFF, 0xFF, 0xFF), VicPalette.Colors[1]);
        Assert.Equal(new VicPalette.Rgb(0x6B, 0x5E, 0xD1), VicPalette.Colors[14]);
        Assert.Equal(new VicPalette.Rgb(0x1B, 0x1B, 0x8E), VicPalette.Colors[6]);
    }

    [Fact]
    public void TryExact_FindsKnownColors()
    {
        Assert.True(VicPalette.TryExact(0xFF, 0xFF, 0xFF, out var idx) && idx == 1);
        Assert.True(VicPalette.TryExact(0x6B, 0x5E, 0xD1, out idx) && idx == 14);
        Assert.False(VicPalette.TryExact(0xAB, 0xCD, 0xEF, out _));
    }
}
