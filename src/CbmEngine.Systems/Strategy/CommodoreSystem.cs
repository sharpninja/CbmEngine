using CbmEngine.Abstractions;
using ViceSharp.Abstractions;
using ViceSharp.Architectures.C64;
using ViceSharp.Core;

namespace CbmEngine.Systems.Strategy;

public static class CommodoreSystem
{
    public static readonly IReadOnlyList<string> SupportedProfileIds = new[] { "c64", "c64c", "ntsc", "newntsc" };

    public static ICommodoreMachine Build(string profileId, IRomProvider roms)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(roms);

        var profile = ResolveProfile(profileId);
        var descriptor = new C64Descriptor(profile);
        var machine = new ArchitectureBuilder(roms).Build(descriptor);
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
