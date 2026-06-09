using CbmEngine.Abstractions;

namespace CbmEngine.Tests.Shared.Helpers;

public sealed class FakeBlitTarget : IBlitTarget
{
    public int UploadCount { get; private set; }
    public int LastWidth { get; private set; }
    public int LastHeight { get; private set; }
    public byte[]? LastBuffer { get; private set; }

    public void Upload(ReadOnlySpan<byte> bgra, int width, int height)
    {
        UploadCount++;
        LastWidth = width;
        LastHeight = height;
        LastBuffer = bgra.ToArray();
    }
}
