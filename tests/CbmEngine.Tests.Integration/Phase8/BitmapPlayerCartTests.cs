using CbmEngine.Pipeline;
using CbmEngine.Systems.Cartridge;
using CbmEngine.Systems.Strategy;
using CbmEngine.Tests.Integration.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace CbmEngine.Tests.Integration.Phase8;

[Trait("Speed", "Slow")]
public class BitmapPlayerCartTests
{
    private readonly ITestOutputHelper _out;
    public BitmapPlayerCartTests(ITestOutputHelper output) { _out = output; }

    private static EncodedSplashBitmap BlankSplash(SplashBitmapMode mode = SplashBitmapMode.Multicolor, byte bg = 0)
    {
        var bitmap = new byte[EncodedSplashBitmap.BitmapByteSize];
        var screen = new byte[EncodedSplashBitmap.ScreenRamSize];
        var color = new byte[EncodedSplashBitmap.ColorRamSize];
        Array.Fill(screen, (byte)0x10);
        Array.Fill(color, (byte)0x01);
        return new EncodedSplashBitmap(mode, bg, bitmap, screen, color);
    }

    [Fact]
    public void TEST_CBM_VID_010_BuildAndAttach_DepositsMarkerAndBitmapModeBits_Multicolor()
    {
        var splash = BlankSplash(SplashBitmapMode.Multicolor, bg: 0);
        var cart = BitmapPlayerCart.Build(splash);
        Assert.Equal(16384, cart.Length);

        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        var result = CartridgeBoot.AttachAndWaitForMarker(sys, cart, maxFrames: 600);
        Assert.True(result.MarkerSeen, $"Marker not deposited within 600 frames (saw at frame {result.FramesUntilMarker})");

        Assert.Equal(0x3B, sys.Bus.Read(0xD011));
        Assert.Equal(0x18, sys.Bus.Read(0xD018) & 0xFE);   // bit 0 of $D018 is unused and reads back as 1 on real hw
        Assert.True((sys.Bus.Read(0xD016) & 0x10) == 0x10, $"$D016 expected MCM=1; got ${sys.Bus.Read(0xD016):X2}");
        _out.WriteLine($"Marker hit at frame {result.FramesUntilMarker}. D011=${sys.Bus.Read(0xD011):X2} D016=${sys.Bus.Read(0xD016):X2} D018=${sys.Bus.Read(0xD018):X2}");
    }

    [Fact]
    public void BuildAndAttach_HiResSplash_SetsD016WithoutMcmBit()
    {
        var splash = BlankSplash(SplashBitmapMode.HiRes, bg: 0);
        var cart = BitmapPlayerCart.Build(splash);
        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        var result = CartridgeBoot.AttachAndWaitForMarker(sys, cart, maxFrames: 600);
        Assert.True(result.MarkerSeen);
        Assert.Equal(0x00, sys.Bus.Read(0xD016) & 0x10);
    }
}
