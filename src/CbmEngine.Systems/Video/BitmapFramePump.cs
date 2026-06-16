using CbmEngine.Abstractions;
using CbmEngine.Pipeline;

namespace CbmEngine.Systems.Video;

/// <summary>VIC-bank addresses and mode register values used by <see cref="BitmapFramePump"/>.</summary>
public readonly record struct BitmapFramePumpConfig(
    ushort BitmapBase,
    ushort ScreenBase,
    ushort ColorBase,
    ushort D016Address,
    byte MulticolorD016,
    byte HiResD016)
{
    /// <summary>Engine defaults: bitmap $6000, screen $4400, colour $D800, $D016 = $D8 (mc) / $C8 (hi-res).</summary>
    public static BitmapFramePumpConfig Default { get; } =
        new(0x6000, 0x4400, 0xD800, 0xD016, 0xD8, 0xC8);
}

/// <summary>
/// Pushes an arbitrary in-memory bitmap frame (bitmap to RAM, screen to RAM, colour via IO, mode to
/// $D016) into a machine's memory, independent of any stream source. The $D016 mode byte is only
/// written when the mode changes between consecutive frames; the first frame always writes it.
/// </summary>
public sealed class BitmapFramePump
{
    private const byte ModeSentinel = 0xFF;

    private readonly BitmapFramePumpConfig _config;
    private readonly byte[] _d016Buf = new byte[1];
    private byte _lastModeByte = ModeSentinel;

    public BitmapFramePump(BitmapFramePumpConfig? config = null) =>
        _config = config ?? BitmapFramePumpConfig.Default;

    public BitmapFramePumpConfig Config => _config;

    /// <summary>Reset mode tracking so the next pump re-writes $D016 (use after a stream rewind/loop).</summary>
    public void Reset() => _lastModeByte = ModeSentinel;

    /// <summary>Pump an encoded frame into memory.</summary>
    public void Pump(IMemoryService memory, EncodedSplashBitmap frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        Pump(memory, frame.Mode, frame.Bitmap, frame.ScreenRam, frame.ColorRam);
    }

    /// <summary>Pump raw frame spans into memory (alloc-free; used by stream players).</summary>
    public void Pump(
        IMemoryService memory,
        SplashBitmapMode mode,
        ReadOnlySpan<byte> bitmap,
        ReadOnlySpan<byte> screen,
        ReadOnlySpan<byte> color)
    {
        ArgumentNullException.ThrowIfNull(memory);

        byte modeByte = (byte)(mode == SplashBitmapMode.HiRes ? 1 : 0);
        if (modeByte != _lastModeByte)
        {
            _d016Buf[0] = mode == SplashBitmapMode.HiRes ? _config.HiResD016 : _config.MulticolorD016;
            memory.WriteIo(_config.D016Address, _d016Buf);
            _lastModeByte = modeByte;
        }

        memory.WriteRange(_config.BitmapBase, bitmap);
        memory.WriteRange(_config.ScreenBase, screen);
        // Colour RAM ($D800-) is bus-mapped in this engine's memory model; route through IO writes.
        memory.WriteIo(_config.ColorBase, color);
    }
}
