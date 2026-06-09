using CbmEngine.Systems.Native;
using Xunit;

namespace CbmEngine.Tests.Unit.Phase5;

[Trait("Speed", "Fast")]
public class IrqPlayerStubTests
{
    [Fact]
    public void Build_ProducesFixedSize()
    {
        var bytes = IrqPlayerStub.Build(stubBase: 0xC000, playAddress: 0x1003);
        Assert.Equal(IrqPlayerStub.Size, bytes.Length);
    }

    [Fact]
    public void Build_InitBlock_InstallsIrqVectorPointingAtHandler()
    {
        var bytes = IrqPlayerStub.Build(stubBase: 0xC000, playAddress: 0x1003);
        Assert.Equal(0x78, bytes[0]);
        Assert.Equal(0xA9, bytes[1]);
        Assert.Equal(0x10, bytes[2]);
        Assert.Equal(0x8D, bytes[3]); Assert.Equal(0x14, bytes[4]); Assert.Equal(0x03, bytes[5]);
        Assert.Equal(0xA9, bytes[6]);
        Assert.Equal(0xC0, bytes[7]);
        Assert.Equal(0x8D, bytes[8]); Assert.Equal(0x15, bytes[9]); Assert.Equal(0x03, bytes[10]);
        Assert.Equal(0x58, bytes[11]);
        Assert.Equal(0x60, bytes[12]);
    }

    [Fact]
    public void Build_Handler_AcksVicAndJsrsPlayAndChainsToKernal()
    {
        var bytes = IrqPlayerStub.Build(stubBase: 0xC000, playAddress: 0x1003);
        int h = IrqPlayerStub.HandlerOffset;
        Assert.Equal(0xA9, bytes[h + 0]); Assert.Equal(0x01, bytes[h + 1]);
        Assert.Equal(0x8D, bytes[h + 2]); Assert.Equal(0x19, bytes[h + 3]); Assert.Equal(0xD0, bytes[h + 4]);
        Assert.Equal(0x20, bytes[h + 5]); Assert.Equal(0x03, bytes[h + 6]); Assert.Equal(0x10, bytes[h + 7]);
        Assert.Equal(0x4C, bytes[h + 8]); Assert.Equal(0x31, bytes[h + 9]); Assert.Equal(0xEA, bytes[h + 10]);
    }

    [Fact]
    public void Build_RespectsAlternateStubBase()
    {
        var bytes = IrqPlayerStub.Build(stubBase: 0xC800, playAddress: 0x1003);
        Assert.Equal(0xC8, bytes[7]);
    }

    [Fact]
    public void Build_EncodesPlayAddressLittleEndian()
    {
        var bytes = IrqPlayerStub.Build(stubBase: 0xC000, playAddress: 0x40FE);
        int h = IrqPlayerStub.HandlerOffset;
        Assert.Equal(0xFE, bytes[h + 6]);
        Assert.Equal(0x40, bytes[h + 7]);
    }
}
