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
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using ViceSharp.RomFetch;

namespace CbmEngine.Game.Sample;

public static class Program
{
    public static int Main(string[] args)
    {
        // ILogger remediation (review + BDP slice in scope): use structured logging instead of raw Console
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger("CbmEngine.Sample");

        bool headless = args.Contains("--headless");
        string? pngPath = args.FirstOrDefault(a => a.StartsWith("--dump-png="))?.Substring("--dump-png=".Length);
        int frames = ParseInt(args, "--frames=", defaultValue: 60);

        string? videoAsset = args.FirstOrDefault(a => a.StartsWith("--video="))?["--video=".Length..];
        if (videoAsset is not null) return RunVideo(videoAsset, headless, frames, pngPath, logger);

        string? midiPath = args.FirstOrDefault(a => a.StartsWith("--midi="))?["--midi=".Length..];
        if (midiPath is not null) return RunMidi(midiPath, headless, frames, logger);

        var romBase = FindRomBase();
        var roms = new RomProvider(romBase);

        var sys = CommodoreSystem.Build("c64", roms);

        logger.LogInformation("=== Build PsidPlayerCart with embedded Frost Point (assembled via CA65) ===");
        if (!Ca65Assembler.IsAvailable())
        {
            logger.LogError("ca65/ld65 not found on PATH. Install CC65 (https://cc65.github.io) and ensure ca65.exe + ld65.exe are reachable.");
            return 2;
        }
        var ca65 = new Ca65Assembler();
        logger.LogInformation("  Toolchain: {Ca65} {Ld65}", ca65.Ca65Path, ca65.Ld65Path);
        var sidPath = FindContentFile("assets/sid/Frost_Point.sid")
            ?? throw new InvalidOperationException("Frost_Point.sid not found.");
        using var fs = File.OpenRead(sidPath);
        var psid = PsidLoader.Load(fs);
        logger.LogInformation("  SID: '{Name}' by {Author}  load=${Load:X4} init=${Init:X4} play=${Play:X4}  payload={Payload}B",
            psid.Header.Name, psid.Header.Author, psid.Header.LoadAddress, psid.Header.InitAddress, psid.Header.PlayAddress, psid.Payload.Length);

        EncodedSplashBitmap? splash = null;
        var splashPng = FindContentFile("artifacts/captures/frost-point-title.png");
        if (splashPng is not null)
        {
            splash = C64MulticolorBitmapEncoder.Encode(splashPng, forceBackgroundColor: 0x00);
            logger.LogInformation("  Splash: {Png}  ({Size}B bitmap, bg=${Bg:X1}, mode={Mode})", splashPng, splash.Bitmap.Length, splash.BackgroundColorIndex, splash.Mode);
        }
        else
        {
            logger.LogInformation("  Splash: none (artifacts/captures/frost-point-title.png missing -- run tools/CaptureFrostPoint.ps1 + convert-bmp.ps1)");
        }

        var cart = PsidPlayerCart.Build(psid, backgroundColor: 0x00, initialBorderColor: 0x00, borderCyclePeriodFrames: 50, splash: splash, assembler: ca65);
        logger.LogInformation("  Cart assembled by ca65+ld65: {Size} bytes (16K){WithSplash}  segments HEADER=$8000 BOOT=$8009 IRQ=$8200 BITMAP=$8300 SCREEN=$A240 COLOR=$A628 PAYLOAD=$AA10",
            cart.Length, (splash is not null ? "  with bitmap splash" : ""));

        logger.LogInformation("=== Attach cart + reset + wait for cart bootstrap marker ===");
        var boot = CartridgeBoot.AttachAndWaitForMarker(sys, cart, maxFrames: 600);
        if (!boot.MarkerSeen)
        {
            logger.LogError("Cart bootstrap marker not deposited within {Frames} frames.", boot.FramesUntilMarker);
            return 1;
        }
        logger.LogInformation("BOOT   Cart bootstrap complete after {Frames} frame(s); marker ${Hi:X2}{Lo:X2} seen at ${Addr:X4}.",
            boot.FramesUntilMarker, BootstrapCart.MarkerHi, BootstrapCart.MarkerLo, BootstrapCart.MarkerAddress);
        logger.LogInformation("LIVE   CIA1 IRQ now drives Frost Point play and border cycle on the emulated 6510. Engine resumes game-side rendering.");

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
                logger.LogInformation("Wrote {Png}", pngPath);
            }
            return 0;
        }

        logger.LogInformation("Starting CbmEngine demo window. Esc to exit.");
        using var host = new MonoGameHost(sys.Underlying, game: game, gameContext: ctx);
        host.Run();
        return 0;
    }

    private static int RunVideo(string assetPath, bool headless, int frames, string? pngPath, ILogger logger)
    {
        var stream = OpenVideoAsset(assetPath);
        var player = new VideoPlayer(stream);
        logger.LogInformation("=== Video asset opened: '{Path}' ===", assetPath);
        logger.LogInformation("  Header: {Count} frames @ {Fps} fps  defaultMode={Mode}  loopHint={Loop}", player.Header.FrameCount, player.Header.FrameRate, player.Header.DefaultMode, (player.Header.Flags & 1) != 0);

        if (!Ca65Assembler.IsAvailable())
        {
            logger.LogError("ca65/ld65 not found on PATH.");
            return 2;
        }
        var ca65 = new Ca65Assembler();
        var splash = player.PeekFrame(0);
        var cart = BitmapPlayerCart.Build(splash, ca65);
        logger.LogInformation("  BitmapPlayerCart: {Size} bytes (16K)  splash mode={Mode}", cart.Length, splash.Mode);

        var sys = CommodoreSystem.Build("c64", new RomProvider(FindRomBase()));
        logger.LogInformation("=== Attach cart + wait for marker ===");
        var boot = CartridgeBoot.AttachAndWaitForMarker(sys, cart, maxFrames: 600);
        if (!boot.MarkerSeen)
        {
            logger.LogError("Cart bootstrap marker not deposited within {Frames} frames.", boot.FramesUntilMarker);
            player.Dispose();
            return 1;
        }
        logger.LogInformation("BOOT   Marker seen after {Frames} frame(s).  Streaming video frames into VIC bank 1.", boot.FramesUntilMarker);

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
                logger.LogInformation("Wrote {Png}", pngPath);
            }
            player.Dispose();
            return 0;
        }

        logger.LogInformation("Starting CbmEngine video window. Esc to exit.");
        using (var host = new MonoGameHost(sys.Underlying, game: game, gameContext: ctx))
            host.Run();
        player.Dispose();
        return 0;
    }

    private static int RunMidi(string midiPath, bool headless, int frames, ILogger logger)
    {
        if (!File.Exists(midiPath))
        {
            var candidate = Path.Combine(AppContext.BaseDirectory, midiPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) midiPath = candidate;
        }
        if (!File.Exists(midiPath)) { logger.LogError("MIDI file not found: {Path}", midiPath); return 2; }

        var sys = CommodoreSystem.Build("c64", new RomProvider(FindRomBase()));
        logger.LogInformation("=== MIDI -> SID  source={Path} ===", midiPath);
        for (int i = 0; i < 120; i++) sys.RunFrame();
        sys.Sound.SetVolume(15);

        using var fs = File.OpenRead(midiPath);
        var smf = SmfReader.Load(fs);
        logger.LogInformation("  SMF format={Format} tracks={Tracks} tpq={Tpq}", smf.Format, smf.TrackCount, smf.TicksPerQuarter);

        using var bridge = new MidiSidBridge(sys);
        bridge.Load(smf);
        bridge.Play();
        var ctx = new GameContext(sys);
        var game = new MidiGame(bridge);

        if (headless)
        {
            game.Initialize(ctx);
            for (int i = 0; i < frames; i++) { game.Update(ctx, i); sys.RunFrame(); }
            logger.LogInformation("Headless complete. VoicesActive={Voices} CurrentTick={Tick}", bridge.VoicesActive, bridge.CurrentTick);
            return 0;
        }

        logger.LogInformation("Starting MIDI playback window. Esc to exit.");
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

    private static string FindRomBase() => CbmEngine.Systems.Strategy.RomDiscovery.DiscoverRomBase();
}
