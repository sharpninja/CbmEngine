using ViceSharp.RomFetch;

namespace CbmEngine.Tests.Integration.Helpers;

public static class TestRomProvider
{
    public static string RomBasePath =>
        Path.Combine(BootSpikeTests.RepoRootPublic, "external", "vice-sharp", "native", "vice", "vice", "data");

    public static RomProvider Create() => new(RomBasePath);
}
