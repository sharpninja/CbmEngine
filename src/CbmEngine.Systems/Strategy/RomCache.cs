namespace CbmEngine.Systems.Strategy;

/// <summary>
/// Stable per-user cache directory for VICE ROMs acquired at runtime (download-on-demand).
/// The ROM locator caches to <c>&lt;base&gt;/&lt;architecture&gt;/&lt;romName&gt;</c>, so C64 ROMs land in
/// <c>&lt;DefaultBasePath&gt;/C64/</c>.
/// </summary>
public static class RomCache
{
    /// <summary><c>%LOCALAPPDATA%/CbmEngine/roms</c> - the ROM base the download-on-demand path caches into.</summary>
    public static string DefaultBasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CbmEngine",
        "roms");
}
