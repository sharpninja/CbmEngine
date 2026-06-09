using CbmEngine.Pipeline;
using CbmEngine.Systems.Audio;

namespace CbmEngine.Systems.Cartridge;

public static class PsidPlayerCart
{
    public static byte[] Build(
        PsidProgram program,
        byte backgroundColor = 0x01,
        byte initialBorderColor = 0x00,
        int borderCyclePeriodFrames = 50,
        EncodedSplashBitmap? splash = null,
        Ca65Assembler? assembler = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (program.Payload.Length > 0x1700)
            throw new ArgumentException($"PSID payload of {program.Payload.Length} bytes exceeds the slot reserved after the splash + screen + color blocks.", nameof(program));

        assembler ??= new Ca65Assembler();
        string source = PsidPlayerCartSource.BuildSource(program, backgroundColor, initialBorderColor, borderCyclePeriodFrames, splash);
        var includes = new Dictionary<string, byte[]>
        {
            [PsidPlayerCartSource.PayloadFileName] = program.Payload.ToArray(),
        };
        if (splash is not null)
        {
            includes[PsidPlayerCartSource.BitmapFileName] = splash.Bitmap;
            includes[PsidPlayerCartSource.ScreenFileName] = splash.ScreenRam;
            includes[PsidPlayerCartSource.ColorFileName] = splash.ColorRam;
        }
        else
        {
            includes[PsidPlayerCartSource.BitmapFileName] = Array.Empty<byte>();
            includes[PsidPlayerCartSource.ScreenFileName] = Array.Empty<byte>();
            includes[PsidPlayerCartSource.ColorFileName] = Array.Empty<byte>();
        }

        var image = assembler.Build(source, PsidPlayerCartSource.LinkerConfig, includes);
        if (image.Length != CartridgeImage.Size16K)
            throw new InvalidOperationException($"Assembled cart is {image.Length} bytes; expected {CartridgeImage.Size16K}.");
        return image;
    }
}
