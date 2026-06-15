using CbmEngine.Pipeline.CbmVid;
using CbmEngine.Systems.Video;
using CbmEngine.Tools.CbmVidStudio;
using ViceSharp.RomFetch;

// Dispatch: no args (or "gui" / "--gui") -> launch Myra GUI. Otherwise: CLI.
bool guiRequested = args.Length == 0
    || (args.Length >= 1 && (args[0] is "gui" or "--gui"));
bool helpRequested = args.Contains("--help") || args.Contains("-h");

if (helpRequested) { PrintUsage(); return 0; }

if (guiRequested)
{
    // dotnet tool shims don't carry app.manifest, so the process starts DPI-unaware and every
    // coordinate Windows reports is virtualized. Opt in to PerMonitorV2 BEFORE any SDL/window
    // initialization so display modes, window rects, and DPI queries are real pixels.
    DpiNormalizer.TryEnablePerMonitorV2();

    var guiArgs = args.Length >= 1 && (args[0] is "gui" or "--gui") ? args[1..] : args;
    try
    {
        using var gameWindow = new StudioGame(guiArgs);
        gameWindow.Run();
        return 0;
    }
    catch (Exception ex)
    {
        // GUI exceptions otherwise vanish when launched without a console; persist to the diag
        // log and stderr so crashes are diagnosable from %TEMP%\cbmvid-studio.log.
        StudioDiag.Log($"FATAL: {ex}");
        Console.Error.WriteLine(ex);
        return 1;
    }
}

string? inDir = null;
string? inVideo = null;
string? inGif = null;
string? inCbmvid = null;
string? outPath = null;
string? gifOut = null;
string? ffmpegPath = null;
string? romBase = null;
ushort fps = 50;
CbmVidFrameMode mode = CbmVidFrameMode.Multicolor;
bool validateOnly = false;
bool keepFrames = false;

for (int i = 0; i < args.Length; i++)
{
    var a = args[i];
    if (a == "--validate") validateOnly = true;
    else if (a == "--in" && i + 1 < args.Length) inDir = args[++i];
    else if (a == "--in-video" && i + 1 < args.Length) inVideo = args[++i];
    else if (a == "--in-gif" && i + 1 < args.Length) inGif = args[++i];
    else if (a == "--in-cbmvid" && i + 1 < args.Length) inCbmvid = args[++i];
    else if (a == "--out" && i + 1 < args.Length) outPath = args[++i];
    else if (a == "--gif-out" && i + 1 < args.Length) gifOut = args[++i];
    else if (a == "--rom-base" && i + 1 < args.Length) romBase = args[++i];
    else if (a == "--ffmpeg" && i + 1 < args.Length) ffmpegPath = args[++i];
    else if (a == "--keep-frames") keepFrames = true;
    else if (a == "--fps" && i + 1 < args.Length) fps = ushort.Parse(args[++i]);
    else if (a == "--mode" && i + 1 < args.Length)
    {
        var m = args[++i].ToLowerInvariant();
        mode = m switch { "mc" or "multicolor" => CbmVidFrameMode.Multicolor, "hr" or "hires" => CbmVidFrameMode.HiRes, _ => throw new ArgumentException($"Unknown mode '{m}'.") };
    }
    else if (a.StartsWith("--")) throw new ArgumentException($"Unknown flag '{a}'.");
}

if (validateOnly)
{
    if (outPath is null) { Console.Error.WriteLine("--validate requires --out <path>"); return 2; }
    var hdr = VideoPlayer.ValidateFile(outPath);
    Console.WriteLine($"OK {outPath} version=1 frames={hdr.FrameCount} mode={hdr.DefaultMode} fps={hdr.FrameRate}");
    return 0;
}

if (inCbmvid is not null)
{
    if (gifOut is null) { Console.Error.WriteLine("--in-cbmvid requires --gif-out <path>"); return 2; }
    var roms = new RomProvider(romBase ?? FindRomBase());
    CbmVidGifExporter.Export(inCbmvid, gifOut, roms, log: Console.WriteLine);
    return 0;
}

if (outPath is null || (inDir is null && inVideo is null && inGif is null)) { PrintUsage(); return 2; }

if (inGif is not null)
{
    Console.WriteLine($"Encoding animated GIF {inGif} -> {outPath} (defaultMode={mode}, fpsOverride={(fps == 50 ? "no" : fps.ToString())})");
    ushort? overrideFps = fps == 50 ? null : fps;
    CbmVidEncoder.EncodeAnimatedGif(inGif, outPath, defaultMode: mode, overrideFrameRate: overrideFps, log: Console.WriteLine);
}
else if (inVideo is not null)
{
    Console.WriteLine($"Encoding video {inVideo} -> {outPath} (fps={fps}, defaultMode={mode})");
    CbmVidEncoder.EncodeVideo(inVideo, outPath, frameRate: fps, defaultMode: mode, ffmpegPath: ffmpegPath, keepIntermediateFrames: keepFrames, log: Console.WriteLine);
}
else
{
    Console.WriteLine($"Encoding {inDir} -> {outPath} (fps={fps}, defaultMode={mode})");
    CbmVidEncoder.EncodeDirectory(inDir!, outPath, frameRate: fps, defaultMode: mode);
}
var h = VideoPlayer.ValidateFile(outPath);
Console.WriteLine($"Wrote {outPath}: {h.FrameCount} frames, mode={h.DefaultMode}, fps={h.FrameRate}, file-size={new FileInfo(outPath).Length} B");

if (gifOut is not null)
{
    var roms = new RomProvider(romBase ?? FindRomBase());
    CbmVidGifExporter.Export(outPath, gifOut, roms, log: Console.WriteLine);
}

return 0;

static string FindRomBase()
{
    // Priority: bundled roms/ next to the tool -> CBMVID_ROM_BASE env -> repo walk-up (dev).
    return StudioRoms.Resolve()
        ?? throw new InvalidOperationException($"C64 ROMs not found; pass --rom-base <path> or set {StudioRoms.EnvVar}.");
}

static void PrintUsage()
{
    Console.WriteLine("cbmvid - encode video into a .cbmvid file (CLI + GUI).");
    Console.WriteLine();
    Console.WriteLine("GUI:");
    Console.WriteLine("  cbmvid                          launch the Myra-based encoder studio");
    Console.WriteLine("  cbmvid gui [folder-of-pngs]     same, optionally preloading a frames folder");
    Console.WriteLine();
    Console.WriteLine("CLI:");
    Console.WriteLine("  cbmvid --in-gif <file.gif> --out <file.cbmvid> [--mode mc|hr] [--fps <override>]");
    Console.WriteLine("  cbmvid --in-video <file> --out <file.cbmvid> [--fps 50] [--mode mc|hr] [--ffmpeg <path>] [--gif-out <preview.gif>] [--keep-frames]");
    Console.WriteLine("  cbmvid --in <png-dir> --out <file.cbmvid> [--fps 50] [--mode mc|hr] [--gif-out <preview.gif>]");
    Console.WriteLine("  cbmvid --in-cbmvid <file.cbmvid> --gif-out <preview.gif>");
    Console.WriteLine("  cbmvid --validate --out <file.cbmvid>");
    Console.WriteLine();
    Console.WriteLine("Video input: any format ffmpeg accepts (320x200 scale, VIC palette snap).");
    Console.WriteLine("PNG input: 320x200 frame_NNNN.png files, palette-clean (16 VIC colors).");
    Console.WriteLine("Animated GIF input: native ImageSharp decoder; preserves per-frame timing.");
}
