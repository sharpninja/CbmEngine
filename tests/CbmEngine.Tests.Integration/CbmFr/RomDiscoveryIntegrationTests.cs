using CbmEngine.Systems.Strategy;
using Xunit;

namespace CbmEngine.Tests.Integration.CbmFr;

// FR-CBM-ROM-001 (CBMFR-006): ROM discovery against the real bundled VICE ROMs.
[Trait("Speed", "Slow")]
public class RomDiscoveryIntegrationTests
{
    [Fact]
    public void TEST_CBM_017_Discover_ProviderHasC64Roms()
    {
        var roms = RomDiscovery.Discover();
        Assert.True(roms.IsAvailable("kernal", "C64"));
        Assert.True(roms.IsAvailable("basic", "C64"));
        Assert.True(roms.IsAvailable("characters", "C64"));
    }

    [Fact]
    public void TEST_CBM_018_BuildC64_NoRomsArg_BuildsMachine()
    {
        var sys = CommodoreSystem.Build("c64");
        Assert.NotNull(sys);
        sys.RunFrame();
    }
}
