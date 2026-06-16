using ViceSharp.Abstractions;
using ViceSharp.RomFetch;

namespace CbmEngine.Systems.Strategy;

/// <summary>
/// Locates the bundled VICE ROM data directory so each host does not reinvent path-walking.
/// Resolution order: the <see cref="RomBaseEnvVar"/> environment variable, then a walk up to the
/// repository solution file (<c>CbmEngine.slnx</c>), then a direct walk up looking for the data dir.
/// </summary>
public static class RomDiscovery
{
    /// <summary>Environment variable that, when set to an existing directory, overrides discovery.</summary>
    public const string RomBaseEnvVar = "CBMENGINE_ROM_BASE";

    private static readonly string[] DataDirParts =
        { "external", "vice-sharp", "native", "vice", "vice", "data" };

    /// <summary>Resolve the ROM base directory. <paramref name="startDir"/> defaults to the app base dir.</summary>
    public static string DiscoverRomBase(string? startDir = null)
    {
        var env = Environment.GetEnvironmentVariable(RomBaseEnvVar);
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return env;

        var start = startDir ?? AppContext.BaseDirectory;
        var relativeDataDir = Path.Combine(DataDirParts);

        // Prefer a tree rooted at the solution file.
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CbmEngine.slnx")))
            {
                var candidate = Path.Combine(dir.FullName, relativeDataDir);
                if (Directory.Exists(candidate)) return candidate;
            }
        }

        // Fallback: walk up looking for the data dir directly.
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relativeDataDir);
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new InvalidOperationException(
            $"Could not locate the bundled VICE ROM data directory starting from '{start}'. " +
            $"Set the {RomBaseEnvVar} environment variable to a ROM base directory, or run from within the CbmEngine repository.");
    }

    /// <summary>Discover the ROM base and return an <see cref="IRomProvider"/> rooted there.</summary>
    public static IRomProvider Discover(string? startDir = null) => new RomProvider(DiscoverRomBase(startDir));
}
