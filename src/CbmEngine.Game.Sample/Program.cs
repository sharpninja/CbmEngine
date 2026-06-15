using CbmEngine.Host.MonoGame;
using CbmEngine.Pipeline;
using CbmEngine.Pipeline.Midi;
using CbmEngine.Systems;
using CbmEngine.Systems.Audio;
using CbmEngine.Systems.Boot;
using CbmEngine.Systems.Cartridge;
using CbmEngine.Systems.Midi;
using CbmEngine.Systems.Strategy;
using CbmEngine.Systems.Video;
using Microsoft.Xna.Framework;
using ViceSharp.RomFetch;

namespace CbmEngine.Game.Sample;

public static class Program
{
    public static int Main(string[] args)
    {
        bool headless = args.Contains("--headless");
        string? pngPath = args.FirstOrDefault(a => a.StartsWith("--dump-png="))?.Substring("--dump-png=".Length);
        int frames = ParseInt(args, "--frames=", defaultValue: 60);

        string? videoAsset = args.FirstOrDefault(a => a.StartsWith("--video="))?["--video=".Length..];
        if (videoAsset is not null) return RunVideo(videoAsset, headless, frames, pngPath);

        string? midiPath = args.FirstOrDefault(a => a.StartsWith("--midi="))?["--midi=".Length..];
        if (midiPath is not null) return RunMidi(midiPath, headless, frames);

        var romBase = FindRomBase();
        var roms = new RomProvider(romBase);

        var sys = CommodoreSystem.Build("c64", roms);

        Console.WriteLine("=== Build PsidPlayerCart with embedded Frost Point (assembled via CA65) ===");
        if (!Ca65Assembler.IsAvailable())
        {
            Console.WriteLine("ERROR  ca65/ld65 not found on PATH. Install CC65 (https://cc65.github.io) and ensure ca65.exe + ld65.exe are reachable.");
            return 2;
        }
        var ca65 = new Ca65Assembler();
        Console.WriteLine($"  Toolchain: {ca65.Ca65Path}");
        Console.WriteLine($"             {ca65.Ld65Path}");
        var sidPath = FindContentFile("assets/sid/Frost_Point.sid")
            ?? throw new InvalidOperationException("Frost_Point.sid not found.");
        using var fs = File.OpenRead(sidPath);
        var psid = PsidLoader.Load(fs);
        Console.WriteLine($"  SID: '{psid.Header.Name}' by {psid.Header.Author}  load=${psid.Header.LoadAddress:X4} init=${psid.Header.InitAddress:X4} play=${psid.Header.PlayAddress:X4}  payload={psid.Payload.Length}B");

        EncodedSplashBitmap? splash = null;
        var splashPng = FindContentFile("artifacts/captures/frost-point-title.png");
        if (splashPng is not null)
        {
            splash = C64MulticolorBitmapEncoder.Encode(splashPng, forceBackgroundColor: 0x00);
            Console.WriteLine($"  Splash: {splashPng}  ({splash.Bitmap.Length}B bitmap, bg=${splash.BackgroundColorIndex:X1}, mode={splash.Mode})");
        }
        else
        {
            Console.WriteLine("  Splash: none (artifacts/captures/frost-point-title.png missing -- run tools/CaptureFrostPoint.ps1 + convert-bmp.ps1)");
        }

        var cart = PsidPlayerCart.Build(psid, backgroundColor: 0x00, initialBorderColor: 0x00, borderCyclePeriodFrames: 50, splash: splash, assembler: ca65);
        Console.WriteLine($"  Cart assembled by ca65+ld65: {cart.Length} bytes (16K){(splash is not null ? "  with bitmap splash" : "")}  segments HEADER=$8000 BOOT=$8009 IRQ=$8200 BITMAP=$8300 SCREEN=$A240 COLOR=$A628 PAYLOAD=$AA10");

        Console.WriteLine("=== Attach cart + reset + wait for cart bootstrap marker ===");
        var boot = CartridgeBoot.AttachAndWaitForMarker(sys, cart, maxFrames: 600);
        if (!boot.MarkerSeen)
        {
            Console.WriteLine($"ERROR  Cart bootstrap marker not deposited within {boot.FramesUntilMarker} frames.");
            return 1;
        }
        Console.WriteLine($"BOOT   Cart bootstrap complete after {boot.FramesUntilMarker} frame(s); marker ${BootstrapCart.MarkerHi:X2}{BootstrapCart.MarkerLo:X2} seen at ${BootstrapCart.MarkerAddress:X4}.");
        Console.WriteLine("LIVE   CIA1 IRQ now drives Frost Point play and border cycle on the emulated 6510. Engine resumes game-side rendering.");

        var ctx = new GameContext(sys);
        var game = new DemoGame();

        if (headless)
        {
            game.Initialize(ctx);
            for (int i = 0; i < frames; i++)
            {
                game.Update(ctx, i);
                sys.RunFrame();
            }
            if (pngPath is not null)
            {
                FramebufferPng.Write(pngPath, sys.VideoChip.FrameBuffer, sys.VideoChip.FrameWidth, sys.VideoChip.FrameHeight);
                Console.WriteLine($"Wrote {pngPath}");
            }
            return 0;
        }

        Console.WriteLine("Starting CbmEngine demo window. Esc to exit.");
        using var host = new MonoGameHost(sys.Underlying, game: game, gameContext: ctx);
        host.Run();
        return 0;
    }

