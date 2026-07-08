namespace CbmEngine.Systems.Strategy;

/// <summary>
/// Ensures the C64 ROMs the emulator needs are present, downloading and caching any that are missing via
/// the ViceSharp locator. Downloads use the locator's logical role keys (<c>basic</c>/<c>kernal</c>/
/// <c>characters</c> - the <c>RomProvider.RomDatabase</c> keys), but the C64 machine profile validates and
/// loads ROMs by their canonical VICE filename (<c>C64ViceRomNames</c>, e.g. <c>basic-901226-01.bin</c>).
/// The locator caches under the role key, so after each download the cached file is also materialized under
/// its canonical filename (<see cref="MaterializeCanonicalC64"/>) so the machine build resolves it.
/// </summary>
public static class RomAcquisition
{
    /// <summary>The VICE architecture key for the Commodore 64.</summary>
    public const string C64Architecture = "C64";

    /// <summary>Logical C64 ROM roles the emulator loads and the locator can download.</summary>
    public static readonly IReadOnlyList<string> C64RomNames = new[] { "basic", "kernal", "characters" };

    /// <summary>
    /// Serializes acquisition process-wide so concurrent callers (e.g. parallel test classes, or several
    /// components starting at once) don't race on the shared cache - the download writes and the canonical
    /// copy are not safe to interleave. The first caller populates the cache; the rest hit it and no-op.
    /// </summary>
    private static readonly SemaphoreSlim AcquisitionGate = new(1, 1);

    /// <summary>Download any missing C64 ROMs via <paramref name="acquirer"/>; no-op for those already present.</summary>
    public static async Task EnsureC64RomsAsync(IRomAcquirer acquirer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(acquirer);

        await AcquisitionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var romName in C64RomNames)
            {
                if (!acquirer.IsAvailable(romName, C64Architecture))
                {
                    await acquirer.DownloadAsync(romName, C64Architecture, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            AcquisitionGate.Release();
        }
    }

    /// <summary>
    /// The canonical on-disk VICE filename the emulator's C64 machine profile validates and loads for a
    /// logical role key (e.g. <c>basic</c> -> <c>basic-901226-01.bin</c>). Mirrors ViceSharp's
    /// <c>C64ViceRomNames</c>. Names that are already canonical or unknown are returned unchanged.
    /// </summary>
    public static string CanonicalC64FileName(string romName) => romName switch
    {
        "basic" => "basic-901226-01.bin",
        "kernal" => "kernal-901227-03.bin",
        "characters" => "chargen-901225-01.bin",
        _ => romName,
    };

    /// <summary>
    /// After a download the locator has cached <paramref name="romName"/> under its logical role key; the
    /// C64 machine profile resolves ROMs by their canonical VICE filename. Copy the cached file to that
    /// filename so <c>IsAvailable</c>/<c>LoadRom</c> resolve it. No-op when the name is already canonical,
    /// the source cache file is absent, or the canonical file already exists.
    /// </summary>
    public static void MaterializeCanonicalC64(string basePath, string architecture, string romName)
    {
        var canonical = CanonicalC64FileName(romName);
        if (string.Equals(canonical, romName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var archDir = Path.Combine(basePath, architecture);
        var source = Path.Combine(archDir, romName);
        var destination = Path.Combine(archDir, canonical);
        if (File.Exists(source) && !File.Exists(destination))
        {
            File.Copy(source, destination);
        }
    }
}
