using CbmEngine.Systems.Audio;
using CbmEngine.Tests.Shared.Helpers;
using Xunit;

namespace CbmEngine.Tests.Unit.Phase5;

[Trait("Speed", "Fast")]
public class PsidLoaderTests
{
    [Fact]
    public void TEST_CBM_PSID_001_Load_ValidPsid_ReturnsParsedHeader()
    {
        var bytes = PsidFixtures.BuildSyntheticPsid();
        using var ms = new MemoryStream(bytes);
        var prog = PsidLoader.Load(ms);

        Assert.Equal("PSID", prog.Header.Magic);
        Assert.Equal(2, prog.Header.Version);
        Assert.Equal(0x1000, prog.Header.LoadAddress);
        Assert.Equal(0x1000, prog.Header.InitAddress);
        Assert.Equal(0x1003, prog.Header.PlayAddress);
        Assert.Equal(1, prog.Header.SongCount);
        Assert.Equal(1, prog.Header.StartSong);
        Assert.Equal("Test", prog.Header.Name);
        Assert.True(prog.Payload.Length > 0);
    }

    [Fact]
    public void TEST_CBM_PSID_002_Load_TruncatedStream_Throws()
    {
        var bytes = PsidFixtures.BuildSyntheticPsid();
        using var ms = new MemoryStream(bytes.AsSpan(0, 10).ToArray());
        var ex = Assert.Throws<PsidFormatException>(() => PsidLoader.Load(ms));
        Assert.Contains("Truncated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TEST_CBM_PSID_003_Load_WrongMagic_ThrowsWithObservedBytes()
    {
        var bytes = PsidFixtures.BuildSyntheticPsid(magic: "BAD!");
        using var ms = new MemoryStream(bytes);
        var ex = Assert.Throws<PsidFormatException>(() => PsidLoader.Load(ms));
        Assert.Equal("BAD!", ex.ObservedMagic);
    }

    [Fact]
    public void Load_RsidMagic_AlsoAccepted()
    {
        var bytes = PsidFixtures.BuildSyntheticPsid(magic: "RSID");
        using var ms = new MemoryStream(bytes);
        var prog = PsidLoader.Load(ms);
        Assert.Equal("RSID", prog.Header.Magic);
    }
}
