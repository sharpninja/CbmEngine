using CbmEngine.Pipeline;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace CbmEngine.Tests.Unit.CbmFr;

// FR-CBM-ENCODE-001 (CBMFR-001 follow-up): raw-span multicolor encode overload.
[Trait("Speed", "Fast")]
public class EncoderSpanTests
{
    private static Rgba32[] Gradient(int seed = 0)
    {
        var px = new Rgba32[320 * 200];
        for (int y = 0; y < 200; y++)
            for (int x = 0; x < 320; x++)
                px[y * 320 + x] = new Rgba32(
                    (byte)((x + seed) & 0xFF),
                    (byte)((y * 2 + seed) & 0xFF),
                    (byte)((x + y + seed) & 0xFF),
                    255);
        return px;
    }

    private static Image<Rgba32> ToImage(Rgba32[] px)
    {
        var img = new Image<Rgba32>(320, 200);
        img.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < 200; y++)
                px.AsSpan(y * 320, 320).CopyTo(acc.GetRowSpan(y));
        });
        return img;
    }

    [Fact]
    public void TEST_CBM_001_SpanEncode_ReturnsSizedBitmap()
    {
        var enc = C64MulticolorBitmapEncoder.Encode((ReadOnlySpan<Rgba32>)Gradient(), 320, 200);
        Assert.Equal(EncodedSplashBitmap.BitmapByteSize, enc.Bitmap.Length);
        Assert.Equal(EncodedSplashBitmap.ScreenRamSize, enc.ScreenRam.Length);
        Assert.Equal(EncodedSplashBitmap.ColorRamSize, enc.ColorRam.Length);
        Assert.Equal(SplashBitmapMode.Multicolor, enc.Mode);
    }

    [Fact]
    public void TEST_CBM_002_SpanEncode_ByteIdenticalToImageOverload()
    {
        var px = Gradient(7);
        var spanEnc = C64MulticolorBitmapEncoder.Encode((ReadOnlySpan<Rgba32>)px, 320, 200);
        using var img = ToImage(px);
        var imgEnc = C64MulticolorBitmapEncoder.Encode(img);
        Assert.Equal(imgEnc.Bitmap, spanEnc.Bitmap);
        Assert.Equal(imgEnc.ScreenRam, spanEnc.ScreenRam);
        Assert.Equal(imgEnc.ColorRam, spanEnc.ColorRam);
        Assert.Equal(imgEnc.BackgroundColorIndex, spanEnc.BackgroundColorIndex);
    }

    [Theory]
    [InlineData(160, 200, "width")]
    [InlineData(320, 100, "height")]
    public void TEST_CBM_003_SpanEncode_BadDims_Throws(int w, int h, string param)
    {
        var px = new Rgba32[w * h];
        var ex = Assert.Throws<ArgumentException>(() => C64MulticolorBitmapEncoder.Encode((ReadOnlySpan<Rgba32>)px, w, h));
        Assert.Equal(param, ex.ParamName);
    }

    [Fact]
    public void TEST_CBM_004_SpanEncode_ShortSpan_Throws()
    {
        var px = new Rgba32[100];
        var ex = Assert.Throws<ArgumentException>(() => C64MulticolorBitmapEncoder.Encode((ReadOnlySpan<Rgba32>)px, 320, 200));
        Assert.Equal("pixels", ex.ParamName);
    }

    [Fact]
    public void TEST_CBM_005_SpanEncode_ForceBackground_Honored()
    {
        var px = Gradient(3);
        var enc = C64MulticolorBitmapEncoder.Encode((ReadOnlySpan<Rgba32>)px, 320, 200, forceBackgroundColor: 6);
        Assert.Equal(6, enc.BackgroundColorIndex);
        using var img = ToImage(px);
        var imgEnc = C64MulticolorBitmapEncoder.Encode(img, forceBackgroundColor: 6);
        Assert.Equal(imgEnc.Bitmap, enc.Bitmap);
    }

    [Fact]
    public void TEST_CBM_006_ImageOverload_DelegatesToSpan()
    {
        var px = Gradient(11);
        using var img = ToImage(px);
        var imgEnc = C64MulticolorBitmapEncoder.Encode(img);
        var spanEnc = C64MulticolorBitmapEncoder.Encode((ReadOnlySpan<Rgba32>)px, 320, 200);
        Assert.Equal(spanEnc.Bitmap, imgEnc.Bitmap);
        Assert.Equal(spanEnc.ScreenRam, imgEnc.ScreenRam);
        Assert.Equal(spanEnc.ColorRam, imgEnc.ColorRam);
    }
}
