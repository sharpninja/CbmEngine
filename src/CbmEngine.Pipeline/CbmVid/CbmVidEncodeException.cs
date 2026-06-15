namespace CbmEngine.Pipeline.CbmVid;

public sealed class CbmVidEncodeException : Exception
{
    public int FrameIndex { get; }
    public string? PngPath { get; }

    public CbmVidEncodeException(int frameIndex, string? pngPath, string message, Exception? inner = null)
        : base(message, inner)
    {
        FrameIndex = frameIndex;
        PngPath = pngPath;
    }
}
