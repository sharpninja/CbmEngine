using System.Linq;
using CbmEngine.Systems.Layout;
using CbmEngine.Systems.Text;
using CbmEngine.Systems.Video;
using CbmEngine.Pipeline;
using Xunit;

namespace CbmEngine.Tests.Unit.CbmFr;

// FR-CBM-LAYOUT-001 (CBMFR-007): mixed-mode (raster-split) layout helper.
[Trait("Speed", "Fast")]
public class ScreenLayoutTests
{
    private static CompiledScreenLayout Canonical() =>
        new ScreenLayout.Builder()
            .AddCharBand(3)
            .AddBitmapBand(19, multicolor: true)
            .AddCharBand(3)
            .Build(bank: 1);

    [Fact]
    public void TEST_CBM_050_RowsMustSumTo25()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new ScreenLayout.Builder().AddCharBand(3).AddBitmapBand(10, true).Build(bank: 1));
        Assert.Contains("25", ex.Message);
    }

    [Fact]
    public void TEST_CBM_051_AllocatesNonOverlappingAddresses()
    {
        var layout = Canonical();
        var r = layout.Regions;
        Assert.Equal(3, r.Count);

        // Distinct screen bases.
        var screens = r.Select(x => x.ScreenBase).ToArray();
        Assert.Equal(screens.Length, screens.Distinct().Count());

        var bitmap = r.Single(x => x.Kind == ScreenRegionKind.BitmapBand).BitmapBase!.Value;
        var charset = r.First(x => x.Kind == ScreenRegionKind.CharBand).CharsetBase!.Value;

        // No screen RAM (1KB) overlaps the bitmap (8KB) or the charset (2KB).
        foreach (var region in r)
        {
            Assert.False(Overlaps(region.ScreenBase, 1000, bitmap, 8000));
            Assert.False(Overlaps(region.ScreenBase, 1000, charset, 2048));
        }
        Assert.False(Overlaps(charset, 2048, bitmap, 8000));
    }

    [Fact]
    public void TEST_CBM_052_SplitRasterLinesAndRegisterWrites()
    {
        var layout = Canonical();
        Assert.Equal(51, layout.Regions[0].StartRasterLine);
        Assert.Equal(75, layout.Regions[1].StartRasterLine);   // 51 + 3*8
        Assert.Equal(227, layout.Regions[2].StartRasterLine);  // 51 + 22*8

        Assert.Equal(new[] { 51, 75, 227 }, layout.SteadyState.Lines.OrderBy(x => x).ToArray());

        foreach (var line in new[] { 51, 75, 227 })
        {
            Assert.True(layout.SteadyState.TryGet(line, out var w));
            Assert.Contains(w, x => x.Address == Vic.D011);
            Assert.Contains(w, x => x.Address == Vic.D016);
            Assert.Contains(w, x => x.Address == Vic.D018);
        }
    }

    [Fact]
    public void TEST_CBM_053_PerSplitRegisterValues()
    {
        var layout = Canonical();

        // Bitmap band at raster 75: BMM set, multicolor D016, bitmap D018.
        Assert.True(layout.SteadyState.TryGet(75, out var bmp));
        Assert.Equal(0x3B, bmp.First(x => x.Address == Vic.D011).Value);
        Assert.Equal(0xD8, bmp.First(x => x.Address == Vic.D016).Value);
        Assert.Equal(0x18, bmp.First(x => x.Address == Vic.D018).Value);

        // Char band at raster 51: text mode, hi-res D016, char D018 (screen nibble 0 + charset $1800).
        Assert.True(layout.SteadyState.TryGet(51, out var ch));
        Assert.Equal(0x1B, ch.First(x => x.Address == Vic.D011).Value);
        Assert.Equal(0xC8, ch.First(x => x.Address == Vic.D016).Value);
        Assert.Equal(0x06, ch.First(x => x.Address == Vic.D018).Value);
    }

    [Fact]
    public void TEST_CBM_055_RegionsUsableByTextAndPump_NoCollision()
    {
        var layout = Canonical();
        var mem = new RecordingMemory();

        var charBand = layout.Regions[0];
        var bitmapBand = layout.Regions.Single(x => x.Kind == ScreenRegionKind.BitmapBand);

        // Write text into the char band.
        TextService.Write(mem, charBand.ScreenBase, charBand.ColorBase, col: 0, row: 0, text: "HI", color: 1);
        byte hCode = mem.Ram[charBand.ScreenBase];   // 'H' => 8

        // Pump a bitmap frame into the bitmap band's allocated memory.
        var cfg = BitmapFramePumpConfig.Default with
        {
            BitmapBase = bitmapBand.BitmapBase!.Value,
            ScreenBase = bitmapBand.ScreenBase,
            ColorBase = bitmapBand.ColorBase,
        };
        var frame = MakeFrame();
        new BitmapFramePump(cfg).Pump(mem, frame);

        // The char band's text RAM was not clobbered by the bitmap pump.
        Assert.Equal(hCode, mem.Ram[charBand.ScreenBase]);
        Assert.Equal(8, mem.Ram[charBand.ScreenBase]);
    }

    [Fact]
    public void TEST_CBM_056_TooManyBands_Throws()
    {
        var b = new ScreenLayout.Builder()
            .AddCharBand(1).AddCharBand(1).AddCharBand(1).AddCharBand(1)
            .AddCharBand(1).AddCharBand(1).AddCharBand(19);   // 7 bands, rows sum 25
        Assert.Throws<ArgumentException>(() => b.Build(bank: 1));
    }

    private static EncodedSplashBitmap MakeFrame()
    {
        var b = new byte[EncodedSplashBitmap.BitmapByteSize];
        var s = new byte[EncodedSplashBitmap.ScreenRamSize];
        var c = new byte[EncodedSplashBitmap.ColorRamSize];
        Array.Fill(b, (byte)0x55);
        return new EncodedSplashBitmap(SplashBitmapMode.Multicolor, 0, b, s, c);
    }

    private static bool Overlaps(int aStart, int aLen, int bStart, int bLen) =>
        aStart < bStart + bLen && bStart < aStart + aLen;
}
