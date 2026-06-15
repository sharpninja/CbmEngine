namespace CbmEngine.Tools.CbmVidStudio;

/// <summary>
/// ROM base-path resolution for the studio/tool, in priority order:
/// 1. CBMVID_ROM_BASE environment variable (explicit user override)
/// 2. roms/ directory bundled next to the tool assembly (shipped in the nupkg)
/// 3. Walk up from the app base looking for a CbmEngine repo checkout (dev runs)
/// Returns null when nothing usable is found; callers degrade gracefully.
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

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CbmEngine.slnx")))
            {
                var repoRoms = Path.Combine(dir.FullName, "external", "vice-sharp", "native", "vice", "vice", "data");
                return HasC64Roms(repoRoms) ? repoRoms : null;
            }
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>Cheap availability probe: the C64 BASIC ROM (canonical name or alias) must exist.</summary>
    private static bool HasC64Roms(string basePath)
    {
        var c64 = Path.Combine(basePath, "C64");
        return File.Exists(Path.Combine(c64, "basic-901226-01.bin"))
            || File.Exists(Path.Combine(c64, "basic"));
    }
}
