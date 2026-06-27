using CbmEngine.Abstractions;
using ViceSharp.Abstractions;
using ViceSharp.Architectures.C64;
using ViceSharp.Core;

namespace CbmEngine.Systems.Strategy;

public static class CommodoreSystem
{
    public static readonly IReadOnlyList<string> SupportedProfileIds = new[] { "c64", "c64c", "ntsc", "newntsc" };

    /// <summary>Build a machine using ROMs auto-discovered via <see cref="RomDiscovery"/>.</summary>
    public static ICommodoreMachine Build(string profileId) => Build(profileId, RomDiscovery.Discover());

    public static ICommodoreMachine Build(string profileId, IRomProvider roms)
        => Build(profileId, roms, audioBackend: null);

    /// <summary>
    /// Build a machine with an optional live-audio backend. When supplied, the SID streams samples to
    /// it during emulation (the builder also runs <c>ConfigureAudioClock</c> for the profile's master
    /// clock), so a host that advances RunFrame at the machine's refresh rate gets correctly paced
    /// audio. Pass null for a silent machine (parity / headless rigs).
    /// </summary>
    public static ICommodoreMachine Build(string profileId, IRomProvider roms, IAudioBackend? audioBackend)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(roms);

        var profile = ResolveProfile(profileId);
        var descriptor = new C64Descriptor(profile);
        var machine = new ArchitectureBuilder(roms, audioBackend).Build(descriptor);
        return new CommodoreMachine(machine, profile);
    }

    public static C64MachineProfile ResolveProfile(string profileId)
    {
        if (C64MachineProfiles.TryResolve(profileId, out var profile) &&
            SupportedProfileIds.Contains(profileId, StringComparer.OrdinalIgnoreCase))
        {
            return profile;
        }

        throw new ArgumentException(
            $"Unknown or unsupported CbmEngine profile '{profileId}'. Supported v1 ids: {string.Join(", ", SupportedProfileIds)}.",
            nameof(profileId));
    }
}
