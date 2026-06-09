using CbmEngine.Game.Sample;
using CbmEngine.Systems;
using CbmEngine.Systems.Cartridge;
using CbmEngine.Systems.Strategy;
using CbmEngine.Tests.Integration.Helpers;
using Xunit;

namespace CbmEngine.Tests.Integration;

[Trait("Speed", "Slow")]
public class SampleGameTests
{
    private static (GameContext Ctx, DemoGame Game) BuildSample()
    {
        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        var cart = BootstrapCart.BuildMarkerOnly16K();
        var boot = CartridgeBoot.AttachAndWaitForMarker(sys, cart, maxFrames: 300);
        Assert.True(boot.MarkerSeen);
        var ctx = new GameContext(sys);
        var game = new DemoGame();
        game.Initialize(ctx);
        return (ctx, game);
    }

    [Fact]
    public void DemoGame_Initialize_RunsWithoutThrowing()
    {
        var (ctx, _) = BuildSample();
        Assert.NotNull(ctx);
    }

    [Fact]
    public void TEST_CBM_GAME_001_DemoGame_UpdateAdvancesFrameCounter()
    {
        var (ctx, game) = BuildSample();
        for (int i = 0; i < 10; i++)
        {
            game.Update(ctx, i);
            ctx.Machine.RunFrame();
        }
        Assert.Equal(9, game.FrameCounter);
    }

    [Fact]
    public void TEST_CBM_GAME_004_EngineTickRate_MatchesProfileRefresh()
    {
        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        Assert.InRange(sys.Capabilities.RefreshRateHz, 50.0, 50.3);
        var ntsc = CommodoreSystem.Build("ntsc", TestRomProvider.Create());
        Assert.InRange(ntsc.Capabilities.RefreshRateHz, 59.7, 60.0);
    }
}
