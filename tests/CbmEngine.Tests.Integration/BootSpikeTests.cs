using System.Security.Cryptography;
using CbmEngine.Systems.Boot;
using ViceSharp.Architectures.C64;
using ViceSharp.RomFetch;
using Xunit;

namespace CbmEngine.Tests.Integration;

[Trait("Speed", "Slow")]
public class BootSpikeTests
{
    private const int LightBluePaletteIndex = 14;
    private const int WarmupFrames = 120;

    private static string RepoRoot => FindRepoRoot();

    public static string RepoRootPublic => FindRepoRoot();

    private static string ArtifactsDir => Path.Combine(RepoRoot, "artifacts", "phase0");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CbmEngine.slnx")))
            dir = dir.Parent;
        if (dir is null)
            throw new InvalidOperationException("Could not find CbmEngine.slnx from test base directory.");
        return dir.FullName;
    }

    [Fact]
    public void TEST_CBM_BOOT_001_ReadyScreen_AppearsAfterWarmup_AndIsWrittenToPng()
    {
        var roms = Helpers.TestRomProvider.Create();
        Directory.CreateDirectory(ArtifactsDir);

        var result = BootRunner.Run(C64MachineProfiles.C64Pal, roms, WarmupFrames);

        Assert.Equal(384, result.Width);
        Assert.Equal(272, result.Height);
        Assert.Equal(result.Width * result.Height * 4, result.FrameBuffer.Length);

        int lightBlueInBand = PaletteAssertions.CountPixelsOfIndex(
            result.FrameBuffer, result.Width, result.Height, LightBluePaletteIndex, yMin: 60, yMax: 80);
        Assert.True(lightBlueInBand > 50, $"Expected >50 light-blue (index 14) pixels in Y=[60,80]; got {lightBlueInBand}.");

        var pngPath = Path.Combine(ArtifactsDir, "ready.png");
        FramebufferPng.Write(pngPath, result.FrameBuffer, result.Width, result.Height);
        Assert.True(File.Exists(pngPath));
        Assert.True(new FileInfo(pngPath).Length > 100);
    }

    [Fact]
    public void TEST_CBM_BOOT_003_BaselineFrameHash_IsDeterministic()
    {
        var roms = Helpers.TestRomProvider.Create();
        Directory.CreateDirectory(ArtifactsDir);

        string Hash()
        {
            var r = BootRunner.Run(C64MachineProfiles.C64Pal, roms, WarmupFrames);
            return Convert.ToHexString(SHA256.HashData(r.FrameBuffer));
        }

        var h1 = Hash();
        var h2 = Hash();
        var h3 = Hash();

        Assert.Equal(h1, h2);
        Assert.Equal(h2, h3);

        File.WriteAllText(Path.Combine(ArtifactsDir, "ready.sha256"), h1);
    }
}
