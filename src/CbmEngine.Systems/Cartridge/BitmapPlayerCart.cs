using CbmEngine.Pipeline;

namespace CbmEngine.Systems.Cartridge;

public static class BitmapPlayerCart
{
    public static byte[] Build(EncodedSplashBitmap initialSplash, Ca65Assembler? assembler = null)
    {
        ArgumentNullException.ThrowIfNull(initialSplash);
        if (initialSplash.Bitmap.Length != EncodedSplashBitmap.BitmapByteSize) throw new ArgumentException("bitmap size mismatch", nameof(initialSplash));
        if (initialSplash.ScreenRam.Length != EncodedSplashBitmap.ScreenRamSize) throw new ArgumentException("screen size mismatch", nameof(initialSplash));
        if (initialSplash.ColorRam.Length != EncodedSplashBitmap.ColorRamSize) throw new ArgumentException("color size mismatch", nameof(initialSplash));

        assembler ??= new Ca65Assembler();
        string source = BitmapPlayerCartSource.BuildSource(initialSplash);
        var includes = new Dictionary<string, byte[]>
        {
            [BitmapPlayerCartSource.BitmapFileName] = initialSplash.Bitmap,
            [BitmapPlayerCartSource.ScreenFileName] = initialSplash.ScreenRam,
            [BitmapPlayerCartSource.ColorFileName] = initialSplash.ColorRam,
        };
        var image = assembler.Build(source, BitmapPlayerCartSource.LinkerConfig, includes);
        if (image.Length != CartridgeImage.Size16K)
            throw new InvalidOperationException($"Assembled cart is {image.Length} bytes; expected {CartridgeImage.Size16K}.");
        return image;
    }
}
