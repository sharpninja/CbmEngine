using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CbmEngine.Host.MonoGame;

public sealed class FpsOverlay : IDisposable
{
    private readonly GraphicsDevice _device;
    private Texture2D? _pixel;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private int _framesThisWindow;
    private double _windowStartSeconds;
    private double _measuredFps;

    public double MeasuredFps => _measuredFps;

    public FpsOverlay(GraphicsDevice device)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
    }

    public void RegisterFrame()
    {
        _framesThisWindow++;
        double now = _clock.Elapsed.TotalSeconds;
        if (now - _windowStartSeconds >= 1.0)
        {
            _measuredFps = _framesThisWindow / (now - _windowStartSeconds);
            _framesThisWindow = 0;
            _windowStartSeconds = now;
        }
    }

    public void Draw(SpriteBatch spriteBatch, int x = 6, int y = 6, int pixelSize = 2)
    {
        if (_pixel is null)
        {
            _pixel = new Texture2D(_device, 1, 1, false, SurfaceFormat.Color);
            _pixel.SetData(new[] { Color.White });
        }
        string text = $"FPS {_measuredFps:F1}";
        DrawString(spriteBatch, text, x, y, pixelSize);
    }

    private void DrawString(SpriteBatch spriteBatch, string text, int x, int y, int pixelSize)
    {
        if (_pixel is null) return;
        int pad = 2 * pixelSize;
        int textPixelWidth = text.Length * 6 * pixelSize;
        int textPixelHeight = 8 * pixelSize;
        spriteBatch.Draw(_pixel, new Rectangle(x - pad, y - pad, textPixelWidth + pad * 2, textPixelHeight + pad * 2), Color.Black * 0.6f);
        int cx = x;
        for (int i = 0; i < text.Length; i++)
        {
            var glyph = OverlayFont.GetGlyph(text[i]);
            for (int row = 0; row < 8; row++)
            {
                byte rowBits = glyph[row];
                for (int col = 0; col < 5; col++)
                {
                    if ((rowBits & (0x80 >> col)) != 0)
                        spriteBatch.Draw(_pixel, new Rectangle(cx + col * pixelSize, y + row * pixelSize, pixelSize, pixelSize), Color.White);
                }
            }
            cx += 6 * pixelSize;
        }
    }

    public void Dispose() => _pixel?.Dispose();
}

internal static class OverlayFont
{
    private static readonly byte[][] Digits =
    {
        new byte[] { 0x70, 0x88, 0x98, 0xA8, 0xC8, 0x88, 0x70, 0x00 },
        new byte[] { 0x20, 0x60, 0x20, 0x20, 0x20, 0x20, 0x70, 0x00 },
        new byte[] { 0x70, 0x88, 0x08, 0x30, 0x40, 0x80, 0xF8, 0x00 },
        new byte[] { 0x70, 0x88, 0x08, 0x30, 0x08, 0x88, 0x70, 0x00 },
        new byte[] { 0x10, 0x30, 0x50, 0x90, 0xF8, 0x10, 0x10, 0x00 },
        new byte[] { 0xF8, 0x80, 0xF0, 0x08, 0x08, 0x88, 0x70, 0x00 },
        new byte[] { 0x70, 0x80, 0xF0, 0x88, 0x88, 0x88, 0x70, 0x00 },
        new byte[] { 0xF8, 0x08, 0x10, 0x20, 0x40, 0x40, 0x40, 0x00 },
        new byte[] { 0x70, 0x88, 0x88, 0x70, 0x88, 0x88, 0x70, 0x00 },
        new byte[] { 0x70, 0x88, 0x88, 0x78, 0x08, 0x88, 0x70, 0x00 },
    };

    private static readonly Dictionary<char, byte[]> Letters = new()
    {
        ['F'] = new byte[] { 0xF8, 0x80, 0x80, 0xF0, 0x80, 0x80, 0x80, 0x00 },
        ['P'] = new byte[] { 0xF0, 0x88, 0x88, 0xF0, 0x80, 0x80, 0x80, 0x00 },
        ['S'] = new byte[] { 0x78, 0x80, 0x80, 0x70, 0x08, 0x08, 0xF0, 0x00 },
        [' '] = new byte[8],
        ['.'] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x60, 0x60 },
    };

    public static byte[] GetGlyph(char ch)
    {
        ch = char.ToUpperInvariant(ch);
        if (ch >= '0' && ch <= '9') return Digits[ch - '0'];
        return Letters.TryGetValue(ch, out var g) ? g : new byte[8];
    }
}
