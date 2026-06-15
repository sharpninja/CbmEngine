using CbmEngine.Pipeline;
using CbmEngine.Pipeline.CbmVid;
using CbmEngine.Systems.Video;
using Xunit;

namespace CbmEngine.Tests.Unit.Phase8;

[Trait("Speed", "Fast")]
public class CbmVidFormatTests
{
    private static EncodedSplashBitmap MakeFrame(SplashBitmapMode mode, byte fill, byte bg)
    {
        var bitmap = new byte[EncodedSplashBitmap.BitmapByteSize];
        var screen = new byte[EncodedSplashBitmap.ScreenRamSize];
        var color = new byte[EncodedSplashBitmap.ColorRamSize];
        Array.Fill(bitmap, fill);
        Array.Fill(screen, (byte)(fill ^ 0xA5));
        Array.Fill(color, (byte)(fill ^ 0x5A));
        return new EncodedSplashBitmap(mode, bg, bitmap, screen, color);
    }

    [Fact]
    public void TEST_CBM_VID_001_WriteThenRead_ThreeSyntheticFrames_RoundtripExact()
    {
        var f0 = MakeFrame(SplashBitmapMode.Multicolor, 0x11, 0x00);
        var f1 = MakeFrame(SplashBitmapMode.HiRes, 0x22, 0x06);
        var f2 = MakeFrame(SplashBitmapMode.Multicolor, 0x33, 0x01);

        using var ms = new MemoryStream();
        var header = new CbmVidHeader(320, 200, 50, FrameCount: 3, CbmVidFrameMode.Multicolor, Flags: 0);
        using (var writer = new CbmVidWriter(ms, header, leaveOpen: true))
        {
            writer.WriteFrame(f0);
            writer.WriteFrame(f1);
            writer.WriteFrame(f2);
        }

        Assert.Equal(64 + 3 * 10004, ms.Length);

        ms.Position = 0;
        using var player = new VideoPlayer(ms, leaveOpen: true);
        Assert.Equal(3u, player.Header.FrameCount);
        Assert.Equal(50, player.Header.FrameRate);
        Assert.Equal(CbmVidFrameMode.Multicolor, player.Header.DefaultMode);

        var read0 = player.PeekFrame(0);
        var read1 = player.PeekFrame(1);
        var read2 = player.PeekFrame(2);

        AssertFrameEqual(f0, read0);
        AssertFrameEqual(f1, read1);
        AssertFrameEqual(f2, read2);
    }

    [Fact]
    public void Header_RejectsBadMagic()
    {
        var bytes = new byte[64 + 10004];
        bytes[0] = (byte)'X';
        using var ms = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() => new VideoPlayer(ms));
    }

    [Fact]
    public void Header_RejectsUnsupportedVersion()
    {
        using var ms = new MemoryStream();
        var header = new CbmVidHeader(320, 200, 50, FrameCount: 0, CbmVidFrameMode.Multicolor, 0);
        using (var w = new CbmVidWriter(ms, header, leaveOpen: true)) { }
        ms.Position = 7;
        ms.WriteByte(0x99);
        ms.Position = 0;
        Assert.Throws<NotSupportedException>(() => new VideoPlayer(ms));
    }

    private static void AssertFrameEqual(EncodedSplashBitmap expected, EncodedSplashBitmap actual)
    {
        Assert.Equal(expected.Mode, actual.Mode);
        Assert.Equal(expected.BackgroundColorIndex, actual.BackgroundColorIndex);
        Assert.True(expected.Bitmap.AsSpan().SequenceEqual(actual.Bitmap), "Bitmap mismatch");
        Assert.True(expected.ScreenRam.AsSpan().SequenceEqual(actual.ScreenRam), "ScreenRam mismatch");
        Assert.True(expected.ColorRam.AsSpan().SequenceEqual(actual.ColorRam), "ColorRam mismatch");
    }
}
