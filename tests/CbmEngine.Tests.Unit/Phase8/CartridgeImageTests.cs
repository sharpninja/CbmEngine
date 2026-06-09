using CbmEngine.Systems.Cartridge;
using Xunit;

namespace CbmEngine.Tests.Unit.Phase8;

[Trait("Speed", "Fast")]
public class CartridgeImageTests
{
    [Fact]
    public void Build16K_ProducesFixedSize()
    {
        var img = CartridgeImage.Build16K(new byte[] { 0x60 });
        Assert.Equal(0x4000, img.Length);
    }

    [Fact]
    public void Build16K_PlacesColdAndWarmVectors_FollowedByCbm80Signature()
    {
        var img = CartridgeImage.Build16K(new byte[] { 0x60 });
        Assert.Equal(0x09, img[0]);
        Assert.Equal(0x80, img[1]);
        Assert.Equal(0x09, img[2]);
        Assert.Equal(0x80, img[3]);
        Assert.Equal(0xC3, img[4]);
        Assert.Equal(0xC2, img[5]);
        Assert.Equal(0xCD, img[6]);
        Assert.Equal(0x38, img[7]);
        Assert.Equal(0x30, img[8]);
    }

    [Fact]
    public void Build16K_CodeLandsAtOffset9()
    {
        var img = CartridgeImage.Build16K(new byte[] { 0xA9, 0x42, 0x60 });
        Assert.Equal(0xA9, img[9]);
        Assert.Equal(0x42, img[10]);
        Assert.Equal(0x60, img[11]);
    }

    [Fact]
    public void Build16K_OverlongCode_Throws()
    {
        var tooBig = new byte[CartridgeImage.Size16K];
        Assert.Throws<ArgumentException>(() => CartridgeImage.Build16K(tooBig));
    }

    [Fact]
    public void BuildMarkerOnly16K_ContainsCbm80AndCodeAt9()
    {
        var img = BootstrapCart.BuildMarkerOnly16K();
        Assert.Equal(0xC3, img[4]);
        Assert.Equal(0x78, img[9]);
    }
}
