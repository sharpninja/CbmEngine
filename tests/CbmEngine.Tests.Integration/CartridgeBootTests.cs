using CbmEngine.Systems.Cartridge;
using CbmEngine.Systems.Strategy;
using CbmEngine.Tests.Integration.Helpers;
using Xunit;

namespace CbmEngine.Tests.Integration;

[Trait("Speed", "Slow")]
public class CartridgeBootTests
{
    private readonly ITestOutputHelper _out;
    public CartridgeBootTests(ITestOutputHelper output) { _out = output; }

    [Fact]
    public void CartridgeBoot_MarkerOnly_DepositsMarkerWithinFewFrames()
    {
        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        var cart = BootstrapCart.BuildMarkerOnly16K();
        var result = CartridgeBoot.AttachAndWaitForMarker(sys, cart, maxFrames: 60);

        _out.WriteLine($"Marker seen after {result.FramesUntilMarker} frames (success={result.MarkerSeen}).");
        Assert.True(result.MarkerSeen);
        Assert.Equal(BootstrapCart.MarkerHi, sys.Bus.Read(BootstrapCart.MarkerAddress));
        Assert.Equal(BootstrapCart.MarkerLo, sys.Bus.Read((ushort)(BootstrapCart.MarkerAddress + 1)));
    }

    [Fact]
    public void CartridgeBoot_AppliesBorderAndBackgroundColorsFromBootstrap()
    {
        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        var cart = BootstrapCart.BuildMarkerOnly16K(borderColor: 0x02, backgroundColor: 0x07);
        CartridgeBoot.AttachAndWaitForMarker(sys, cart, maxFrames: 60);

        Assert.Equal(0x02, sys.Bus.Read(0xD020) & 0x0F);
        Assert.Equal(0x07, sys.Bus.Read(0xD021) & 0x0F);
    }
}
