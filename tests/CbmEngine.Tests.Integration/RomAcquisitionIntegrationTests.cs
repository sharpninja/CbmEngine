using CbmEngine.Systems.Strategy;
using ViceSharp.RomFetch;
using Xunit;

namespace CbmEngine.Tests.Integration;

/// <summary>
/// Slow, network-gated: exercises real download-on-demand. Against an empty base the acquirer downloads
/// + caches the 3 C64 ROMs (available afterward), and a second pass is a cache hit (no throw).
/// </summary>
[Trait("Speed", "Slow")]
public sealed class RomAcquisitionIntegrationTests
{
    [Fact]
    public async Task TEST_ROM_INT_001_EnsureC64Roms_DownloadsThenCaches()
    {
        var tempBase = Path.Combine(Path.GetTempPath(), "cbmrom-dl-" + Guid.NewGuid().ToString("N"));
        try
        {
            var provider = new RomProvider(tempBase);
            var acquirer = new RomProviderAcquirer(provider);

            // Empty base -> download.
            await RomAcquisition.EnsureC64RomsAsync(acquirer);

            Assert.True(provider.IsAvailable("basic", "C64"));
            Assert.True(provider.IsAvailable("kernal", "C64"));
            Assert.True(provider.IsAvailable("characters", "C64"));

            // Downloaded ROMs load as real bytes, and the emulator boots from them.
            Assert.False(provider.LoadRom("kernal", "C64").IsEmpty);
            var machine = CommodoreSystem.Build("c64", provider);
            machine.RunFrame();

            // Second pass is a cache hit (no re-download, no throw).
            await RomAcquisition.EnsureC64RomsAsync(acquirer);
        }
        finally
        {
            try { Directory.Delete(tempBase, recursive: true); } catch { /* best effort */ }
        }
    }
}
