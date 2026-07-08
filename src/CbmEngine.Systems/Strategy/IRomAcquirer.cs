namespace CbmEngine.Systems.Strategy;

/// <summary>
/// Testable seam over the ViceSharp ROM locator: check availability and download-on-demand.
/// <see cref="ViceSharp.Abstractions.IRomProvider"/> exposes only load/availability, not download, and the
/// concrete <c>RomProvider.DownloadRom</c> is not mockable - so the acquisition orchestration depends on this
/// interface and can be unit-tested without network.
/// </summary>
public interface IRomAcquirer
{
    /// <summary>Whether the named ROM is already present (and valid) locally for the architecture.</summary>
    bool IsAvailable(string romName, string architecture);

    /// <summary>Download the named ROM for the architecture and cache it locally.</summary>
    Task DownloadAsync(string romName, string architecture, CancellationToken cancellationToken);
}
