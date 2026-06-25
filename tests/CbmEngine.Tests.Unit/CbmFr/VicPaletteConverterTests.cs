using CbmEngine.Pipeline;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace CbmEngine.Tests.Unit.CbmFr;

// FR-CBM-PALETTE-001 (CBMFR-011): VicPaletteConverter selects a VIC palette index for any RGB or
// Rgba32 color, with optional curated subset to bias the choice toward saturated hues.
[Trait("Speed", "Fast")]
public class VicPaletteConverterTests
{
    [Fact]
    public void TEST_CBM_056_NearestIndex_ExactPaletteColor_ReturnsItsIndex()
    {
        for (byte i = 0; i < 16; i++)
        {
            var c = VicPalette.Colors[i];
            Assert.Equal(i, VicPaletteConverter.NearestIndex(c.R, c.G, c.B));
        }
    }

    [Fact]
    public void TEST_CBM_057_NearestIndex_OffPaletteColor_SnapsToClosest()
    {
        // (250, 250, 250) is very close to white (VIC 1 = 0xFF,0xFF,0xFF) and far from all other
        // VIC colors; the nearest selection must be 1.
        Assert.Equal(1, VicPaletteConverter.NearestIndex(250, 250, 250));

        // (10, 10, 10) is very close to black (VIC 0); selection must be 0.
        Assert.Equal(0, VicPaletteConverter.NearestIndex(10, 10, 10));
    }

    [Fact]
    public void TEST_CBM_058_NearestIndex_Rgba32Overload_IgnoresAlpha()
    {
        var c = VicPalette.Colors[7]; // yellow
        Assert.Equal(7, VicPaletteConverter.NearestIndex(new Rgba32(c.R, c.G, c.B, 255)));
        Assert.Equal(7, VicPaletteConverter.NearestIndex(new Rgba32(c.R, c.G, c.B, 0)));
        Assert.Equal(7, VicPaletteConverter.NearestIndex(new Rgba32(c.R, c.G, c.B, 128)));
    }

    [Fact]
    public void TEST_CBM_059_NearestIndex_RestrictedToSubset_PicksWithinSubset()
    {
        // Brown (VIC 9 = 0x5A,0x33,0x00) is the exact-match for itself; restricting the subset to
        // {0=black, 7=yellow, 8=orange} forces the algorithm to pick something else. Orange
        // (0x9B,0x52,0x1C) is by far the closest among the three.
        ReadOnlySpan<byte> allowed = stackalloc byte[] { 0, 7, 8 };
        var brown = VicPalette.Colors[9];
        Assert.Equal(8, VicPaletteConverter.NearestIndex(brown.R, brown.G, brown.B, allowed));
    }

    [Fact]
    public void TEST_CBM_060_NearestIndex_RestrictedToSubset_BlueGrayPicksBlueNotGray()
    {
        // A blue-tinged gray (110, 120, 160) without a subset usually maps to medium gray (VIC 12).
        // Restricting the subset to {0=black, 6=blue, 14=light blue, 15=light gray} excludes gray
        // and forces a saturated-hue choice. The expected pick is one of the blues.
        ReadOnlySpan<byte> allowed = stackalloc byte[] { 0, 6, 14, 15 };
        var picked = VicPaletteConverter.NearestIndex(110, 120, 160, allowed);
        Assert.True(picked == 6 || picked == 14, $"expected blue (6) or light blue (14), got {picked}");
    }

    [Fact]
    public void TEST_CBM_061_NearestIndex_EmptySubset_FallsBackToFullPalette()
    {
        // An empty allowedIndices span means "no restriction"; behavior must match the unrestricted
        // overload.
        var c = VicPalette.Colors[5]; // green
        var unrestricted = VicPaletteConverter.NearestIndex(c.R, c.G, c.B);
        var withEmptySubset = VicPaletteConverter.NearestIndex(c.R, c.G, c.B, ReadOnlySpan<byte>.Empty);
        Assert.Equal(unrestricted, withEmptySubset);
    }

    [Fact]
    public void TEST_CBM_062_NearestIndex_InvalidIndexInSubset_Throws()
    {
        // Indices above 15 are not valid VIC palette entries; the converter must refuse to use them
        // rather than silently misbehave.
        var span = new byte[] { 0, 16 };
        Assert.Throws<ArgumentOutOfRangeException>(() => VicPaletteConverter.NearestIndex(0, 0, 0, span));
    }

