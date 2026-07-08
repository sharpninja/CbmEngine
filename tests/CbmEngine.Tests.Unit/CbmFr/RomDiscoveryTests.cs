using CbmEngine.Systems.Strategy;
using Xunit;

namespace CbmEngine.Tests.Unit.CbmFr;

// FR-CBM-ROM-001 (CBMFR-006): ROM base resolution. After de-vendoring vice-sharp, resolution is:
// the CBMENGINE_ROM_BASE env var if it points at an existing directory, otherwise the per-user download
// cache (RomCache.DefaultBasePath). It no longer walks to a vice-sharp source checkout and no longer
// throws when nothing is found (the download-on-demand path populates the cache).
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

    [Fact]
    public void TEST_CBM_013_EnvVar_Wins()
    {
        var envDir = Path.Combine(Path.GetTempPath(), "cbmrom-env-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(envDir);
        try
        {
            Environment.SetEnvironmentVariable(RomDiscovery.RomBaseEnvVar, envDir);
            Assert.Equal(envDir, RomDiscovery.DiscoverRomBase());
        }
        finally { Directory.Delete(envDir, true); }
    }

    [Fact]
    public void TEST_CBM_014_NoEnvVar_FallsBackToPerUserCache()
    {
        // Env cleared by the ctor. No throw; returns the per-user cache base regardless of start dir.
        var start = Path.Combine(Path.GetTempPath(), "cbmrom-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(start);
        try
        {
            Assert.Equal(RomCache.DefaultBasePath, RomDiscovery.DiscoverRomBase(start));
        }
        finally { Directory.Delete(start, true); }
    }

    [Fact]
    public void TEST_CBM_016_EnvVarMissingDir_Ignored_FallsBackToCache()
    {
        var missing = Path.Combine(Path.GetTempPath(), "cbmrom-missing-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(RomDiscovery.RomBaseEnvVar, missing);
        Assert.Equal(RomCache.DefaultBasePath, RomDiscovery.DiscoverRomBase());
    }
}
