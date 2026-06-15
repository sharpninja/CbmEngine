using CbmEngine.Pipeline;
using CbmEngine.Pipeline.CbmVid;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

string repo = FindRepoRoot();
string srcPng = Path.Combine(repo, "artifacts", "captures", "frost-point-title.png");
string outVid = Path.Combine(repo, "assets", "video", "intro.cbmvid");
Directory.CreateDirectory(Path.GetDirectoryName(outVid)!);

if (!File.Exists(srcPng)) throw new FileNotFoundException("Need artifacts/captures/frost-point-title.png; run tools/CaptureFrostPoint.ps1 + tools/convert-bmp.ps1.", srcPng);

var frame0 = C64MulticolorBitmapEncoder.Encode(srcPng, forceBackgroundColor: 0x00);
var frame1 = SolidFrame(6, 14);     // blue field, white border-color in cells
var frame2 = SolidFrame(2, 7);      // red field, yellow

using var fs = File.Create(outVid);
var header = new CbmVidHeader(320, 200, 50, FrameCount: 3, CbmVidFrameMode.Multicolor, Flags: 1);
using (var writer = new CbmVidWriter(fs, header, leaveOpen: false))
{
    writer.WriteFrame(frame0);
    writer.WriteFrame(frame1);
    writer.WriteFrame(frame2);
}
Console.WriteLine($"Wrote {outVid}  ({new FileInfo(outVid).Length} bytes)");

static EncodedSplashBitmap SolidFrame(byte bgIndex, byte fgIndex)
{
    var bitmap = new byte[EncodedSplashBitmap.BitmapByteSize];
    var screen = new byte[EncodedSplashBitmap.ScreenRamSize];
    var color = new byte[EncodedSplashBitmap.ColorRamSize];
    // bitmap all-ones bits -> every multicolor pixel is color "01" -> foreground from screen high nibble
    Array.Fill(bitmap, (byte)0x55);
    Array.Fill(screen, (byte)((fgIndex << 4) | (bgIndex & 0x0F)));
    Array.Fill(color, fgIndex);
    return new EncodedSplashBitmap(SplashBitmapMode.Multicolor, bgIndex, bitmap, screen, color);
}

static string FindRepoRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d is not null && !File.Exists(Path.Combine(d.FullName, "CbmEngine.slnx"))) d = d.Parent;
    return d?.FullName ?? throw new InvalidOperationException("repo root not found");
}
