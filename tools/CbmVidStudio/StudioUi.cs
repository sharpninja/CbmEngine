using CbmEngine.Pipeline.CbmVid;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.TextureAtlases;
using Myra.Graphics2D.UI;
using SixLabors.ImageSharp.PixelFormats;
using IsImage = SixLabors.ImageSharp.Image;
using ImageWidget = Myra.Graphics2D.UI.Image;

namespace CbmEngine.Tools.CbmVidStudio;

internal sealed class StudioUi : IDisposable
{
    private readonly GraphicsDevice _gd;
    private readonly ImageWidget _sourceImage;
    private readonly ImageWidget _emulatorImage;
    private readonly ListView _frameList;
    private readonly Label _statusLabel;
    private readonly ComboView _modeCombo;
    private readonly SpinButton _backgroundSpin;
    private readonly SpinButton _fpsSpin;
    private readonly IconFactory _icons;
    private Texture2D? _sourceTexture;

    public Panel Root { get; }

    public event Action? OnOpenFolder;
    public event Action? OnOpenVideo;
    public event Action? OnSave;
    public event Action? OnExportGif;
    public event Action? OnExit;
    public event Action<int>? OnFrameSelected;
    public event Action<CbmVidFrameMode>? OnFrameModeChanged;
    public event Action<byte>? OnBackgroundColorChanged;
    public event Action<int>? OnFpsChanged;
    public event Action? OnReencode;

    private readonly int _s;   // structural metric scale (DPI factor rounded for paddings/widths)

