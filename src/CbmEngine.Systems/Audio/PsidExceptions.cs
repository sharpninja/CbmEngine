namespace CbmEngine.Systems.Audio;

public class PsidFormatException : Exception
{
    public int OffendingOffset { get; }
    public string? ExpectedMagic { get; }
    public string? ObservedMagic { get; }

    public PsidFormatException(string message, int offset = -1, string? expectedMagic = null, string? observedMagic = null)
        : base(message)
    {
        OffendingOffset = offset;
        ExpectedMagic = expectedMagic;
        ObservedMagic = observedMagic;
    }
}

public class PsidPlacementException : Exception
{
    public PsidPlacementException(string message) : base(message) { }
}

public class PsidExecutionException : Exception
{
    public PsidExecutionException(string message) : base(message) { }
}
