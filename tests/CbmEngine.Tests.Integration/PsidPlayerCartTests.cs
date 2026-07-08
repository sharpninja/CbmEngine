using CbmEngine.Systems.Audio;
using CbmEngine.Systems.Cartridge;
using CbmEngine.Systems.Strategy;
using CbmEngine.Tests.Integration.Helpers;
using CbmEngine.Tests.Shared.Helpers;
using Xunit;

namespace CbmEngine.Tests.Integration;

[Trait("Speed", "Slow")]
public class PsidPlayerCartTests
{
    private readonly ITestOutputHelper _out;
    public PsidPlayerCartTests(ITestOutputHelper output) { _out = output; }

    private static PsidProgram BuildSyntheticPlayingPsid()
    {
        using var ms = new MemoryStream(PsidFixtures.BuildSyntheticPsid());
        return PsidLoader.Load(ms);
    }

    [Fact]
    public void Cart_CarriesEmbeddedPsidPayloadAtPayloadSegment()
    {
        var psid = BuildSyntheticPlayingPsid();
        var cart = PsidPlayerCart.Build(psid);
        Assert.Equal(16384, cart.Length);
        // PAYLOAD segment lives at $AA10 in the cart ROM image (linker config in PsidPlayerCartSource).
        const int PayloadCartOffset = 0xAA10 - 0x8000;
        for (int i = 0; i < psid.Payload.Length; i++)
            Assert.Equal(psid.Payload.Span[i], cart[PayloadCartOffset + i]);
    }

    [Fact]
    public void Cart_BackgroundColorAppliedDuringBootstrap()
    {
        var psid = BuildSyntheticPlayingPsid();
        var cart = PsidPlayerCart.Build(psid, backgroundColor: 0x01);
        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        var result = CartridgeBoot.AttachAndWaitForMarker(sys, cart, maxFrames: 300);
        Assert.True(result.MarkerSeen);
        Assert.Equal(0x01, sys.Bus.Read(0xD021) & 0x0F);
    }

    [Fact]
    public void Cart_CopiesEmbeddedPsidIntoRamAtLoadAddress()
    {
        var psid = BuildSyntheticPlayingPsid();
        var cart = PsidPlayerCart.Build(psid);
        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        CartridgeBoot.AttachAndWaitForMarker(sys, cart, maxFrames: 300);

        for (int i = 0; i < psid.Payload.Length; i++)
            Assert.Equal(psid.Payload.Span[i], sys.Bus.Read((ushort)(psid.Header.LoadAddress + i)));
    }

    [Fact]
    public void Cart_RunsPsidInitDuringBootstrap_AndPlayViaCia1Irq()
    {
        var psid = BuildSyntheticPlayingPsid();
        var cart = PsidPlayerCart.Build(psid);
        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        var result = CartridgeBoot.AttachAndWaitForMarker(sys, cart, maxFrames: 300);
        Assert.True(result.MarkerSeen);

        sys.Memory.WriteIo(0xD418, new byte[] { 0 });
        for (int i = 0; i < 60; i++) sys.RunFrame();
        byte vol = (byte)(sys.Bus.Read(0xD418) & 0x0F);
        _out.WriteLine($"$D418 low nibble after 60 frames = 0x{vol:X2}");
        Assert.Equal(0x0F, vol);
    }

    [Fact]
    public void Cart_BorderCyclerAdvancesD020OncePerPeriod()
    {
        var psid = BuildSyntheticPlayingPsid();
        var cart = PsidPlayerCart.Build(psid, initialBorderColor: 0x00, borderCyclePeriodFrames: 10);
        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        CartridgeBoot.AttachAndWaitForMarker(sys, cart, maxFrames: 300);

        byte borderInitial = (byte)(sys.Bus.Read(0xD020) & 0x0F);
        for (int i = 0; i < 60; i++) sys.RunFrame();
        byte borderAfter = (byte)(sys.Bus.Read(0xD020) & 0x0F);

        _out.WriteLine($"border initial=0x{borderInitial:X2} after-60-frames=0x{borderAfter:X2}");
        Assert.NotEqual(borderInitial, borderAfter);
    }
}
