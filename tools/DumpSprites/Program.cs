using CbmEngine.Pipeline;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

string repo = FindRepoRoot();
string capDir = Path.Combine(repo, "artifacts", "captures");
string outDir = Path.Combine(capDir, "sprites-preview");
Directory.CreateDirectory(outDir);
byte[] ram = File.ReadAllBytes(Path.Combine(capDir, "all-ram.bin"));
if (ram.Length is 65538) ram = ram.AsSpan(2).ToArray();

var paletteRgb = new Rgba32[16];
for (int i = 0; i < 16; i++) paletteRgb[i] = new Rgba32(VicPalette.Colors[i].R, VicPalette.Colors[i].G, VicPalette.Colors[i].B);

int[] denseBlocks = new int[1024];
for (int blockIdx = 0; blockIdx < 1024; blockIdx++)
{
    int addr = blockIdx * 64;
    if (addr + 63 >= ram.Length) break;
    int setBits = 0;
    for (int i = 0; i < 63; i++) setBits += System.Numerics.BitOperations.PopCount((uint)ram[addr + i]);
    denseBlocks[blockIdx] = setBits;
}

Console.WriteLine("Top 30 in-bank-0 sprite candidates ($0000-$3FFF) by bit density:");
var sorted = Enumerable.Range(0, 256).Where(b => denseBlocks[b] > 30).OrderByDescending(b => denseBlocks[b]).Take(30).ToArray();
foreach (var b in sorted) Console.WriteLine($"  block ${b:X2} -> data ${b * 64:X4}  bits={denseBlocks[b]}");

Console.WriteLine();
Console.WriteLine("Dumping all captured sprite blocks as PNG (mc=hi-res-ish black/blue rendering):");
int[] interestingPointers = sorted.Take(30).Concat(new[] { 0x2C, 0x2D, 0x2E, 0x2F, 0x30, 0x31, 0x38 }).Distinct().ToArray();
foreach (int ptr in interestingPointers)
{
    int addr = ptr * 64;
    if (addr + 63 >= ram.Length) continue;
    using var img = new Image<Rgba32>(48, 42);
    for (int py = 0; py < 21; py++)
    {
        for (int byteCol = 0; byteCol < 3; byteCol++)
        {
            byte b = ram[addr + py * 3 + byteCol];
            for (int pair = 0; pair < 4; pair++)
            {
                int code = (b >> (6 - pair * 2)) & 0x03;
                int colorIdx = code switch { 0 => 0, 1 => 14, 2 => 1, _ => 6 };
                for (int dx = 0; dx < 2; dx++)
                    for (int dy = 0; dy < 2; dy++)
                    {
                        img[byteCol * 8 + pair * 2 + dx, py * 2 + dy] = paletteRgb[colorIdx];
                    }
            }
        }
    }
    string path = Path.Combine(outDir, $"sprite-{ptr:X2}-at-{addr:X4}.png");
    img.SaveAsPng(path);
}
Console.WriteLine($"  Wrote {interestingPointers.Length} previews to {outDir}");

static string FindRepoRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d is not null && !File.Exists(Path.Combine(d.FullName, "CbmEngine.slnx"))) d = d.Parent;
    return d?.FullName ?? throw new InvalidOperationException("repo root not found");
}
