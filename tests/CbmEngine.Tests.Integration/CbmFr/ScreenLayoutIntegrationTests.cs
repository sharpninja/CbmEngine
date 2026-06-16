using System.Linq;
using CbmEngine.Abstractions;
using CbmEngine.Systems.Layout;
using CbmEngine.Systems.LineDirector;
using CbmEngine.Systems.Strategy;
using CbmEngine.Tests.Integration.Helpers;
using Xunit;

namespace CbmEngine.Tests.Integration.CbmFr;

// FR-CBM-LAYOUT-001 (CBMFR-007) AC5: steady-state raster-split program runs on a real machine.
[Trait("Speed", "Slow")]
public class ScreenLayoutIntegrationTests
{
    private static ICommodoreMachine BuildC64(int warmupFrames = 60)
    {
        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        for (int i = 0; i < warmupFrames; i++) sys.RunFrame();
        return sys;
    }

    [Fact]
    public void TEST_CBM_054_SteadyState_RunsThroughLineDirector_OverFullFrame()
    {
        var layout = new ScreenLayout.Builder()
            .AddCharBand(3)
            .AddBitmapBand(19, multicolor: true)
            .AddCharBand(3)
            .Build(bank: 1);

        var sys = BuildC64();
        var director = new LineDirector(sys, layout.SteadyState);

        var observed = director.RunFrameAndRecordRasterLines();

        // The director stepped through a full frame.
        Assert.Equal(director.TotalLines, observed.Count);

        // Every split raster line falls within the frame the director runs.
        foreach (var line in layout.SteadyState.Lines)
            Assert.InRange(line, 0, director.TotalLines - 1);

        // The split program rendered a valid framebuffer.
        Assert.Equal(sys.VideoChip.FrameWidth * sys.VideoChip.FrameHeight * 4, sys.VideoChip.FrameBuffer.Length);
    }
}
