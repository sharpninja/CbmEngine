using System.Diagnostics;
using CbmEngine.Pipeline;
using CbmEngine.Pipeline.CbmVid;
using CbmEngine.Systems.Cartridge;
using CbmEngine.Systems.Strategy;
using CbmEngine.Systems.Video;
using CbmEngine.Tests.Integration.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace CbmEngine.Tests.Integration.Phase8;

[Trait("Speed", "Slow")]
public class VideoPlayerIntegrationTests
{
    private readonly ITestOutputHelper _out;
    public VideoPlayerIntegrationTests(ITestOutputHelper output) { _out = output; }

    private static EncodedSplashBitmap Frame(SplashBitmapMode mode, byte bitmapFill, byte screenFill, byte colorFill, byte bg = 0)
    {
        var bitmap = new byte[EncodedSplashBitmap.BitmapByteSize];
        var screen = new byte[EncodedSplashBitmap.ScreenRamSize];
        var color = new byte[EncodedSplashBitmap.ColorRamSize];
        Array.Fill(bitmap, bitmapFill);
        Array.Fill(screen, screenFill);
        Array.Fill(color, colorFill);
        return new EncodedSplashBitmap(mode, bg, bitmap, screen, color);
    }

    private static MemoryStream BuildStream(params EncodedSplashBitmap[] frames)
    {
        var ms = new MemoryStream();
        var header = new CbmVidHeader(320, 200, 50, (uint)frames.Length, CbmVidFrameMode.Multicolor, 0);
        using (var w = new CbmVidWriter(ms, header, leaveOpen: true))
            foreach (var f in frames) w.WriteFrame(f);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void TEST_CBM_VID_004_PumpFrame_FiveFrames_RamMatchesExpected()
    {
        var splash = Frame(SplashBitmapMode.Multicolor, 0x00, 0x10, 0x01);
        var f1 = Frame(SplashBitmapMode.Multicolor, 0x11, 0x21, 0x02);
        var f2 = Frame(SplashBitmapMode.Multicolor, 0x22, 0x32, 0x03);
        var f3 = Frame(SplashBitmapMode.Multicolor, 0x33, 0x43, 0x04);
        var f4 = Frame(SplashBitmapMode.Multicolor, 0x44, 0x54, 0x05);
        var f5 = Frame(SplashBitmapMode.Multicolor, 0x55, 0x65, 0x06);

        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        var cart = BitmapPlayerCart.Build(splash);
        Assert.True(CartridgeBoot.AttachAndWaitForMarker(sys, cart, maxFrames: 600).MarkerSeen);

        using var stream = BuildStream(f1, f2, f3, f4, f5);
        using var player = new VideoPlayer(stream);

        EncodedSplashBitmap[] expected = [f1, f2, f3, f4, f5];
        for (int i = 0; i < 5; i++)
        {
            Assert.True(player.PumpFrame(sys.Memory));
            sys.RunFrame();
            // verify bitmap byte 0, screen byte 0, color byte 0 match expected fill
            Assert.Equal(expected[i].Bitmap[0], sys.Bus.Read(0x6000));
            Assert.Equal(expected[i].ScreenRam[0], sys.Bus.Read(0x4400));
            Assert.Equal(expected[i].ColorRam[0], (byte)(sys.Bus.Read(0xD800) & 0x0F));
        }
    }

    [Fact]
    public void TEST_CBM_VID_005_ModeFlip_TogglesD016Bit()
    {
        var splash = Frame(SplashBitmapMode.Multicolor, 0x00, 0x10, 0x01);
        var mc = Frame(SplashBitmapMode.Multicolor, 0xAA, 0x21, 0x02);
        var hr = Frame(SplashBitmapMode.HiRes, 0xBB, 0x31, 0x03);
        var mc2 = Frame(SplashBitmapMode.Multicolor, 0xCC, 0x41, 0x04);

        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        var cart = BitmapPlayerCart.Build(splash);
        Assert.True(CartridgeBoot.AttachAndWaitForMarker(sys, cart, maxFrames: 600).MarkerSeen);

        using var stream = BuildStream(mc, hr, mc2);
        using var player = new VideoPlayer(stream);

        var observed = new byte[3];
        for (int i = 0; i < 3; i++)
        {
            player.PumpFrame(sys.Memory);
            sys.RunFrame();
            observed[i] = (byte)(sys.Bus.Read(0xD016) & 0x10);
        }

        Assert.Equal(0x10, observed[0]);   // MC
        Assert.Equal(0x00, observed[1]);   // HR
        Assert.Equal(0x10, observed[2]);   // MC
    }

    [Fact]
    public void TEST_CBM_VID_009_SixHundredFrames_UnderTwelveSeconds()
    {
        var splash = Frame(SplashBitmapMode.Multicolor, 0x00, 0x10, 0x01);
        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        var cart = BitmapPlayerCart.Build(splash);
        Assert.True(CartridgeBoot.AttachAndWaitForMarker(sys, cart, maxFrames: 600).MarkerSeen);

        var frames = new EncodedSplashBitmap[600];
        for (int i = 0; i < 600; i++) frames[i] = Frame(SplashBitmapMode.Multicolor, (byte)(i & 0xFF), (byte)((i + 1) & 0xFF), (byte)((i + 2) & 0xFF));
        using var stream = BuildStream(frames);
        using var player = new VideoPlayer(stream);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 600; i++) { player.PumpFrame(sys.Memory); sys.RunFrame(); }
        sw.Stop();
        _out.WriteLine($"600 frames in {sw.Elapsed.TotalSeconds:F2}s");
        Assert.True(sw.Elapsed.TotalSeconds <= 12.0, $"600 frames took {sw.Elapsed.TotalSeconds:F2}s; budget is 12s");
    }
}
