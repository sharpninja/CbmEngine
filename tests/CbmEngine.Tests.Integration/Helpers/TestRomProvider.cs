using CbmEngine.Systems.Strategy;
using ViceSharp.RomFetch;

namespace CbmEngine.Tests.Integration.Helpers;

public static class TestRomProvider
{
    public static string RomBasePath => RomCache.DefaultBasePath;

    /// <summary>
    /// Provider rooted at the per-user cache, with the C64 ROMs downloaded on demand (from the VICE
    /// mirror via the locator) if they are not already cached. Replaces the old vice-sharp submodule path.
    /// </summary>
    public static RomProvider Create()
    {
        var provider = new RomProvider(RomBasePath);
        RomAcquisition.EnsureC64RomsAsync(new RomProviderAcquirer(provider)).GetAwaiter().GetResult();
        return provider;
    }
}