    private static int RunVideo(string assetPath, bool headless, int frames, string? pngPath)
    {
        var stream = OpenVideoAsset(assetPath);
        var player = new VideoPlayer(stream);
        Console.WriteLine($"=== Video asset opened: '{assetPath}' ===");
        Console.WriteLine($"  Header: {player.Header.FrameCount} frames @ {player.Header.FrameRate} fps  defaultMode={player.Header.DefaultMode}  loopHint={(player.Header.Flags & 1) != 0}");

        if (!Ca65Assembler.IsAvailable())
        {
            Console.WriteLine("ERROR  ca65/ld65 not found on PATH.");
            return 2;
        }
        var ca65 = new Ca65Assembler();
        var splash = player.PeekFrame(0);
        var cart = BitmapPlayerCart.Build(splash, ca65);
        Console.WriteLine($"  BitmapPlayerCart: {cart.Length} bytes (16K)  splash mode={splash.Mode}");

        var sys = CommodoreSystem.Build("c64", new RomProvider(FindRomBase()));
        Console.WriteLine("=== Attach cart + wait for marker ===");
        var boot = CartridgeBoot.AttachAndWaitForMarker(sys, cart, maxFrames: 600);
        if (!boot.MarkerSeen)
        {
            Console.WriteLine($"ERROR  Cart bootstrap marker not deposited within {boot.FramesUntilMarker} frames.");
            player.Dispose();
            return 1;
        }
        Console.WriteLine($"BOOT   Marker seen after {boot.FramesUntilMarker} frame(s).  Streaming video frames into VIC bank 1.");

        player.Reset();
        var ctx = new GameContext(sys);
        var game = new VideoGame(player);

        if (headless)
        {
            game.Initialize(ctx);
            for (int i = 0; i < frames; i++) { game.Update(ctx, i); sys.RunFrame(); }
            if (pngPath is not null)
            {
                FramebufferPng.Write(pngPath, sys.VideoChip.FrameBuffer, sys.VideoChip.FrameWidth, sys.VideoChip.FrameHeight);
                Console.WriteLine($"Wrote {pngPath}");
            }
            player.Dispose();
            return 0;
        }

        Console.WriteLine("Starting CbmEngine video window. Esc to exit.");
        using (var host = new MonoGameHost(sys.Underlying, game: game, gameContext: ctx))
            host.Run();
        player.Dispose();
        return 0;
    }

    private static int RunMidi(string midiPath, bool headless, int frames)
    {
        if (!File.Exists(midiPath))
        {
            var candidate = Path.Combine(AppContext.BaseDirectory, midiPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) midiPath = candidate;
        }
        if (!File.Exists(midiPath)) { Console.WriteLine($"ERROR  MIDI file not found: {midiPath}"); return 2; }

        var sys = CommodoreSystem.Build("c64", new RomProvider(FindRomBase()));
        Console.WriteLine($"=== MIDI -> SID  source={midiPath} ===");
        for (int i = 0; i < 120; i++) sys.RunFrame();
        sys.Sound.SetVolume(15);

        using var fs = File.OpenRead(midiPath);
        var smf = SmfReader.Load(fs);
        Console.WriteLine($"  SMF format={smf.Format} tracks={smf.TrackCount} tpq={smf.TicksPerQuarter}");

        using var bridge = new MidiSidBridge(sys);
        bridge.Load(smf);
        bridge.Play();
        var ctx = new GameContext(sys);
        var game = new MidiGame(bridge);

        if (headless)
        {
            game.Initialize(ctx);
            for (int i = 0; i < frames; i++) { game.Update(ctx, i); sys.RunFrame(); }
            Console.WriteLine($"Headless complete. VoicesActive={bridge.VoicesActive} CurrentTick={bridge.CurrentTick}");
            return 0;
        }

        Console.WriteLine("Starting MIDI playback window. Esc to exit.");
        using var host = new MonoGameHost(sys.Underlying, game: game, gameContext: ctx);
        host.Run();
        return 0;
    }

    private static Stream OpenVideoAsset(string assetPath)
    {
        if (File.Exists(assetPath)) return File.OpenRead(assetPath);
        var contentPath = Path.Combine(AppContext.BaseDirectory, assetPath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(contentPath)) return File.OpenRead(contentPath);
        try { return TitleContainer.OpenStream(assetPath); }
        catch (FileNotFoundException ex)
        {
            throw new FileNotFoundException($"Video asset not found at '{assetPath}'. Tried: filesystem path, ${nameof(AppContext.BaseDirectory)}, TitleContainer.", ex);
        }
    }

    private static int ParseInt(string[] args, string prefix, int defaultValue)
    {
        var s = args.FirstOrDefault(a => a.StartsWith(prefix));
        return s is null ? defaultValue : int.Parse(s.AsSpan(prefix.Length));
    }

    private static string? FindContentFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private static string FindRomBase()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CbmEngine.slnx"))) dir = dir.Parent;
        if (dir is null)
        {
            var probe = new DirectoryInfo(AppContext.BaseDirectory);
            while (probe is not null && !Directory.Exists(Path.Combine(probe.FullName, "external", "vice-sharp", "native", "vice", "vice", "data"))) probe = probe.Parent;
            if (probe is null) throw new InvalidOperationException("Could not locate ROM directory.");
            return Path.Combine(probe.FullName, "external", "vice-sharp", "native", "vice", "vice", "data");
        }
        return Path.Combine(dir.FullName, "external", "vice-sharp", "native", "vice", "vice", "data");
    }
}
