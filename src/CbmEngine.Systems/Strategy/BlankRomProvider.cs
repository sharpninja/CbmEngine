using ViceSharp.Abstractions;

namespace CbmEngine.Systems.Strategy;

/// <summary>
/// An <see cref="IRomProvider"/> that supplies zeroed (blank) C64 ROM images. Used to build a ROM-less
/// machine (see <see cref="CommodoreSystem.BuildRomless(string, IAudioBackend?)"/>): the ROM loader's
/// size check still passes, and because the ROM names are non-canonical the loader skips its MD5 check.
/// The blank BASIC/CHARGEN bytes are never sampled by a host-driven multicolour bitmap render (the VIC
/// reads bitmap/screen/colour RAM, not CHARGEN, for a bitmap at $2000), and the KERNAL is not requested.
/// </summary>
internal sealed class BlankRomProvider : IRomProvider
{
    /// <summary>C64 BASIC and KERNAL ROM image size.</summary>
    public const int BasicRomSize = 8192;

    /// <summary>C64 character generator ROM image size.</summary>
    public const int CharacterRomSize = 4096;

    public bool IsAvailable(string romName, string architecture) => true;

    public ReadOnlyMemory<byte> LoadRom(string romName, string architecture)
        => new byte[SizeFor(romName)];

    private static int SizeFor(string romName)
    {
        // Character ROM is 4 KB; everything else (BASIC; KERNAL is never requested under KernalNone) is 8 KB.
        var name = (romName ?? string.Empty).ToLowerInvariant();
        return name.Contains("char") ? CharacterRomSize : BasicRomSize;
    }
}
