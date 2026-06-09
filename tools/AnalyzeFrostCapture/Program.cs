using CbmEngine.Pipeline;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

string repo = FindRepoRoot();
string capDir = Path.Combine(repo, "artifacts", "captures");
string outDir = Path.Combine(repo, "artifacts", "captures");
byte[] ram = File.ReadAllBytes(Path.Combine(capDir, "all-ram.bin"));
byte[] colorVic = File.ReadAllBytes(Path.Combine(capDir, "color-vic.bin"));

if (ram.Length is 65538) ram = ram.AsSpan(2).ToArray();
if (colorVic.Length is 3074) colorVic = colorVic.AsSpan(2).ToArray();

byte d018Reg = colorVic[0x018];
ushort screenBase = (ushort)(((d018Reg >> 4) & 0x0F) * 0x0400);
ushort charsetBase = (ushort)(((d018Reg >> 1) & 0x07) * 0x0800);
Console.WriteLine($"$D018 = ${d018Reg:X2}  -> screen base ${screenBase:X4}, charset base ${charsetBase:X4}");

byte[] screen = ram.AsSpan(screenBase, 1000).ToArray();
byte[] charset = charsetBase is 0x1000 or 0x9000 ? new byte[2048] : ram.AsSpan(charsetBase, 2048).ToArray();
byte[] color = colorVic.AsSpan(0x0800, 1000).ToArray();

File.WriteAllBytes(Path.Combine(outDir, "screen.bin"), screen);
File.WriteAllBytes(Path.Combine(outDir, "charset.bin"), charset);
File.WriteAllBytes(Path.Combine(outDir, "color.bin"), color);

byte[] sprites = ram.AsSpan(0x0B00, 0x0E40 - 0x0B00).ToArray();
File.WriteAllBytes(Path.Combine(outDir, "sprites.bin"), sprites);
Console.WriteLine($"sprites.bin ({sprites.Length} bytes) at $0B00-$0E3F");

byte[] vicState = new byte[]
{
    colorVic[0x000], colorVic[0x001], colorVic[0x002], colorVic[0x003],
    colorVic[0x004], colorVic[0x005], colorVic[0x006], colorVic[0x007],
    colorVic[0x008], colorVic[0x009], colorVic[0x00A], colorVic[0x00B],
    colorVic[0x00C], colorVic[0x00D], colorVic[0x00E], colorVic[0x00F],
    colorVic[0x010], colorVic[0x015], colorVic[0x01C],
    colorVic[0x025], colorVic[0x026],
    colorVic[0x027], colorVic[0x028], colorVic[0x029], colorVic[0x02A],
    colorVic[0x02B], colorVic[0x02C], colorVic[0x02D], colorVic[0x02E],
};
File.WriteAllBytes(Path.Combine(outDir, "vic-sprite-state.bin"), vicState);
Console.WriteLine($"vic-sprite-state.bin ({vicState.Length} bytes): sprite X/Y[0-15]+MSB+enable+MC+MC1+MC2+colors[8]");

byte[] sprPointersBytes = ram.AsSpan(0x07F8, 8).ToArray();
File.WriteAllBytes(Path.Combine(outDir, "sprite-pointers.bin"), sprPointersBytes);

byte d021 = (byte)(colorVic[0x021] & 0x0F);
byte d020 = (byte)(colorVic[0x020] & 0x0F);
byte d016 = colorVic[0x016];
byte d018 = colorVic[0x018];
byte d011 = colorVic[0x011];
Console.WriteLine($"screen.bin  ({screen.Length} bytes)  at $0400");
Console.WriteLine($"charset.bin ({charset.Length} bytes) at $1000");
Console.WriteLine($"color.bin   ({color.Length} bytes)  at $D800");
Console.WriteLine($"$D011 = ${d011:X2}  $D016 = ${d016:X2}  $D018 = ${d018:X2}");
Console.WriteLine($"  text mode = {((d011 & 0x20) == 0 ? "yes" : "no, bitmap")},  multicolor = {((d016 & 0x10) != 0 ? "yes" : "no")}");
Console.WriteLine($"$D020 (border) = ${d020:X1}    $D021 (bg) = ${d021:X1}");

byte[] sprPointers = ram.AsSpan(0x07F8, 8).ToArray();
for (int s = 0; s < 8; s++)
{
    int blk = sprPointers[s];
    int addr = blk * 64;
    Console.WriteLine($"  Sprite {s}: pointer=${blk:X2} -> data ${addr:X4}  color=${colorVic[0x027 + s] & 0xF:X1}  enabled={(((colorVic[0x015] >> s) & 1) == 1 ? "yes" : "no")}  mc={(((colorVic[0x01C] >> s) & 1) == 1 ? "yes" : "no")}");
}

using var img = new Image<Rgba32>(320, 200);
var pal = new Rgba32[16];
for (int i = 0; i < 16; i++) pal[i] = new Rgba32(VicPalette.Colors[i].R, VicPalette.Colors[i].G, VicPalette.Colors[i].B);

for (int row = 0; row < 25; row++)
{
    for (int colx = 0; colx < 40; colx++)
    {
        int cellIdx = row * 40 + colx;
        byte ch = screen[cellIdx];
        byte fgIdx = (byte)(color[cellIdx] & 0x0F);
        byte bgIdx = d021;
        for (int py = 0; py < 8; py++)
        {
            byte bits = charset[ch * 8 + py];
            for (int px = 0; px < 8; px++)
            {
                bool fg = (bits & (0x80 >> px)) != 0;
                img[colx * 8 + px, row * 8 + py] = pal[fg ? fgIdx : bgIdx];
            }
        }
    }
}
string previewPath = Path.Combine(outDir, "splash-textmode-preview.png");
img.SaveAsPng(previewPath);
Console.WriteLine($"Text-mode preview written: {previewPath}");

static string FindRepoRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d is not null && !File.Exists(Path.Combine(d.FullName, "CbmEngine.slnx"))) d = d.Parent;
    return d?.FullName ?? throw new InvalidOperationException("repo root not found");
}
