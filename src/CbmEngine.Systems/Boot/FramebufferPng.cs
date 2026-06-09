using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace CbmEngine.Systems.Boot;

public static class FramebufferPng
{
    public static void Write(string path, ReadOnlySpan<byte> bgra, int width, int height)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (bgra.Length != width * height * 4)
            throw new ArgumentException($"BGRA buffer length {bgra.Length} does not match width*height*4 = {width * height * 4}.", nameof(bgra));

        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var image = Image.LoadPixelData<Bgra32>(bgra, width, height);
        image.SaveAsPng(path);
    }
}
