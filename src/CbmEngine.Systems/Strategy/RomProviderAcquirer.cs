using ViceSharp.RomFetch;

namespace CbmEngine.Systems.Strategy;

/// <summary>
/// Adapts the concrete ViceSharp <see cref="RomProvider"/> (whose <c>DownloadRom</c> is not on
/// <see cref="ViceSharp.Abstractions.IRomProvider"/>) to <see cref="IRomAcquirer"/>.
/// </summary>
public sealed class RomProviderAcquirer : IRomAcquirer
{
    private readonly RomProvider _provider;

    public RomProviderAcquirer(RomProvider provider) => _provider = provider;

    /// <inheritdoc />
    public bool IsAvailable(string romName, string architecture) => _provider.IsAvailable(romName, architecture);

    /// <inheritdoc />
    public async Task DownloadAsync(string romName, string architecture, CancellationToken cancellationToken)
    {
        await _provider.DownloadRom(romName, architecture, cancellationToken);

        // DownloadRom caches under the logical role key; the C64 machine profile resolves ROMs by their
        // canonical VICE filename, so materialize that name too (from the provider's own base path).
        RomAcquisition.MaterializeCanonicalC64(_provider.RomBasePath, architecture, romName);
    }
}