    // ============================================================
    // CBMFR-0xx / VicPaletteConverter image framing (ToC64Frame)
    // Acceptance: arbitrary source -> exactly 320x200, VIC palette only,
    // AR-preserving fit + center, padding (sides or top/bottom) with bg color,
    // reuses NearestIndex for flattening, default bg=0, path overload, no mutation of source.
    // ============================================================

    private static Image<Rgba32> MakeSolid(int w, int h, Rgba32 c)
    {
        var img = new Image<Rgba32>(w, h);
        img.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < h; y++)
                acc.GetRowSpan(y).Fill(c);
        });
        return img;
    }

    private static Rgba32 ToRgba(VicPalette.Rgb c) => new Rgba32(c.R, c.G, c.B, 255);

    private static Image<Rgba32> MakeChecker(int w, int h, Rgba32 a, Rgba32 b, int cell = 4)
    {
        var img = new Image<Rgba32>(w, h);
        img.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < h; y++)
            {
                var row = acc.GetRowSpan(y);
                for (int x = 0; x < w; x++)
                {
                    bool useA = ((x / cell) + (y / cell)) % 2 == 0;
                    row[x] = useA ? a : b;
                }
            }
        });
        return img;
    }

    private static bool IsExactVicColor(Rgba32 p)
    {
        for (int i = 0; i < 16; i++)
        {
            var c = VicPalette.Colors[i];
            if (p.R == c.R && p.G == c.G && p.B == c.B) return true;
        }
        return false;
    }

    private static byte GetVicIndex(Rgba32 p)
    {
        for (byte i = 0; i < 16; i++)
        {
            var c = VicPalette.Colors[i];
            if (p.R == c.R && p.G == c.G && p.B == c.B) return i;
        }
        return 255;
    }

    [Fact]
    public void TEST_CBM_063_ToC64Frame_AlwaysReturnsExactly320x200()
    {
        using var src = MakeSolid(100, 50, ToRgba(VicPalette.Colors[1])); // white
        using var frame = VicPaletteConverter.ToC64Frame(src);
        Assert.Equal(320, frame.Width);
        Assert.Equal(200, frame.Height);
    }

    [Fact]
    public void TEST_CBM_064_ToC64Frame_AllPixelsAreExactVicPaletteColors()
    {
        using var src = MakeChecker(80, 60, ToRgba(VicPalette.Colors[2]), ToRgba(VicPalette.Colors[5])); // red/green mix
        using var frame = VicPaletteConverter.ToC64Frame(src, backgroundIndex: 0);
        frame.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < 200; y++)
            {
                var row = acc.GetRowSpan(y);
                for (int x = 0; x < 320; x++)
                    Assert.True(IsExactVicColor(row[x]), $"non-VIC color at ({x},{y})");
            }
        });
    }

    [Fact]
    public void TEST_CBM_065_ToC64Frame_DefaultBackgroundIsIndex0_BlackPads()
    {
        using var src = MakeSolid(10, 10, ToRgba(VicPalette.Colors[7])); // small yellow square
        using var frame = VicPaletteConverter.ToC64Frame(src); // default bg 0
        var black = new Rgba32(VicPalette.Colors[0].R, VicPalette.Colors[0].G, VicPalette.Colors[0].B, 255);
        // corners must be pad (bg)
        Assert.Equal(black, frame[0, 0]);
        Assert.Equal(black, frame[319, 0]);
        Assert.Equal(black, frame[0, 199]);
        Assert.Equal(black, frame[319, 199]);
    }

    [Fact]
    public void TEST_CBM_066_ToC64Frame_ExplicitBackgroundIndex_UsedForPads()
    {
        using var src = MakeSolid(32, 16, ToRgba(VicPalette.Colors[1]));
        using var frame = VicPaletteConverter.ToC64Frame(src, backgroundIndex: 6); // blue
        var blue = new Rgba32(VicPalette.Colors[6].R, VicPalette.Colors[6].G, VicPalette.Colors[6].B, 255);
        Assert.Equal(blue, frame[5, 5]);   // expect pad here for tiny src
        Assert.Equal(blue, frame[300, 180]);
        // and all pads are this color (sample a few)
        Assert.Equal(6, GetVicIndex(frame[0, 0]));
        Assert.Equal(6, GetVicIndex(frame[319, 199]));
    }

    [Fact]
    public void TEST_CBM_067_ToC64Frame_InvalidBackgroundIndex_Throws()
    {
        using var src = MakeSolid(16, 16, ToRgba(VicPalette.Colors[0]));
        Assert.Throws<ArgumentOutOfRangeException>(() => VicPaletteConverter.ToC64Frame(src, 16));
    }

    [Fact]
    public void TEST_CBM_068_ToC64Frame_SameSizeInput_FlattensButPreservesContentAfterSnap()
    {
        // Build a 320x200 that is already some VIC colors + one off-palette that must snap
        using var src = new Image<Rgba32>(320, 200);
        var yellow = ToRgba(VicPalette.Colors[7]);
        var offWhite = new Rgba32(250, 250, 250, 255); // must snap to 1 (white)
        src.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < 200; y++)
            {
                var row = acc.GetRowSpan(y);
                for (int x = 0; x < 320; x++)
                    row[x] = (x < 160) ? yellow : offWhite;
            }
        });

        using var frame = VicPaletteConverter.ToC64Frame(src);
        // left half should be yellow (7), right half white (1) after flatten
        Assert.Equal(7, GetVicIndex(frame[10, 10]));
        Assert.Equal(1, GetVicIndex(frame[200, 10]));
        Assert.Equal(1, GetVicIndex(frame[300, 100]));
    }

    [Fact]
    public void TEST_CBM_069_ToC64Frame_TallSource_PadsOnSides_PreservesAR_Centers()
    {
        // 100x200 tall source (AR=0.5). Scale to fit height -> contentW ~ 160, pad left/right ~80px each side.
        var red = ToRgba(VicPalette.Colors[2]);
        using var src = MakeSolid(100, 200, red);
        using var frame = VicPaletteConverter.ToC64Frame(src, 0);

        // Content should be full height, width = round(100 * (200/200)) = 100? Wait, scale=min(320/100,200/200)=min(3.2,1)=1 => 100w x200h
        // So pads (320-100)/2 = 110 left and right.
        int expectedPad = (320 - 100) / 2; // 110
        Assert.Equal(0, GetVicIndex(frame[expectedPad - 1, 50])); // left pad
        Assert.Equal(0, GetVicIndex(frame[320 - expectedPad, 150])); // right pad
        Assert.Equal(2, GetVicIndex(frame[expectedPad + 10, 10])); // content red
        Assert.Equal(2, GetVicIndex(frame[expectedPad + 89, 199])); // right edge of content

        // Verify centering: left pad width == right pad width (or +-1)
        int leftPad = 0;
        while (leftPad < 160 && GetVicIndex(frame[leftPad, 100]) == 0) leftPad++;
        int rightPad = 0;
        while (rightPad < 160 && GetVicIndex(frame[319 - rightPad, 100]) == 0) rightPad++;
        Assert.InRange(Math.Abs(leftPad - rightPad), 0, 1);
    }

    [Fact]
    public void TEST_CBM_070_ToC64Frame_WideSource_PadsTopBottom_PreservesAR_Centers()
    {
        // 320x100 wide. scale = min(1, 2) =1 => 320x100, pad 50 top + 50 bottom.
        var cyan = ToRgba(VicPalette.Colors[3]);
        using var src = MakeSolid(320, 100, cyan);
        using var frame = VicPaletteConverter.ToC64Frame(src, 11); // use gray as bg for distinction

        int expectedPad = (200 - 100) / 2; // 50
        Assert.Equal(11, GetVicIndex(frame[10, expectedPad - 1])); // top pad
        Assert.Equal(11, GetVicIndex(frame[300, 200 - expectedPad])); // bottom pad
        Assert.Equal(3, GetVicIndex(frame[50, expectedPad + 5])); // content
        Assert.Equal(3, GetVicIndex(frame[200, expectedPad + 94]));

        // center check
        int topPad = 0;
        while (topPad < 100 && GetVicIndex(frame[160, topPad]) == 11) topPad++;
        int botPad = 0;
        while (botPad < 100 && GetVicIndex(frame[160, 199 - botPad]) == 11) botPad++;
        Assert.InRange(Math.Abs(topPad - botPad), 0, 1);
    }

    [Fact]
    public void TEST_CBM_071_ToC64Frame_SmallSquare_CentersWithPillarboxOrLetterbox()
    {
        // 64x64 -> scale = min(5, 3.125) ~ 3.125 -> ~200x200 , pad sides
        var green = ToRgba(VicPalette.Colors[5]);
        using var src = MakeSolid(64, 64, green);
        using var frame = VicPaletteConverter.ToC64Frame(src, 0);

        // content ~200x200, pad ~60 each side
        Assert.Equal(0, GetVicIndex(frame[50, 100]));
        Assert.Equal(5, GetVicIndex(frame[110, 50])); // inside content area
        Assert.Equal(5, GetVicIndex(frame[210, 150]));
        Assert.Equal(0, GetVicIndex(frame[270, 100]));
    }

    [Fact]
    public void TEST_CBM_072_ToC64Frame_AlphaIsIgnored_DuringFlatten()
    {
        var c = VicPalette.Colors[13]; // light green
        using var src = new Image<Rgba32>(16, 16);
        src.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < 16; y++)
            {
                var row = acc.GetRowSpan(y);
                for (int x = 0; x < 16; x++)
                    row[x] = new Rgba32(c.R, c.G, c.B, (byte)(x * 16)); // varying alpha
            }
        });
        using var frame = VicPaletteConverter.ToC64Frame(src);
        // wherever the small content lands, it must be index 13, not affected by alpha
        // find a content pixel
        bool foundContent = false;
        frame.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < 200 && !foundContent; y++)
            {
                var row = acc.GetRowSpan(y);
                for (int x = 0; x < 320; x++)
                {
                    if (GetVicIndex(row[x]) == 13) { foundContent = true; break; }
                }
            }
        });
        Assert.True(foundContent);
    }

    [Fact]
    public void TEST_CBM_073_ToC64Frame_DoesNotMutateSource()
    {
        var orig = VicPalette.Colors[4];
        using var src = MakeSolid(40, 30, new Rgba32(123, 45, 67, 255)); // off palette
        var before = src[5, 5];
        using var _ = VicPaletteConverter.ToC64Frame(src, 1);
        Assert.Equal(before, src[5, 5]); // unchanged
        // also confirm it would have flattened that color if it had been output
    }

    [Fact]
    public void TEST_CBM_074_ToC64Frame_PathOverload_ProducesSameResultAsImageOverload()
    {
        // Use an in-memory temp png
        var tmp = Path.Combine(Path.GetTempPath(), $"vicframe-test-{Guid.NewGuid():N}.png");
        try
        {
            var pink = ToRgba(VicPalette.Colors[10]);
            using (var src = MakeSolid(50, 25, pink))
                src.SaveAsPng(tmp);

            using var fromPath = VicPaletteConverter.ToC64Frame(tmp, backgroundIndex: 15);
            using var fromImg = VicPaletteConverter.ToC64Frame(MakeSolid(50, 25, pink), backgroundIndex: 15);

            Assert.Equal(320, fromPath.Width);
            Assert.Equal(200, fromPath.Height);

            // Sample a few pixels; both must be identical (either pad 15 or content 10)
            Assert.Equal(GetVicIndex(fromImg[10, 10]), GetVicIndex(fromPath[10, 10]));
            Assert.Equal(GetVicIndex(fromImg[200, 90]), GetVicIndex(fromPath[200, 90]));
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    [Fact]
    public void TEST_CBM_075_ToC64Frame_UsesNearestIndexLogic_ForOffPaletteInput()
    {
        // A color very close to black but not exact must become 0 in pads? No, in content.
        // Create 320x200 source of a dark gray that Nearest says is 0 or 11/12? From existing test, (10,10,10)->0
        using var src = MakeSolid(320, 200, new Rgba32(10, 10, 10, 255));
        using var frame = VicPaletteConverter.ToC64Frame(src);
        // whole thing is "content", flattened to 0
        Assert.Equal(0, GetVicIndex(frame[0, 0]));
        Assert.Equal(0, GetVicIndex(frame[159, 99]));
    }

    // --- Bitmap / pixels return path (ToC64FramePixels) ---

    private static Rgba32[] ImageToPixels(Image<Rgba32> img)
    {
        var px = new Rgba32[320 * 200];
        img.ProcessPixelRows(acc =>
        {
            for (int y = 0; y < 200; y++)
                acc.GetRowSpan(y).CopyTo(px.AsSpan(y * 320, 320));
        });
        return px;
    }

    [Fact]
    public void TEST_CBM_076_ToC64FramePixels_ReturnsExactly320x200_FlatArray()
    {
        using var src = MakeSolid(123, 77, ToRgba(VicPalette.Colors[4]));
        var pixels = VicPaletteConverter.ToC64FramePixels(src);
        Assert.Equal(320 * 200, pixels.Length);
    }

    [Fact]
    public void TEST_CBM_077_ToC64FramePixels_AllEntriesExactVicColors()
    {
        using var src = MakeChecker(90, 45, ToRgba(VicPalette.Colors[8]), ToRgba(VicPalette.Colors[14]));
        var pixels = VicPaletteConverter.ToC64FramePixels(src, backgroundIndex: 0);
        foreach (var p in pixels)
            Assert.True(IsExactVicColor(p));
    }

    [Fact]
    public void TEST_CBM_078_ToC64FramePixels_DefaultAndExplicitBg_PadsCorrectly()
    {
        using var src = MakeSolid(20, 20, ToRgba(VicPalette.Colors[7]));
        var pixels = VicPaletteConverter.ToC64FramePixels(src); // default 0
        Assert.Equal(0, GetVicIndex(pixels[0]));
        Assert.Equal(0, GetVicIndex(pixels[320 * 199 + 319]));

        var pixelsBlue = VicPaletteConverter.ToC64FramePixels(src, backgroundIndex: 6);
        Assert.Equal(6, GetVicIndex(pixelsBlue[50]));
        Assert.Equal(6, GetVicIndex(pixelsBlue[320 * 100 + 10]));
    }

    [Fact]
    public void TEST_CBM_079_ToC64FramePixels_AR_Centering_AndContentPlacement_MatchImageVersion()
    {
        // Tall source -> sides padded
        var red = ToRgba(VicPalette.Colors[2]);
        using var tallSrc = MakeSolid(80, 200, red);
        var imgFrame = VicPaletteConverter.ToC64Frame(tallSrc, 11);
        var direct = VicPaletteConverter.ToC64FramePixels(tallSrc, 11);
        var fromImg = ImageToPixels(imgFrame);

        Assert.Equal(direct, fromImg);
        Assert.Equal(11, GetVicIndex(direct[30])); // left pad
        Assert.Equal(2, GetVicIndex(direct[160 + 10])); // content area (after centering)

        // Wide source
        using var wideSrc = MakeSolid(400, 80, ToRgba(VicPalette.Colors[13]));
        var imgWide = VicPaletteConverter.ToC64Frame(wideSrc, 0);
        var pixWide = VicPaletteConverter.ToC64FramePixels(wideSrc, 0);
        Assert.Equal(ImageToPixels(imgWide), pixWide);
    }

    [Fact]
    public void TEST_CBM_080_ToC64FramePixels_FlattensUsingNearestIndex()
    {
        using var src = MakeSolid(320, 200, new Rgba32(12, 12, 12, 255)); // very dark -> 0
        var pixels = VicPaletteConverter.ToC64FramePixels(src);
        Assert.All(pixels, p => Assert.Equal(0, GetVicIndex(p)));
    }

    [Fact]
    public void TEST_CBM_081_ToC64FramePixels_PathOverload_Equivalence()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"vic-pixels-{Guid.NewGuid():N}.png");
        try
        {
            using (var s = MakeSolid(64, 48, ToRgba(VicPalette.Colors[5])))
                s.SaveAsPng(tmp);

            var fromPath = VicPaletteConverter.ToC64FramePixels(tmp, 9);
            using var loaded = Image.Load<Rgba32>(tmp);
            var fromImg = VicPaletteConverter.ToC64FramePixels(loaded, 9);

            Assert.Equal(fromImg, fromPath);
            Assert.Equal(320 * 200, fromPath.Length);
        }
        finally { try { File.Delete(tmp); } catch { } }
    }

    [Fact]
    public void TEST_CBM_082_ToC64FramePixels_AndImage_AreEquivalent_ForComplexCase()
    {
        // Checker with off-palette accent + specific bg
        var a = ToRgba(VicPalette.Colors[1]);
        var b = new Rgba32(240, 240, 10, 255); // will snap toward yellow(7) or orange
        using var src = MakeChecker(128, 96, a, b, cell: 8);

        var pixels = VicPaletteConverter.ToC64FramePixels(src, backgroundIndex: 12);
        using var img = VicPaletteConverter.ToC64Frame(src, backgroundIndex: 12);
        var viaImage = ImageToPixels(img);

        Assert.Equal(pixels, viaImage);

        // Spot check a pad and a content pixel are VIC
        Assert.True(IsExactVicColor(pixels[5]));
        Assert.True(IsExactVicColor(pixels[320 * 50 + 200]));
    }
}
