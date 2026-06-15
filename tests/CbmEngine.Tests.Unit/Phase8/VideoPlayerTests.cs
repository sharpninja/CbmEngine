using CbmEngine.Pipeline;
using CbmEngine.Pipeline.CbmVid;
using CbmEngine.Systems.Video;
using Xunit;

namespace CbmEngine.Tests.Unit.Phase8;

[Trait("Speed", "Fast")]
public class VideoPlayerTests
{
    private static EncodedSplashBitmap MakeFrame(SplashBitmapMode mode, byte fill, byte bg = 0)
    {
        var bitmap = new byte[EncodedSplashBitmap.BitmapByteSize];
        var screen = new byte[EncodedSplashBitmap.ScreenRamSize];
        var color = new byte[EncodedSplashBitmap.ColorRamSize];
        Array.Fill(bitmap, fill);
        Array.Fill(screen, (byte)(fill ^ 0xA5));
        Array.Fill(color, (byte)(fill ^ 0x5A));
        return new EncodedSplashBitmap(mode, bg, bitmap, screen, color);
    }

    private static MemoryStream BuildStream(params EncodedSplashBitmap[] frames)
    {
        var ms = new MemoryStream();
        var header = new CbmVidHeader(320, 200, 50, (uint)frames.Length, CbmVidFrameMode.Multicolor, 0);
        using (var w = new CbmVidWriter(ms, header, leaveOpen: true))
            foreach (var f in frames) w.WriteFrame(f);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void TEST_CBM_VID_006_Truncated_ThrowsWithFrameIndexAndShortRead()
    {
        var f0 = MakeFrame(SplashBitmapMode.Multicolor, 0x11);
        var f1 = MakeFrame(SplashBitmapMode.Multicolor, 0x22);
        var f2 = MakeFrame(SplashBitmapMode.Multicolor, 0x33);
        var ms = BuildStream(f0, f1, f2);
        var truncated = new MemoryStream(ms.GetBuffer(), 0, (int)ms.Length - 100, writable: false);

        using var player = new VideoPlayer(truncated);
        var memory = new RecordingMemoryService();

        Assert.True(player.PumpFrame(memory));   // frame 0 ok
        Assert.True(player.PumpFrame(memory));   // frame 1 ok
        var ex = Assert.Throws<InvalidDataException>(() => player.PumpFrame(memory));
        Assert.Contains("frame 2", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("truncated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TEST_CBM_VID_007_Loop_WrapsToFrameZero()
    {
        var f0 = MakeFrame(SplashBitmapMode.Multicolor, 0xAA);
        var f1 = MakeFrame(SplashBitmapMode.Multicolor, 0xBB);
        var ms = BuildStream(f0, f1);
        using var player = new VideoPlayer(ms) { Loop = true };
        var memory = new RecordingMemoryService();

        Assert.True(player.PumpFrame(memory));   // 0
        Assert.True(player.PumpFrame(memory));   // 1
        Assert.True(player.IsFinished);          // before wrap on next pump
        Assert.True(player.PumpFrame(memory));   // wraps -> deposits frame 0 again
        Assert.Equal(1, player.CurrentFrame);    // after pumping wrapped-0, next is 1
        Assert.False(player.IsFinished);
    }

    [Fact]
    public void TEST_CBM_VID_104_ModeFlip_RoutesD016ThroughWriteIo()
    {
        var fMc = MakeFrame(SplashBitmapMode.Multicolor, 0x11);
        var fHr = MakeFrame(SplashBitmapMode.HiRes, 0x22);
        var ms = BuildStream(fMc, fHr, fMc);
        using var player = new VideoPlayer(ms);
        var memory = new RecordingMemoryService();

        player.PumpFrame(memory);    // MC: write $D016 = $D8
        player.PumpFrame(memory);    // HR: write $D016 = $C8
        player.PumpFrame(memory);    // MC: write $D016 = $D8

        var d016Writes = memory.IoWrites.Where(w => w.Address == 0xD016).ToArray();
        Assert.Equal(3, d016Writes.Length);
        Assert.Equal(0xD8, d016Writes[0].Value);
        Assert.Equal(0xC8, d016Writes[1].Value);
        Assert.Equal(0xD8, d016Writes[2].Value);
    }

    [Fact]
    public void Pump_BitmapScreenColor_RoutedToCorrectAddresses()
    {
        var f0 = MakeFrame(SplashBitmapMode.Multicolor, 0x11);
        var ms = BuildStream(f0);
        using var player = new VideoPlayer(ms);
        var memory = new RecordingMemoryService();

        player.PumpFrame(memory);

        var ranges = memory.RangeWrites;
        Assert.Contains(ranges, r => r.Address == 0x6000 && r.Length == 8000);   // bitmap -> WriteRange
        Assert.Contains(ranges, r => r.Address == 0x4400 && r.Length == 1000);   // screen -> WriteRange
        var colorIoWrites = memory.IoWrites.Where(w => w.Address >= 0xD800 && w.Address <= 0xDBE7).ToArray();
        Assert.Equal(1000, colorIoWrites.Length);                                // color -> WriteIo (bus)
    }

    [Fact]
    public void PumpFrame_AfterAllFrames_NonLoop_ReturnsFalseAndIsFinished()
    {
        var f0 = MakeFrame(SplashBitmapMode.Multicolor, 0x11);
        var ms = BuildStream(f0);
        using var player = new VideoPlayer(ms);
        var memory = new RecordingMemoryService();

        Assert.True(player.PumpFrame(memory));
        Assert.True(player.IsFinished);
        Assert.False(player.PumpFrame(memory));
    }
}
