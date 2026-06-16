using CbmEngine.Systems.Text;
using Xunit;

namespace CbmEngine.Tests.Unit.CbmFr;

// FR-CBM-TEXT-001 (CBMFR-008/CBMFR-010): ASCII -> C64 screen-code mapping.
[Trait("Speed", "Fast")]
public class ScreenCodeTests
{
    [Fact]
    public void TEST_CBM_043_FromAscii_MapsLettersDigitsSpace()
    {
        Assert.Equal(1, ScreenCode.FromAscii('A'));
        Assert.Equal(26, ScreenCode.FromAscii('Z'));
        Assert.Equal(0x30, ScreenCode.FromAscii('0'));
        Assert.Equal(0x39, ScreenCode.FromAscii('9'));
        Assert.Equal(0x20, ScreenCode.FromAscii(' '));
        Assert.Equal(1, ScreenCode.FromAscii('a'));   // case-insensitive
    }

    [Theory]
    // CBMFR-010: ASCII $20-$3F (space, punctuation, digits, ':;<=>?') maps 1:1 to C64 screen codes.
    [InlineData('!', 0x21)]
    [InlineData('"', 0x22)]
    [InlineData('#', 0x23)]
    [InlineData('$', 0x24)]
    [InlineData('%', 0x25)]
    [InlineData('&', 0x26)]
    [InlineData('\'', 0x27)]
    [InlineData('(', 0x28)]
    [InlineData(')', 0x29)]
    [InlineData('*', 0x2A)]
    [InlineData('+', 0x2B)]
    [InlineData(',', 0x2C)]
    [InlineData('-', 0x2D)]
    [InlineData('.', 0x2E)]
    [InlineData('/', 0x2F)]
    [InlineData(':', 0x3A)]
    [InlineData(';', 0x3B)]
    [InlineData('<', 0x3C)]
    [InlineData('=', 0x3D)]
    [InlineData('>', 0x3E)]
    [InlineData('?', 0x3F)]
    public void TEST_CBM_055_FromAscii_PunctuationMapsOneToOne(char ascii, byte screenCode)
    {
        Assert.Equal(screenCode, ScreenCode.FromAscii(ascii));
    }

    [Fact]
    public void TEST_CBM_048_FromAscii_OutOfRangeMapsToSpace()
    {
        // Characters above the $20-$3F + letter ranges still collapse to space.
        Assert.Equal(0x20, ScreenCode.FromAscii('~'));   // $7E
        Assert.Equal(0x20, ScreenCode.FromAscii('\t'));  // $09
        Assert.Equal(0x20, ScreenCode.FromAscii('['));   // $5B - above 'Z'
    }
}
