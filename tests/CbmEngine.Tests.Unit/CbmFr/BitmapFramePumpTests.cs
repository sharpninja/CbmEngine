using System.Linq;
using CbmEngine.Pipeline;
using CbmEngine.Pipeline.CbmVid;
using CbmEngine.Systems.Video;
using Xunit;

namespace CbmEngine.Tests.Unit.CbmFr;

// FR-CBM-PUMP-001 (CBMFR-003): general per-frame bitmap pump.
[Trait("Speed", "Fast")]
public class BitmapFramePumpTests
{
    private static EncodedSplashBitmap Frame(SplashBitmapMode mode, byte fill, byte bg = 0)
    {
        var b = new byte[EncodedSplashBitmap.BitmapByteSize];
        var s = new byte[EncodedSplashBitmap.ScreenRamSize];
        var c = new byte[EncodedSplashBitmap.ColorRamSize];
        Array.Fill(b, fill);
        Array.Fill(s, (byte)(fill ^ 0xA5));
        Array.Fill(c, (byte)(fill ^ 0x5A));
        return new EncodedSplashBitmap(mode, bg, b, s, c);
    }

    [Fact]
    public void TEST_CBM_020_Pump_Bitmap_ToBitmapBase()
    {
        var mem = new RecordingMemory();
        new BitmapFramePump().Pump(mem, Frame(SplashBitmapMode.Multicolor, 0x11));
        Assert.Contains(mem.RangeWrites, r => r.Address == 0x6000 && r.Length == 8000);
    }

    [Fact]
    public void TEST_CBM_021_Pump_Screen_ToScreenBase()
    {
        var mem = new RecordingMemory();
        new BitmapFramePump().Pump(mem, Frame(SplashBitmapMode.Multicolor, 0x11));
        Assert.Contains(mem.RangeWrites, r => r.Address == 0x4400 && r.Length == 1000);
    }

    [Fact]
    public void TEST_CBM_022_Pump_Color_ToColorBaseViaIo()
    {
        var mem = new RecordingMemory();
        new BitmapFramePump().Pump(mem, Frame(SplashBitmapMode.Multicolor, 0x11));
        var colorIo = mem.IoWrites.Count(w => w.Address >= 0xD800 && w.Address <= 0xDBE7);
        Assert.Equal(1000, colorIo);
    }

    [Fact]
    public void TEST_CBM_023_Pump_ModeFlip_WritesD016()
    {
        var mem = new RecordingMemory();
        var p = new BitmapFramePump();
        p.Pump(mem, Frame(SplashBitmapMode.Multicolor, 0x11));
        p.Pump(mem, Frame(SplashBitmapMode.HiRes, 0x22));
        var d016 = mem.IoWrites.Where(w => w.Address == 0xD016).ToArray();
        Assert.Equal(2, d016.Length);
        Assert.Equal(0xD8, d016[0].Value);
        Assert.Equal(0xC8, d016[1].Value);
    }

    [Fact]
    public void TEST_CBM_024_Pump_D016_OnlyOnModeChange()
    {
        var mem = new RecordingMemory();
        var p = new BitmapFramePump();
        p.Pump(mem, Frame(SplashBitmapMode.Multicolor, 0x11));
        p.Pump(mem, Frame(SplashBitmapMode.Multicolor, 0x22));
        p.Pump(mem, Frame(SplashBitmapMode.Multicolor, 0x33));
        var d016 = mem.IoWrites.Where(w => w.Address == 0xD016).ToArray();
        Assert.Single(d016);                 // first pump only
        Assert.Equal(0xD8, d016[0].Value);
    }

    [Fact]
    public void TEST_CBM_025_Pump_CustomConfig_Addresses()
    {
        var mem = new RecordingMemory();
        var cfg = BitmapFramePumpConfig.Default with { BitmapBase = 0xA000 };
        new BitmapFramePump(cfg).Pump(mem, Frame(SplashBitmapMode.Multicolor, 0x11));
        Assert.Contains(mem.RangeWrites, r => r.Address == 0xA000 && r.Length == 8000);
    }

    [Fact]
    public void TEST_CBM_026_Pump_NullArgs_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new BitmapFramePump().Pump(null!, Frame(SplashBitmapMode.Multicolor, 1)));
        Assert.Throws<ArgumentNullException>(() => new BitmapFramePump().Pump(new RecordingMemory(), null!));
    }

    [Fact]
    public void TEST_CBM_027_VideoPlayer_DelegatesToPump_SameWrites()
    {
        var frame = Frame(SplashBitmapMode.Multicolor, 0x11);

        // VideoPlayer path.
        var ms = new MemoryStream();
        var header = new CbmVidHeader(320, 200, 50, 1, CbmVidFrameMode.Multicolor, 0);
        using (var w = new CbmVidWriter(ms, header, leaveOpen: true)) w.WriteFrame(frame);
        ms.Position = 0;
        var viaPlayer = new RecordingMemory();
        using (var player = new VideoPlayer(ms)) player.PumpFrame(viaPlayer);

        // Direct pump path.
        var viaPump = new RecordingMemory();
        new BitmapFramePump().Pump(viaPump, frame);

        Assert.Equal(viaPump.RangeWrites, viaPlayer.RangeWrites);
        Assert.Equal(viaPump.IoWrites, viaPlayer.IoWrites);
    }
}
