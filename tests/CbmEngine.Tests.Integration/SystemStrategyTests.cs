using System.Security.Cryptography;
using CbmEngine.Abstractions;
using CbmEngine.Systems.Strategy;
using CbmEngine.Tests.Integration.Helpers;
using ViceSharp.Abstractions;
using Xunit;

namespace CbmEngine.Tests.Integration;

[Trait("Speed", "Slow")]
public class SystemStrategyTests
{
    [Theory]
    [InlineData("c64", VideoStandard.Pal, 63, 312)]
    [InlineData("c64c", VideoStandard.Pal, 63, 312)]
    [InlineData("ntsc", VideoStandard.Ntsc, 65, 263)]
    [InlineData("newntsc", VideoStandard.Ntsc, 65, 263)]
    public void TEST_CBM_SYS_001_AllFourProfiles_HaveExpectedTimingGeometry(string id, VideoStandard std, int cyclesPerLine, int rasterLines)
    {
        var roms = TestRomProvider.Create();
        var sys = CommodoreSystem.Build(id, roms);

        Assert.Equal(std, sys.Capabilities.VideoStandard);
        Assert.Equal(cyclesPerLine, sys.Capabilities.CyclesPerLine);
        Assert.Equal(rasterLines, sys.Capabilities.RasterLines);
        Assert.NotNull(sys.VideoChip);
        Assert.NotNull(sys.AudioChip);
        Assert.NotNull(sys.KeyboardMatrix);
        Assert.NotNull(sys.Memory);
        Assert.NotNull(sys.Sound);
    }

    [Fact]
    public void TEST_CBM_SYS_002_PalAndNtscBoot_ProduceDistinctFramebufferHashes()
    {
        var roms = TestRomProvider.Create();
        var pal = CommodoreSystem.Build("c64", roms);
        var ntsc = CommodoreSystem.Build("ntsc", roms);
        for (int i = 0; i < 120; i++) { pal.RunFrame(); ntsc.RunFrame(); }

        string palSha = Convert.ToHexString(SHA256.HashData(pal.VideoChip.FrameBuffer));
        string ntscSha = Convert.ToHexString(SHA256.HashData(ntsc.VideoChip.FrameBuffer));

        Assert.NotEqual(palSha, ntscSha);
    }

    [Fact]
    public void TEST_CBM_SYS_003_AllProfiles_ShareIdenticalFramebufferDimensions()
    {
        var roms = TestRomProvider.Create();
        var built = CommodoreSystem.SupportedProfileIds.Select(id => CommodoreSystem.Build(id, roms)).ToList();
        var first = built[0];
        var fw = first.VideoChip.FrameWidth;
        var fh = first.VideoChip.FrameHeight;
        foreach (var sys in built)
        {
            sys.RunFrame();
            Assert.Equal(fw, sys.VideoChip.FrameWidth);
            Assert.Equal(fh, sys.VideoChip.FrameHeight);
            Assert.Equal(fw * fh * 4, sys.VideoChip.FrameBuffer.Length);
        }
    }

    [Fact]
    public void TEST_CBM_SYS_004_UnknownProfile_ThrowsArgumentException()
    {
        var roms = TestRomProvider.Create();
        var ex = Assert.Throws<ArgumentException>(() => CommodoreSystem.Build("c128", roms));
        Assert.Contains("c128", ex.Message);
        Assert.Contains("c64", ex.Message);
    }

    [Fact]
    public void Sound_ResolvesByProfile()
    {
        var roms = TestRomProvider.Create();
        Assert.Equal(SidModel.Mos6581, CommodoreSystem.Build("c64", roms).Sound.Model);
        Assert.Equal(SidModel.Mos8580, CommodoreSystem.Build("c64c", roms).Sound.Model);
        Assert.Equal(SidModel.Mos6581, CommodoreSystem.Build("ntsc", roms).Sound.Model);
        Assert.Equal(SidModel.Mos8580, CommodoreSystem.Build("newntsc", roms).Sound.Model);
    }
}
