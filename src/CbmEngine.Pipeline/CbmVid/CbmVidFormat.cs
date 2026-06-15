namespace CbmEngine.Pipeline.CbmVid;

public enum CbmVidFrameMode : byte
{
    Multicolor = 0,
    HiRes = 1,
}

public readonly record struct CbmVidHeader(
    ushort Width,
    ushort Height,
    ushort FrameRate,
    uint FrameCount,
    CbmVidFrameMode DefaultMode,
    byte Flags);

public static class CbmVidFormat
{
    public static readonly byte[] Magic = "CBMVID\0"u8.ToArray();
    public const byte Version = 0x01;
    public const int HeaderSize = 64;
    public const int FrameRecordSize = 10004;
    public const int FrameBitmapOffset = 4;
    public const int FrameBitmapSize = 8000;
    public const int FrameScreenOffset = 8004;
    public const int FrameScreenSize = 1000;
    public const int FrameColorOffset = 9004;
    public const int FrameColorSize = 1000;
}
