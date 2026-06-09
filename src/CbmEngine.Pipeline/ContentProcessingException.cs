namespace CbmEngine.Pipeline;

public class ContentProcessingException : Exception
{
    public ContentProcessingException(string message) : base(message) { }
    public ContentProcessingException(string message, Exception inner) : base(message, inner) { }
}

public sealed class BandedAllocationException : ContentProcessingException
{
    public int OverflowingRowStart { get; }
    public int OverflowingRowEnd { get; }

    public BandedAllocationException(int rowStart, int rowEnd, string message) : base(message)
    {
        OverflowingRowStart = rowStart;
        OverflowingRowEnd = rowEnd;
    }
}
