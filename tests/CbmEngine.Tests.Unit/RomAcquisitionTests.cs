using System.Net.Http;
using CbmEngine.Systems.Strategy;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace CbmEngine.Tests.Unit;

/// <summary>
/// Byrd TDD tests for download-on-demand ROM acquisition. The orchestration is tested against a substituted
/// <see cref="IRomAcquirer"/> (no network): it downloads every missing C64 ROM, skips present ones, and
/// surfaces download failures. Also pins the per-user cache base.
/// </summary>
[Trait("Speed", "Fast")]
public class RomAcquisitionTests
{
    [Fact]
    public async Task TEST_ROM_001_EnsureC64Roms_DownloadsEachMissingRom()
    {
        var acquirer = Substitute.For<IRomAcquirer>();
        acquirer.IsAvailable(Arg.Any<string>(), "C64").Returns(false);
        acquirer.DownloadAsync(Arg.Any<string>(), "C64", Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

        await RomAcquisition.EnsureC64RomsAsync(acquirer);

        foreach (var name in new[] { "basic", "kernal", "characters" })
        {
            await acquirer.Received(1).DownloadAsync(name, "C64", Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task TEST_ROM_002_EnsureC64Roms_SkipsDownloadWhenPresent()
    {
        var acquirer = Substitute.For<IRomAcquirer>();
        acquirer.IsAvailable(Arg.Any<string>(), "C64").Returns(true);

        await RomAcquisition.EnsureC64RomsAsync(acquirer);

        await acquirer.DidNotReceive()
                      .DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TEST_ROM_003_EnsureC64Roms_PropagatesDownloadFailure()
    {
        var acquirer = Substitute.For<IRomAcquirer>();
        acquirer.IsAvailable(Arg.Any<string>(), "C64").Returns(false);
        acquirer.DownloadAsync(Arg.Any<string>(), "C64", Arg.Any<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("download failed"));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => RomAcquisition.EnsureC64RomsAsync(acquirer));
    }

    [Fact]
    public void TEST_ROM_004_DefaultCacheBase_IsUnderLocalAppData()
    {
        var path = RomCache.DefaultBasePath;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.StartsWith(localAppData, path);
        Assert.EndsWith(Path.Combine("CbmEngine", "roms"), path);
    }

    // The locator caches downloads under the logical role key (basic/kernal/characters), but the C64
    // machine profile validates + loads ROMs by their canonical VICE filename (C64ViceRomNames). These
    // pin the logical -> canonical bridge that lets a downloaded ROM satisfy the machine build.

    [Theory]
    [InlineData("basic", "basic-901226-01.bin")]
    [InlineData("kernal", "kernal-901227-03.bin")]
    [InlineData("characters", "chargen-901225-01.bin")]
    public void TEST_ROM_005_CanonicalC64FileName_MapsLogicalRoleToViceFilename(string logical, string expected)
    {
        Assert.Equal(expected, RomAcquisition.CanonicalC64FileName(logical));
    }

    [Fact]
    public void TEST_ROM_005b_CanonicalC64FileName_PassesThroughUnmappedNames()
    {
        // Already-canonical and unknown names are returned unchanged (no double-mapping, no throw).
        Assert.Equal("basic-901226-01.bin", RomAcquisition.CanonicalC64FileName("basic-901226-01.bin"));
        Assert.Equal("mystery", RomAcquisition.CanonicalC64FileName("mystery"));
    }

    [Fact]
    public void TEST_ROM_006_MaterializeCanonicalC64_CopiesLogicalCacheFileToViceFilename()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cbmrom-canon-" + Guid.NewGuid().ToString("N"));
        try
        {
            var archDir = Path.Combine(baseDir, "C64");
            Directory.CreateDirectory(archDir);
            var payload = new byte[] { 1, 2, 3, 4 };
            File.WriteAllBytes(Path.Combine(archDir, "basic"), payload);

            RomAcquisition.MaterializeCanonicalC64(baseDir, "C64", "basic");

            var canonical = Path.Combine(archDir, "basic-901226-01.bin");
            Assert.True(File.Exists(canonical));
            Assert.Equal(payload, File.ReadAllBytes(canonical));
        }
        finally
        {
            try { Directory.Delete(baseDir, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void TEST_ROM_006b_MaterializeCanonicalC64_NoThrowWhenLogicalCacheFileMissing()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cbmrom-canon-" + Guid.NewGuid().ToString("N"));
        try
        {
            // No source cache file present: a no-op, not a throw, and nothing is created.
            RomAcquisition.MaterializeCanonicalC64(baseDir, "C64", "basic");
            Assert.False(File.Exists(Path.Combine(baseDir, "C64", "basic-901226-01.bin")));
        }
        finally
        {
            try { Directory.Delete(baseDir, recursive: true); } catch { /* best effort */ }
        }
    }
}