    public StudioUi(GraphicsDevice gd, float uiScale = 1f)
    {
        _gd = gd;
        _s = Math.Max(1, (int)MathF.Round(uiScale));
        int S(int logical) => logical * _s;

        var menu = new HorizontalMenu();
        var fileItem = new MenuItem { Text = "File" };
        var openItem = new MenuItem { Text = "Open Folder of PNGs..." };
        openItem.Selected += (_, _) => OnOpenFolder?.Invoke();
        var openVideoItem = new MenuItem { Text = "Open Video (mp4/avi/mov/...)..." };
        openVideoItem.Selected += (_, _) => OnOpenVideo?.Invoke();
        var saveItem = new MenuItem { Text = "Save .cbmvid..." };
        saveItem.Selected += (_, _) => OnSave?.Invoke();
        var exportGifItem = new MenuItem { Text = "Export Animated GIF (preview)..." };
        exportGifItem.Selected += (_, _) => OnExportGif?.Invoke();
        var exitItem = new MenuItem { Text = "Exit" };
        exitItem.Selected += (_, _) => OnExit?.Invoke();
        fileItem.Items.Add(openItem);
        fileItem.Items.Add(openVideoItem);
        fileItem.Items.Add(saveItem);
        fileItem.Items.Add(exportGifItem);
        fileItem.Items.Add(new MenuSeparator());
        fileItem.Items.Add(exitItem);
        menu.Items.Add(fileItem);

        // Toolbar: icon buttons mirroring the File menu actions. Icons are pixel-art scaled by
        // integer factor so they stay crisp at any DPI.
        _icons = new IconFactory(gd, pixelScale: Math.Clamp(_s, 1, 4));
        var toolbar = new HorizontalStackPanel
        {
            Spacing = S(6),
            Padding = new Thickness(S(6), S(4)),
            Background = new SolidBrush(new Color(38, 38, 42)),
        };
        toolbar.Widgets.Add(MakeToolButton(_icons.OpenFolder, "Open", () => OnOpenFolder?.Invoke()));
        toolbar.Widgets.Add(MakeToolButton(_icons.OpenVideo, "Video", () => OnOpenVideo?.Invoke()));
        toolbar.Widgets.Add(MakeToolButton(_icons.Save, "Save", () => OnSave?.Invoke()));
        toolbar.Widgets.Add(MakeToolButton(_icons.ExportGif, "GIF", () => OnExportGif?.Invoke()));
        toolbar.Widgets.Add(MakeToolButton(_icons.Reencode, "Re-encode", () => OnReencode?.Invoke()));

        _frameList = new ListView { Background = new SolidBrush(new Color(45, 45, 48)) };
        _frameList.SelectedIndexChanged += (_, _) => { if (_frameList.SelectedIndex.HasValue) OnFrameSelected?.Invoke(_frameList.SelectedIndex.Value); };
        var framesScroll = new ScrollViewer { Content = _frameList };

        _sourceImage = new ImageWidget { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var srcLabel = new Label { Text = "Source PNG (native)", HorizontalAlignment = HorizontalAlignment.Center };
        var srcStack = new VerticalStackPanel { Spacing = S(4), Padding = new Thickness(S(8)) };
        srcStack.Widgets.Add(srcLabel);
        srcStack.Widgets.Add(new HorizontalSeparator());
        srcStack.Widgets.Add(_sourceImage);

        _emulatorImage = new ImageWidget { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var emuLabel = new Label { Text = "Encoded -> C64 Emulator", HorizontalAlignment = HorizontalAlignment.Center };
        var emuStack = new VerticalStackPanel { Spacing = S(4), Padding = new Thickness(S(8)) };
        emuStack.Widgets.Add(emuLabel);
        emuStack.Widgets.Add(new HorizontalSeparator());
        emuStack.Widgets.Add(_emulatorImage);

        var centerSplit = new HorizontalStackPanel { Spacing = S(4) };
        centerSplit.Widgets.Add(srcStack);
        centerSplit.Widgets.Add(new VerticalSeparator());
        centerSplit.Widgets.Add(emuStack);

        var settingsStack = new VerticalStackPanel { Spacing = S(6), Padding = new Thickness(S(8)) };
        settingsStack.Widgets.Add(new Label { Text = "Encoder Settings", HorizontalAlignment = HorizontalAlignment.Center });
        settingsStack.Widgets.Add(new HorizontalSeparator());

        settingsStack.Widgets.Add(new Label { Text = "Mode (current frame):" });
        _modeCombo = new ComboView();
        _modeCombo.Widgets.Add(new Label { Text = "Multicolor" });
        _modeCombo.Widgets.Add(new Label { Text = "HiRes" });
        _modeCombo.SelectedIndex = 0;
        _modeCombo.SelectedIndexChanged += (_, _) =>
        {
            var mode = _modeCombo.SelectedIndex == 1 ? CbmVidFrameMode.HiRes : CbmVidFrameMode.Multicolor;
            OnFrameModeChanged?.Invoke(mode);
        };
        settingsStack.Widgets.Add(_modeCombo);

        settingsStack.Widgets.Add(new Label { Text = "Forced background (0-15):" });
        _backgroundSpin = new SpinButton { Minimum = 0, Maximum = 15, Value = 0, Integer = true };
        _backgroundSpin.ValueChanged += (_, _) => OnBackgroundColorChanged?.Invoke((byte)(_backgroundSpin.Value ?? 0));
        settingsStack.Widgets.Add(_backgroundSpin);

        settingsStack.Widgets.Add(new Label { Text = "Frame rate (Hz):" });
        _fpsSpin = new SpinButton { Minimum = 1, Maximum = 60, Value = 50, Integer = true };
        _fpsSpin.ValueChanged += (_, _) => OnFpsChanged?.Invoke((int)(_fpsSpin.Value ?? 50));
        settingsStack.Widgets.Add(_fpsSpin);

        var reencodeBtn = new Button { Content = new Label { Text = "Re-encode current frame" } };
        reencodeBtn.Click += (_, _) => OnReencode?.Invoke();
        settingsStack.Widgets.Add(reencodeBtn);

        _statusLabel = new Label { Text = "", Padding = new Thickness(S(8), S(4)) };

        var mainGrid = new Grid { ColumnSpacing = S(4), RowSpacing = S(4) };
        mainGrid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, S(240)));
        mainGrid.ColumnsProportions.Add(new Proportion(ProportionType.Fill));
        mainGrid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, S(260)));
        mainGrid.RowsProportions.Add(new Proportion(ProportionType.Fill));
        Grid.SetColumn(framesScroll, 0);
        Grid.SetColumn(centerSplit, 1);
        Grid.SetColumn(settingsStack, 2);
        mainGrid.Widgets.Add(framesScroll);
        mainGrid.Widgets.Add(centerSplit);
        mainGrid.Widgets.Add(settingsStack);

        var rootStack = new VerticalStackPanel { Spacing = 0 };
        rootStack.Widgets.Add(menu);
        rootStack.Widgets.Add(toolbar);
        rootStack.Widgets.Add(mainGrid);
        rootStack.Widgets.Add(_statusLabel);
        rootStack.HorizontalAlignment = HorizontalAlignment.Stretch;
        rootStack.VerticalAlignment = VerticalAlignment.Stretch;

        Root = new Panel { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        Root.Widgets.Add(rootStack);
    }

    public void PopulateFrames(IReadOnlyList<FrameEntry> frames)
    {
        _frameList.Widgets.Clear();
        for (int i = 0; i < frames.Count; i++)
            _frameList.Widgets.Add(new Label { Text = $"{i:D4}  {Path.GetFileName(frames[i].PngPath)}" });
    }

    public void SelectFrame(int index)
    {
        if (index >= 0 && index < _frameList.Widgets.Count) _frameList.SelectedIndex = index;
    }

    public void SetStatus(string text) => _statusLabel.Text = text;

    public void UpdateSourcePreview(string pngPath)
    {
        _sourceTexture?.Dispose();
        using var image = IsImage.Load<Rgba32>(pngPath);
        var pixels = new Color[image.Width * image.Height];
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                    pixels[y * accessor.Width + x] = new Color(row[x].R, row[x].G, row[x].B, row[x].A);
            }
        });
        var tex = new Texture2D(_gd, image.Width, image.Height, false, SurfaceFormat.Color);
        tex.SetData(pixels);
        _sourceTexture = tex;
        _sourceImage.Renderable = new TextureRegion(tex);
    }

    public void UpdateEmulatorPreview(Texture2D? framebufferTexture)
    {
        _emulatorImage.Renderable = framebufferTexture is null ? null : new TextureRegion(framebufferTexture);
    }

    private Button MakeToolButton(Texture2D icon, string label, Action onClick)
    {
        var content = new HorizontalStackPanel { Spacing = 4 * _s, Padding = new Thickness(6 * _s, 2 * _s) };
        content.Widgets.Add(new ImageWidget
        {
            Renderable = new TextureRegion(icon),
            VerticalAlignment = VerticalAlignment.Center,
        });
        content.Widgets.Add(new Label { Text = label, VerticalAlignment = VerticalAlignment.Center });
        var button = new Button { Content = content };
        button.Click += (_, _) =>
        {
            StudioDiag.Log($"toolbar: {label}");
            onClick();
        };
        return button;
    }

    public void Dispose()
    {
        _sourceTexture?.Dispose();
        _icons.Dispose();
    }
}
