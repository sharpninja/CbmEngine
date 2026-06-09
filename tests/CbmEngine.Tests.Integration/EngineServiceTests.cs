using CbmEngine.Systems;
using CbmEngine.Systems.Services;
using CbmEngine.Systems.Strategy;
using CbmEngine.Tests.Integration.Helpers;
using Xunit;

namespace CbmEngine.Tests.Integration;

[Trait("Speed", "Slow")]
public class EngineServiceTests
{
    private static GameContext BuildContext()
    {
        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        for (int i = 0; i < 60; i++) sys.RunFrame();
        return new GameContext(sys);
    }

    [Fact]
    public void Sprite_SetPosition_UpdatesD000AndD001()
    {
        var ctx = BuildContext();
        ctx.Sprites.SetPosition(0, x: 100, y: 150);
        Assert.Equal(100, ctx.Machine.Bus.Read(0xD000));
        Assert.Equal(150, ctx.Machine.Bus.Read(0xD001));
        Assert.Equal(0, ctx.Machine.Bus.Read(0xD010) & 0x01);
    }

    [Fact]
    public void Sprite_SetPositionAbove255_SetsMsbBit()
    {
        var ctx = BuildContext();
        ctx.Sprites.SetPosition(0, x: 300, y: 150);
        Assert.Equal(300 & 0xFF, ctx.Machine.Bus.Read(0xD000));
        Assert.True((ctx.Machine.Bus.Read(0xD010) & 0x01) != 0);
    }

    [Fact]
    public void Sprite_SetEnabled_TogglesD015Bit()
    {
        var ctx = BuildContext();
        ctx.Sprites.SetEnabled(2, true);
        Assert.True((ctx.Machine.Bus.Read(0xD015) & 0x04) != 0);
        ctx.Sprites.SetEnabled(2, false);
        Assert.True((ctx.Machine.Bus.Read(0xD015) & 0x04) == 0);
    }

    [Fact]
    public void Tilemap_SetCell_WritesScreenAndColorRam()
    {
        var ctx = BuildContext();
        ctx.Tilemap.SetCell(col: 5, row: 3, glyph: 0x41, color: 7);
        Assert.Equal(0x41, ctx.Machine.Bus.Read((ushort)(0x0400 + 3 * 40 + 5)));
        Assert.Equal(7, ctx.Machine.Bus.Read((ushort)(0xD800 + 3 * 40 + 5)) & 0x0F);
    }

    [Fact]
    public void Tilemap_Fill_WritesAllCells()
    {
        var ctx = BuildContext();
        ctx.Tilemap.Fill(glyph: 0x20, color: 0);
        for (int r = 0; r < TilemapService.Rows; r++)
            for (int c = 0; c < TilemapService.Columns; c++)
                Assert.Equal(0x20, ctx.Tilemap.ReadGlyph(c, r));
    }
}
