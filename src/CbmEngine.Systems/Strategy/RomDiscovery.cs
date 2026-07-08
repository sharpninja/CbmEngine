using ViceSharp.Abstractions;
using ViceSharp.RomFetch;

namespace CbmEngine.Systems.Strategy;

/// <summary>
/// Resolves the C64 ROM base directory and, on demand, downloads missing ROMs into it.
/// Resolution order: the <see cref="RomBaseEnvVar"/> environment variable if it points at an existing
/// directory, otherwise the per-user cache (<see cref="RomCache.DefaultBasePath"/>) which the
/// download-on-demand path populates. ROMs are no longer taken from a vice-sharp source checkout.
/// </summary>
public static class RomDiscovery
{
    /// <summary>Environment variable that, when set to an existing directory, overrides discovery.</summary>
    public const string RomBaseEnvVar = "CBMENGINE_ROM_BASE";

    /// <summary>Resolve the ROM base directory (env override, else the per-user cache). Never throws.</summary>
    public static string DiscoverRomBase(string? startDir = null)
    {
        var env = Environment.GetEnvironmentVariable(RomBaseEnvVar);
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return env;

        return RomCache.DefaultBasePath;
    }

    /// <summary>Discover the ROM base and return an <see cref="IRomProvider"/> rooted there (no download).</summary>
    public static IRomProvider Discover(string? startDir = null) => new RomProvider(DiscoverRomBase(startDir));

    /// <summary>Resolve the ROM base and download any missing C64 ROMs into it; returns the base directory.</summary>
    public static async Task<string> EnsureRomBaseAsync(
        string? startDir = null, CancellationToken cancellationToken = default)
    {
        var baseDir = DiscoverRomBase(startDir);
        await RomAcquisition.EnsureC64RomsAsync(new RomProviderAcquirer(new RomProvider(baseDir)), cancellationToken);
        return baseDir;
    }

    /// <summary>
    /// Resolve the ROM base, download any missing C64 ROMs into it via the locator, and return a provider
    /// rooted there. This is the reliable path for hosts that may run without ROMs already present.
    /// </summary>
    public static async Task<IRomProvider> DiscoverOrDownloadAsync(
        string? startDir = null, CancellationToken cancellationToken = default)
        => new RomProvider(await EnsureRomBaseAsync(startDir, cancellationToken));
}
