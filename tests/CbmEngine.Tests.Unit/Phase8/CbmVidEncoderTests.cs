using CbmEngine.Pipeline;
using CbmEngine.Pipeline.CbmVid;
using CbmEngine.Systems.Video;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace CbmEngine.Tests.Unit.Phase8;

[Trait("Speed", "Fast")]
public class CbmVidEncoderTests
{
    private static string WritePalettePng(string dir, string name, byte borderColorIdx, byte interiorColorIdx)
    {
        var border = VicPalette.Colors[borderColorIdx];
        var interior = VicPalette.Colors[interiorColorIdx];
        using var img = new Image<Rgba32>(320, 200);
        img.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < 200; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < 320; x++)
                {
                    bool isBorder = x < 8 || x >= 312 || y < 8 || y >= 192;
                    var c = isBorder ? border : interior;
                    row[x] = new Rgba32(c.R, c.G, c.B, 255);
                }
            }
        });
        var path = Path.Combine(dir, name);
        img.SaveAsPng(path);
        return path;
    }

    [Fact]
    public void TEST_CBM_VID_002_EncodeDirectory_FourPngFrames_MatchesPerFrameEncoderOutput()
    {
        var dir = Directory.CreateTempSubdirectory("cbmvid-enc-").FullName;
        try
        {
            var p0 = WritePalettePng(dir, "frame_0000.png", 0, 6);
            var p1 = WritePalettePng(dir, "frame_0001.png", 0, 14);
            var p2 = WritePalettePng(dir, "frame_0002.png", 0, 5);
            var p3 = WritePalettePng(dir, "frame_0003.png", 0, 10);
            var outPath = Path.Combine(dir, "out.cbmvid");

            CbmVidEncoder.EncodeDirectory(dir, outPath);

            using var fs = File.OpenRead(outPath);
            using var player = new VideoPlayer(fs);
            Assert.Equal(4u, player.Header.FrameCount);

            string[] inputs = [p0, p1, p2, p3];
            for (int i = 0; i < 4; i++)
            {
                var expected = C64MulticolorBitmapEncoder.Encode(inputs[i], forceBackgroundColor: null);
                var actual = player.PeekFrame(i);
                Assert.Equal(expected.Mode, actual.Mode);
                Assert.True(expected.Bitmap.AsSpan().SequenceEqual(actual.Bitmap), $"frame {i} bitmap mismatch");
                Assert.True(expected.ScreenRam.AsSpan().SequenceEqual(actual.ScreenRam), $"frame {i} screen mismatch");
                Assert.True(expected.ColorRam.AsSpan().SequenceEqual(actual.ColorRam), $"frame {i} color mismatch");
            }
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void TEST_CBM_VID_003_OutOfPaletteSource_FailsWithFrameAndCoord()
    {
        var dir = Directory.CreateTempSubdirectory("cbmvid-oop-").FullName;
        try
        {
            WritePalettePng(dir, "frame_0000.png", 0, 6);
            WritePalettePng(dir, "frame_0001.png", 0, 6);
            // Frame 2: insert an out-of-palette pixel at (12, 4)
            using (var img = new Image<Rgba32>(320, 200))
            {
                var bg = VicPalette.Colors[0];
                img.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < 200; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (int x = 0; x < 320; x++) row[x] = new Rgba32(bg.R, bg.G, bg.B, 255);
                    }
                });
                img[12, 4] = new Rgba32(0x12, 0x34, 0x56, 255);
                img[13, 4] = new Rgba32(0x12, 0x34, 0x56, 255);
                img.SaveAsPng(Path.Combine(dir, "frame_0002.png"));
            }
            var outPath = Path.Combine(dir, "out.cbmvid");

            var ex = Assert.Throws<CbmVidEncodeException>(() => CbmVidEncoder.EncodeDirectory(dir, outPath));
            Assert.Contains("frame=2", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("x=12", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("y=4", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("#123456", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
