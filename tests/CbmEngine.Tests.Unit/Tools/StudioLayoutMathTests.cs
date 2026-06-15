using CbmEngine.Tools.CbmVidStudio;
using Xunit;

namespace CbmEngine.Tests.Unit.Tools;

[Trait("Speed", "Fast")]
public class StudioLayoutMathTests
{
    [Fact]
    public void InitialClientSize_StandardDpi_1080p_IsLogicalCanvas()
    {
        var (w, h) = StudioLayoutMath.ComputeInitialClientSize(1920, 1080, dpiFactor: 1.0f);
        Assert.Equal(1280, w);
        Assert.Equal(800, h);
    }

    [Fact]
    public void InitialClientSize_250PercentDpi_4K_ScalesUpAndFits()
    {
        var (w, h) = StudioLayoutMath.ComputeInitialClientSize(3840, 2160, dpiFactor: 2.5f);
        // 1280*2.5 = 3200 <= 3840*0.92 = 3532 -> 3200 wide.
        Assert.Equal(3200, w);
        // 800*2.5 = 2000 > 2160*0.85 = 1836 -> capped at 1836.
        Assert.Equal(1836, h);
        Assert.True(w <= 3840 && h <= 2160, "must fit the display");
    }

    [Fact]
    public void InitialClientSize_SmallDisplay_CappedToDisplayFraction()
    {
        var (w, h) = StudioLayoutMath.ComputeInitialClientSize(1366, 768, dpiFactor: 1.0f);
        Assert.True(w <= (int)(1366 * 0.92f) + 1, $"width {w} exceeds display cap");
        Assert.True(h >= 600, "height floor respected");
    }

    [Fact]
    public void InitialClientSize_SubUnityDpi_TreatedAsOne()
    {
        var (w, h) = StudioLayoutMath.ComputeInitialClientSize(1920, 1080, dpiFactor: 0.6f);
        Assert.Equal(1280, w);
        Assert.Equal(800, h);
    }
}
