namespace CbmEngine.Abstractions;

public interface IBlitTarget
{
    void Upload(ReadOnlySpan<byte> bgra, int width, int height);
}
