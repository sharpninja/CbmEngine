using CbmEngine.Host.MonoGame;
using CbmEngine.Systems;
using CbmEngine.Systems.Audio;
using CbmEngine.Systems.Boot;
using CbmEngine.Systems.Cartridge;
using CbmEngine.Systems.Strategy;
using ViceSharp.RomFetch;

namespace CbmEngine.Game.Sample;

public static class Program
{
    public static int Main(string[] args)
    {
        bool headless = args.Contains("--headless");
        string? pngPath = args.FirstOrDefault(a => a.StartsWith("--dump-png="))?.Substring("--dump-png=".Length);
        int frames = ParseInt(args, "--frames=", defaultValue: 60);

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

        var cart = PsidPlayerCart.Build(psid, backgroundColor: 0x01, initialBorderColor: 0x00, borderCyclePeriodFrames: 50, assembler: ca65);
        Console.WriteLine($"  Cart assembled by ca65+ld65: {cart.Length} bytes (16K)  segments HEADER=$8000 BOOT=$8009 IRQ=$8100 PAYLOAD=$8400");

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
