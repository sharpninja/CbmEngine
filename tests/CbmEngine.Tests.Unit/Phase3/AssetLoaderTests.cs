using CbmEngine.Pipeline;
using CbmEngine.Systems.Assets;
using Xunit;

namespace CbmEngine.Tests.Unit.Phase3;

[Trait("Speed", "Fast")]
public class AssetLoaderTests
{
    [Fact]
    public void CompiledCharset_RoundTripsBytesAndModes()
    {
        var glyphs = new byte[2048];
        for (int i = 0; i < glyphs.Length; i++) glyphs[i] = (byte)(i & 0xFF);
        var modes = new CharMode[256];
        for (int i = 0; i < modes.Length; i++) modes[i] = (i & 1) == 0 ? CharMode.HiRes : CharMode.Multicolor;
        var original = new CompiledCharset { GlyphBytes = glyphs, ModeBySlot = modes };
        var bytes = original.Serialize();
        var loaded = AssetLoader.DeserializeCharset(bytes);

        Assert.Equal(glyphs, loaded.GlyphBytes);
        Assert.Equal(modes, loaded.ModeBySlot);
    }

    [Fact]
    public void CompiledTilemap_RoundTrips()
    {
        var charsetA = new byte[2048];
        for (int i = 0; i < charsetA.Length; i++) charsetA[i] = (byte)i;
        var charsetB = new byte[2048];
        for (int i = 0; i < charsetB.Length; i++) charsetB[i] = (byte)(0xFF - i);

        var original = new CompiledTilemap
        {
            ScreenRam = new byte[] { 1, 2, 3, 4 },
            ColorRam = new byte[] { 5, 6, 7, 8 },
            SplitTable = new (int, byte)[] { (51, 0x18), (155, 0x1A) },
            CharsetsPerBand = new[] { charsetA, charsetB }
        };
        var bytes = original.Serialize();
        var loaded = AssetLoader.DeserializeTilemap(bytes);

        Assert.Equal(original.ScreenRam, loaded.ScreenRam);
        Assert.Equal(original.ColorRam, loaded.ColorRam);
        Assert.Equal(original.SplitTable, loaded.SplitTable);
        Assert.Equal(charsetA, loaded.CharsetsPerBand[0]);
        Assert.Equal(charsetB, loaded.CharsetsPerBand[1]);
    }

    [Fact]
    public void DeserializeCharset_BadMagic_Throws()
    {
        var bad = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 };
        Assert.Throws<InvalidDataException>(() => AssetLoader.DeserializeCharset(bad));
    }
}
