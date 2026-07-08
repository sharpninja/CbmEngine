using CbmEngine.Systems.Strategy;
using ViceSharp.RomFetch;

namespace CbmEngine.Tools.CbmVidStudio;

/// <summary>
/// ROM base-path resolution for the studio/tool, in priority order:
/// 1. CBMVID_ROM_BASE environment variable (explicit user override)
/// 2. roms/ directory bundled next to the tool assembly
/// 3. The per-user download cache (<see cref="RomCache.DefaultBasePath"/>) if ROMs were already fetched.
/// <see cref="Resolve"/> returns null when nothing usable is found (callers degrade);
/// <see cref="ResolveOrDownloadAsync"/> downloads the ROMs on demand and always returns a base path.
/// </summary>
public static class StudioRoms
{
    public const string EnvVar = "CBMVID_ROM_BASE";

    public static string? Resolve()
    {
        var env = Environment.GetEnvironmentVariable(EnvVar);
        if (!string.IsNullOrWhiteSpace(env) && HasC64Roms(env)) return env;

        var bundled = Path.Combine(AppContext.BaseDirectory, "roms");
        if (HasC64Roms(bundled)) return bundled;

        var cache = RomCache.DefaultBasePath;
        return HasC64Roms(cache) ? cache : null;
    }

    /// <summary>
    /// Resolve a ROM base path, downloading the missing C64 ROMs into the per-user cache when none are
    /// found locally. Always returns a usable base path (throws only if the download itself fails).
    /// </summary>
    public static async Task<string> ResolveOrDownloadAsync(CancellationToken cancellationToken = default)
    {
        var existing = Resolve();
        if (existing is not null) return existing;

        var cache = RomCache.DefaultBasePath;
        await RomAcquisition.EnsureC64RomsAsync(new RomProviderAcquirer(new RomProvider(cache)), cancellationToken);
        return cache;
    }

    /// <summary>Cheap availability probe: the C64 BASIC ROM (canonical name or alias) must exist.</summary>
    private static bool HasC64Roms(string basePath)
    {
        var c64 = Path.Combine(basePath, "C64");
        return File.Exists(Path.Combine(c64, "basic-901226-01.bin"))
            || File.Exists(Path.Combine(c64, "basic"));
    }
}
