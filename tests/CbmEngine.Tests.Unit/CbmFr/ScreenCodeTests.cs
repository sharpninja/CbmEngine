using CbmEngine.Systems.Text;
using Xunit;

namespace CbmEngine.Tests.Unit.CbmFr;

// FR-CBM-TEXT-001 (CBMFR-008): ASCII -> C64 screen-code mapping.
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

    [Fact]
    public void TEST_CBM_048_FromAscii_UnknownMapsToSpace()
    {
        Assert.Equal(0x20, ScreenCode.FromAscii('#'));
        Assert.Equal(0x20, ScreenCode.FromAscii('~'));
    }
}
