using CbmEngine.Pipeline;
using CbmEngine.Systems.Audio;
using CbmEngine.Systems.Cartridge;

string repo = FindRepoRoot();
string sidPath = Path.Combine(repo, "assets", "sid", "Frost_Point.sid");
string capDir = Path.Combine(repo, "artifacts", "captures");
string outDir = Path.Combine(repo, "artifacts", "carts");
Directory.CreateDirectory(outDir);

using var fs = File.OpenRead(sidPath);
var psid = PsidLoader.Load(fs);

string pngPath = Path.Combine(capDir, "frost-point-title.png");
if (!File.Exists(pngPath)) throw new FileNotFoundException("Missing screenshot - run CaptureFrostPoint.ps1 + convert-bmp.ps1 first.", pngPath);

string previewPath = Path.Combine(capDir, "frost-point-title.encoded.png");
var splash = C64MulticolorBitmapEncoder.Encode(pngPath, forceBackgroundColor: 0x00, debugDecodedPngPath: previewPath);
Console.WriteLine($"Encoded multicolor bitmap: bg=${splash.BackgroundColorIndex:X1}, bitmap={splash.Bitmap.Length}B screen={splash.ScreenRam.Length}B color={splash.ColorRam.Length}B");
Console.WriteLine($"Decoded preview: {previewPath}");

Console.WriteLine($"PSID: '{psid.Header.Name}' by {psid.Header.Author}  init=${psid.Header.InitAddress:X4} play=${psid.Header.PlayAddress:X4} len={psid.Payload.Length}B");

var ca65 = new Ca65Assembler();
var cart = PsidPlayerCart.Build(
    psid,
    backgroundColor: 0x00,
    initialBorderColor: 0x00,
    borderCyclePeriodFrames: 50,
    splash: splash,
    assembler: ca65);

string binPath = Path.Combine(outDir, "Frost_Point.bin");
string crtPath = Path.Combine(outDir, "Frost_Point.crt");
File.WriteAllBytes(binPath, cart);
File.WriteAllBytes(crtPath, CrtFile.WrapStandard16K(cart, "FROSTPOINT"));
Console.WriteLine($"Cart written: {crtPath}  ({cart.Length} bytes raw / {new FileInfo(crtPath).Length} CRT)");

static string FindRepoRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d is not null && !File.Exists(Path.Combine(d.FullName, "CbmEngine.slnx"))) d = d.Parent;
    return d?.FullName ?? throw new InvalidOperationException("repo root not found");
}
