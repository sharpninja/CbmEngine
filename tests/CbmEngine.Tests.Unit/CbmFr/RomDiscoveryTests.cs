using CbmEngine.Systems.Strategy;
using Xunit;

namespace CbmEngine.Tests.Unit.CbmFr;

// FR-CBM-ROM-001 (CBMFR-006): ROM base resolution helper. Unit-level (temp trees + env var).
[Trait("Speed", "Fast")]
public sealed class RomDiscoveryTests : IDisposable
{
    private readonly string? _savedEnv;

    public RomDiscoveryTests()
    {
        _savedEnv = Environment.GetEnvironmentVariable(RomDiscovery.RomBaseEnvVar);
        Environment.SetEnvironmentVariable(RomDiscovery.RomBaseEnvVar, null);
    }

    public void Dispose() => Environment.SetEnvironmentVariable(RomDiscovery.RomBaseEnvVar, _savedEnv);

    private static string MakeTree(bool withSlnx)
    {
        var root = Path.Combine(Path.GetTempPath(), "cbmrom-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "external", "vice-sharp", "native", "vice", "vice", "data"));
        if (withSlnx) File.WriteAllText(Path.Combine(root, "CbmEngine.slnx"), "<Solution/>");
        return root;
    }

    private static string DataDir(string root) =>
        Path.Combine(root, "external", "vice-sharp", "native", "vice", "vice", "data");

    [Fact]
    public void TEST_CBM_013_EnvVar_Wins()
    {
        var envDir = Path.Combine(Path.GetTempPath(), "cbmrom-env-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(envDir);
        var tree = MakeTree(withSlnx: true);
        try
        {
            Environment.SetEnvironmentVariable(RomDiscovery.RomBaseEnvVar, envDir);
            var start = Path.Combine(tree, "a", "b");
            Directory.CreateDirectory(start);
            Assert.Equal(envDir, RomDiscovery.DiscoverRomBase(start));
        }
        finally
        {
            Directory.Delete(envDir, true);
            Directory.Delete(tree, true);
        }
    }

    [Fact]
    public void TEST_CBM_014_FindsViaSlnxWalk()
    {
        var tree = MakeTree(withSlnx: true);
        try
        {
            var start = Path.Combine(tree, "src", "bin", "Debug");
            Directory.CreateDirectory(start);
            Assert.Equal(DataDir(tree), RomDiscovery.DiscoverRomBase(start));
        }
        finally { Directory.Delete(tree, true); }
    }

    [Fact]
    public void TEST_CBM_015_FallbackFindsDataDirWithoutSlnx()
    {
        var tree = MakeTree(withSlnx: false);
        try
        {
            var start = Path.Combine(tree, "x", "y");
            Directory.CreateDirectory(start);
            Assert.Equal(DataDir(tree), RomDiscovery.DiscoverRomBase(start));
        }
        finally { Directory.Delete(tree, true); }
    }

    [Fact]
    public void TEST_CBM_016_NothingFound_Throws()
    {
        var empty = Path.Combine(Path.GetTempPath(), "cbmrom-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => RomDiscovery.DiscoverRomBase(empty));
            Assert.Contains(RomDiscovery.RomBaseEnvVar, ex.Message);
        }
        finally { Directory.Delete(empty, true); }
    }

    [Fact]
    public void TEST_CBM_019_SharedResolver_FindsRepoDataDir()
    {
        // Program.FindRomBase delegates to this shared resolver; from the test bin it resolves the repo data dir.
        var baseDir = RomDiscovery.DiscoverRomBase();
        Assert.True(Directory.Exists(baseDir));
        Assert.EndsWith(Path.Combine("vice", "vice", "data"), baseDir);
    }
}
