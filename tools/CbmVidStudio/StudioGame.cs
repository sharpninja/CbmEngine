using CbmEngine.Pipeline;
using CbmEngine.Pipeline.CbmVid;
using CbmEngine.Systems.Video;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.File;
using ViceSharp.RomFetch;

namespace CbmEngine.Tools.CbmVidStudio;

public sealed class StudioGame : Game
{
    private readonly GraphicsDeviceManager _gdm;
    private Desktop? _desktop;
    private SpriteBatch? _spriteBatch;

    private StudioUi? _ui;
    private StudioEmulator? _emulator;

    private readonly List<FrameEntry> _frames = new();
    private FrameEntry? _selected;
    private string? _sourceDir;
    private byte _forcedBackgroundColor = 0x00;
    private int _fps = 50;
    private CbmVidFrameMode _defaultMode = CbmVidFrameMode.Multicolor;

    private Rectangle _lastClientBounds;

    public StudioGame(string[] args)
    {
        // Initial client-size request from MEASURED display + DPI (valid because Program.Main
        // enabled PerMonitorV2 before any window/SDL initialization). Whatever the OS actually
        // grants is re-measured every frame in Update and the viewport re-synced.
        int initialW = StudioLayoutMath.LogicalWidth;
        int initialH = StudioLayoutMath.LogicalHeight;
        try
        {
            var dm = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            (initialW, initialH) = StudioLayoutMath.ComputeInitialClientSize(dm.Width, dm.Height, DpiNormalizer.SystemDpiFactor());
        }
        catch { /* fall back to logical canvas size */ }

        _gdm = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = initialW,
            PreferredBackBufferHeight = initialH,
            SynchronizeWithVerticalRetrace = true,
        };
        IsMouseVisible = true;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromMilliseconds(16.66);
        Window.AllowUserResizing = true;
        Window.Title = "CbmVidStudio";
        if (args.Length > 0 && Directory.Exists(args[0])) _sourceDir = args[0];
    }

    /// <summary>
    /// One sizing path: match the backbuffer to the ACTUAL client bounds. Runs on the first
    /// frame and whenever the OS changes the client size (resize, maximize, DPI move) -
    /// detected by polling in Update, which is immune to event-ordering surprises. No
    /// Desktop.Scale: DPI is handled by font size + scaled widget metrics (StudioTheme), so
    /// Myra lays out and positions popups in native pixels with no transform involved.
    /// </summary>
    private void SyncViewport(Rectangle bounds)
    {
        if (_desktop is null) return;
        if (bounds.Width < 200 || bounds.Height < 150) return;

        if (_gdm.PreferredBackBufferWidth != bounds.Width || _gdm.PreferredBackBufferHeight != bounds.Height)
        {
            _gdm.PreferredBackBufferWidth = bounds.Width;
            _gdm.PreferredBackBufferHeight = bounds.Height;
            _gdm.ApplyChanges();
        }

        StudioDiag.Log($"sync client={bounds.Width}x{bounds.Height}");
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        MyraEnvironment.Game = this;

        StudioDiag.Reset();
        nint hwnd = 0;
        try { hwnd = Window.Handle; } catch { }
        var dm = GraphicsDevice.Adapter.CurrentDisplayMode;
        float uiScale = MathF.Max(1f, DpiNormalizer.SystemDpiFactor());
        StudioDiag.Log($"display={dm.Width}x{dm.Height} systemDpiFactor={DpiNormalizer.SystemDpiFactor():F2} windowDpiFactor={DpiNormalizer.WindowDpiFactor(hwnd):F2} uiScale={uiScale:F2}");

        // Theme BEFORE any widget is constructed: widgets copy style values (fonts included) at
        // creation time. This replaces Desktop.Scale entirely - everything renders at native
        // pixels so Myra's dialog/menu placement math works unmodified.
        bool themed = StudioTheme.Apply(uiScale);
        StudioDiag.Log($"theme: ttf={(themed ? "applied" : "unavailable, default font")} fontPx={StudioTheme.FontPixels(uiScale)}");

        _ui = new StudioUi(GraphicsDevice, uiScale);
        _ui.OnOpenFolder += OpenFolderDialog;
        _ui.OnOpenVideo += OpenVideoDialog;
        _ui.OnSave += SaveCbmVid;
        _ui.OnExportGif += ExportGifDialog;
        _ui.OnExit += Exit;
        _ui.OnFrameSelected += OnFrameSelected;
        _ui.OnFrameModeChanged += OnFrameModeChanged;
        _ui.OnBackgroundColorChanged += OnBackgroundColorChanged;
        _ui.OnFpsChanged += fps => _fps = fps;
        _ui.OnReencode += ReencodeSelected;

        _desktop = new Desktop { Root = _ui.Root };

        StudioDiag.Log($"initial client={Window.ClientBounds.Width}x{Window.ClientBounds.Height} backbuffer={_gdm.PreferredBackBufferWidth}x{_gdm.PreferredBackBufferHeight}");

        _lastClientBounds = Window.ClientBounds;
        SyncViewport(_lastClientBounds);

        InitializeEmulator();
        if (_sourceDir is not null) LoadFolder(_sourceDir);
        _ui.SetStatus("Ready. Open a folder of frame_*.png files to begin.");
    }


    private void InitializeEmulator()
    {
        string? romBase;
        try
        {
            romBase = StudioRoms.ResolveOrDownloadAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            StudioDiag.Log($"emulator: ROM acquisition failed: {ex.Message}");
            romBase = null;
        }
        if (romBase is null)
        {
            StudioDiag.Log("emulator: no ROMs found (bundled/env/cache all missed, download failed)");
            _ui!.SetStatus($"Emulator preview disabled: C64 ROMs not available. Set {StudioRoms.EnvVar} to a VICE data directory.");
            return;
        }
        try
        {
            var roms = new RomProvider(romBase);
            _emulator = new StudioEmulator(GraphicsDevice, roms);
            StudioDiag.Log($"emulator: ready, roms={romBase}");
            _ui!.SetStatus($"Emulator ready (ROMs at {romBase})");
        }
        catch (Exception ex)
        {
            StudioDiag.Log($"emulator: init failed: {ex}");
            _ui!.SetStatus($"Emulator init failed: {ex.Message}");
        }
    }

    private void OpenVideoDialog()
    {
        var fd = new FileDialog(FileDialogMode.OpenFile)
        {
            Filter = "*.mp4|*.avi|*.mov|*.mkv|*.webm|*.gif",
            Title = "Pick a video file - .gif uses native importer (no ffmpeg); others go through ffmpeg",
        };
        fd.Closed += (_, _) =>
        {
            if (!fd.Result || string.IsNullOrWhiteSpace(fd.FilePath)) return;
            var ext = Path.GetExtension(fd.FilePath).ToLowerInvariant();
            if (ext == ".gif") { LoadAnimatedGif(fd.FilePath); return; }
            LoadVideoViaFfmpeg(fd.FilePath);
        };
        fd.ShowModal(_desktop!);
    }

    private void LoadAnimatedGif(string gifPath)
    {
        var scratch = Path.Combine(Path.GetTempPath(), $"cbmvid-studio-{Guid.NewGuid():N}");
        try
        {
            _ui!.SetStatus($"Decoding {Path.GetFileName(gifPath)} via native GIF importer...");
            Directory.CreateDirectory(scratch);
            var tempOut = Path.Combine(scratch, "preview.cbmvid");
            CbmVidEncoder.EncodeAnimatedGif(gifPath, tempOut, defaultMode: _defaultMode, log: msg => _ui!.SetStatus(msg));
            // Decode the .cbmvid back to per-frame PNGs the source-preview can show.
            ExpandCbmvidToPngs(tempOut, scratch);
            LoadFolder(scratch);
            _ui!.SetStatus($"Loaded {_frames.Count} GIF frames (native import, no ffmpeg)");
        }
        catch (Exception ex)
        {
            _ui!.SetStatus($"GIF import failed: {ex.Message}");
        }
    }

    private static void ExpandCbmvidToPngs(string cbmvidPath, string outDir)
    {
        using var fs = File.OpenRead(cbmvidPath);
        using var player = new VideoPlayer(fs);
        for (int i = 0; i < player.Header.FrameCount; i++)
        {
            var frame = player.PeekFrame(i);
            using var img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(320, 200);
            C64MulticolorBitmapEncoder.WriteDebugPreview(frame, Path.Combine(outDir, $"frame_{i:D4}.png"));
        }
    }

    private void LoadVideoViaFfmpeg(string videoPath)
    {
        var scratch = Path.Combine(Path.GetTempPath(), $"cbmvid-studio-{Guid.NewGuid():N}");
        try
        {
            _ui!.SetStatus($"Decoding {Path.GetFileName(videoPath)} via ffmpeg...");
            Directory.CreateDirectory(scratch);
            var tempOut = Path.Combine(scratch, "preview.cbmvid");
            CbmVidEncoder.EncodeVideo(videoPath, tempOut, frameRate: 50, defaultMode: _defaultMode, scratchDirectory: scratch, keepIntermediateFrames: true, log: msg => _ui!.SetStatus(msg));
            LoadFolder(scratch);
            _ui!.SetStatus($"Loaded {_frames.Count} frames decoded from {Path.GetFileName(videoPath)}");
        }
        catch (Exception ex)
        {
            _ui!.SetStatus($"Video decode failed: {ex.Message}");
        }
    }

    private void OpenFolderDialog()
    {
        var fd = new FileDialog(FileDialogMode.OpenFile)
        {
            Filter = "*.png",
            Title = "Pick any frame_*.png in the source directory",
        };
        if (_sourceDir is not null) fd.Folder = _sourceDir;
        fd.Closed += (_, _) =>
        {
            if (!fd.Result || string.IsNullOrWhiteSpace(fd.FilePath)) return;
            var dir = Path.GetDirectoryName(fd.FilePath);
            if (dir is not null) LoadFolder(dir);
        };
        fd.ShowModal(_desktop!);
    }

    private void LoadFolder(string folder)
    {
        _sourceDir = folder;
        _frames.Clear();
        var files = Directory.EnumerateFiles(folder, "frame_*.png", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        for (int i = 0; i < files.Length; i++)
        {
            _frames.Add(new FrameEntry(i, files[i], _defaultMode));
        }
        _ui!.PopulateFrames(_frames);
        _ui.SetStatus($"Loaded {_frames.Count} frame(s) from {folder}");
        if (_frames.Count > 0) _ui.SelectFrame(0);
    }

    private void OnFrameSelected(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= _frames.Count) return;
        StudioDiag.Log($"frame selected: {frameIndex}");
        _selected = _frames[frameIndex];
        _ui!.UpdateSourcePreview(_selected.PngPath);
        ReencodeSelected();
        _ui.SetStatus($"Frame {frameIndex + 1}/{_frames.Count}: {Path.GetFileName(_selected.PngPath)}");
    }

    private void OnFrameModeChanged(CbmVidFrameMode mode)
    {
        if (_selected is null) return;
        _selected.Mode = mode;
        ReencodeSelected();
    }

    private void OnBackgroundColorChanged(byte bg)
    {
        _forcedBackgroundColor = bg;
        ReencodeSelected();
    }

    private void ReencodeSelected()
    {
        if (_selected is null || _emulator is null) return;
        try
        {
            var encoded = _selected.Mode == CbmVidFrameMode.HiRes
                ? C64MulticolorBitmapEncoder.EncodeHiRes(_selected.PngPath)
                : C64MulticolorBitmapEncoder.Encode(_selected.PngPath, _forcedBackgroundColor);
            _selected.Encoded = encoded;
            _emulator.RenderFrame(encoded);
            _ui!.UpdateEmulatorPreview(_emulator.PreviewTexture);
        }
        catch (Exception ex)
        {
            _ui!.SetStatus($"Encode failed: {ex.Message}");
        }
    }

    private void ExportGifDialog()
    {
        if (_frames.Count == 0) { _ui!.SetStatus("Nothing to export: no frames loaded."); return; }

        var fd = new FileDialog(FileDialogMode.SaveFile)
        {
            Filter = "*.gif",
            Title = "Export animated GIF preview",
        };
        if (_sourceDir is not null) fd.Folder = _sourceDir;
        fd.Closed += (_, _) =>
        {
            if (!fd.Result || string.IsNullOrWhiteSpace(fd.FilePath)) return;
            try
            {
                _ui!.SetStatus("Encoding .cbmvid in memory + rendering through emulator...");
                using var ms = new MemoryStream();
                var entries = _frames.Select(f => new CbmVidEncodeManifest.Entry(f.PngPath, f.Mode, _forcedBackgroundColor)).ToArray();
                var manifest = new CbmVidEncodeManifest(fd.FilePath + ".tmp", entries, (ushort)_fps, _defaultMode, Flags: 1);
                var tempCbmvid = Path.Combine(Path.GetTempPath(), $"cbmvid-gif-{Guid.NewGuid():N}.cbmvid");
                manifest = manifest with { OutputPath = tempCbmvid };
                CbmVidEncoder.Encode(manifest);
                string romBase = StudioRoms.ResolveOrDownloadAsync().GetAwaiter().GetResult();
                var roms = new RomProvider(romBase);
                CbmVidGifExporter.Export(tempCbmvid, fd.FilePath, roms, log: msg => _ui!.SetStatus(msg));
                try { File.Delete(tempCbmvid); } catch { }
                _ui!.SetStatus($"Saved animated GIF: {fd.FilePath}");
            }
            catch (Exception ex) { _ui!.SetStatus($"GIF export failed: {ex.Message}"); }
        };
        fd.ShowModal(_desktop!);
    }

    private void SaveCbmVid()
    {
        if (_frames.Count == 0) { _ui!.SetStatus("Nothing to save: no frames loaded."); return; }

        var fd = new FileDialog(FileDialogMode.SaveFile)
        {
            Filter = "*.cbmvid",
            Title = "Save .cbmvid",
        };
        if (_sourceDir is not null) fd.Folder = _sourceDir;
        fd.Closed += (_, _) =>
        {
            if (!fd.Result || string.IsNullOrWhiteSpace(fd.FilePath)) return;
            try
            {
                var entries = _frames.Select(f => new CbmVidEncodeManifest.Entry(f.PngPath, f.Mode, _forcedBackgroundColor)).ToArray();
                var manifest = new CbmVidEncodeManifest(fd.FilePath, entries, (ushort)_fps, _defaultMode, Flags: 1);
                CbmVidEncoder.Encode(manifest);
                _ui!.SetStatus($"Saved {fd.FilePath} ({_frames.Count} frames)");
            }
            catch (Exception ex) { _ui!.SetStatus($"Save failed: {ex.Message}"); }
        };
        fd.ShowModal(_desktop!);
    }

    // Heavy diagnostics (frame dumps + heartbeats) only when CBMVID_DIAG=1; lightweight event
    // logs (clicks, emulator init, viewport sync) are always on - they are rare and tiny.
    private static readonly bool DiagMode = Environment.GetEnvironmentVariable("CBMVID_DIAG") == "1";

    private long _updateCount;
    private long _drawCount;

    protected override void Update(GameTime gameTime)
    {
        _updateCount++;
        if (DiagMode && _updateCount % 300 == 0) StudioDiag.Log($"heartbeat updates={_updateCount} draws={_drawCount}");
        var bounds = Window.ClientBounds;
        if (bounds.Width != _lastClientBounds.Width || bounds.Height != _lastClientBounds.Height)
        {
            _lastClientBounds = bounds;
            SyncViewport(bounds);
        }
        base.Update(gameTime);
    }

    private int _drawDiagCount;

    protected override void Draw(GameTime gameTime)
    {
        _drawCount++;
        GraphicsDevice.Clear(new Color(30, 30, 30));
        // Desktop.Render() is the full Myra pipeline: layout -> input -> per-widget ProcessInput
        // -> InputEventsManager.ProcessEvents -> layout -> visual. Calling the pieces separately
        // (UpdateInput/UpdateLayout/RenderVisual) SKIPS event delivery - clicks queue up and are
        // never dispatched, which is exactly the "UI doesn't respond" bug.
        try
        {
            _desktop?.Render();
        }
        catch (Exception ex)
        {
            if (_drawDiagCount < 10)
            {
                _drawDiagCount++;
                StudioDiag.Log($"draw EXCEPTION: {ex}");
            }
        }

        // Ground-truth frame dump: what did WE render, independent of any OS capture layer.
        // Diagnostics-only (CBMVID_DIAG=1); refreshes every ~300 draws.
        if (DiagMode && (_drawCount == 90 || (_drawCount > 90 && _drawCount % 300 == 0)))
        {
            try
            {
                int w = GraphicsDevice.PresentationParameters.BackBufferWidth;
                int h = GraphicsDevice.PresentationParameters.BackBufferHeight;
                var data = new Color[w * h];
                GraphicsDevice.GetBackBufferData(data);
                string dump = Path.Combine(Path.GetTempPath(), "cbmvid-framedump.png");
                using var img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(w, h);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var c = data[y * w + x];
                        img[x, y] = new SixLabors.ImageSharp.PixelFormats.Rgba32(c.R, c.G, c.B, 255);
                    }
                SixLabors.ImageSharp.ImageExtensions.SaveAsPng(img, dump);
                StudioDiag.Log($"framedump: {dump} ({w}x{h})");
            }
            catch (Exception ex)
            {
                StudioDiag.Log($"framedump failed: {ex.Message}");
            }
        }
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _emulator?.Dispose();
        _spriteBatch?.Dispose();
        base.UnloadContent();
    }
}

internal sealed class FrameEntry
{
    public int Index { get; }
    public string PngPath { get; }
    public CbmVidFrameMode Mode { get; set; }
    public EncodedSplashBitmap? Encoded { get; set; }

    public FrameEntry(int index, string pngPath, CbmVidFrameMode mode)
    {
        Index = index;
        PngPath = pngPath;
        Mode = mode;
    }
}
