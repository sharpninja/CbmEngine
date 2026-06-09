namespace CbmEngine.Pipeline;

public enum CharMode : byte
{
    HiRes = 0,
    Multicolor = 1
}

public sealed record ScreenColorConfig(byte BackgroundD021, byte McBackgroundD022, byte McBorderD023);
