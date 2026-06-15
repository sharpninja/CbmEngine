using CbmEngine.Abstractions;
using CbmEngine.Pipeline;
using CbmEngine.Systems.Cartridge;
using CbmEngine.Systems.Strategy;
using Microsoft.Xna.Framework.Graphics;
using ViceSharp.RomFetch;

namespace CbmEngine.Tools.CbmVidStudio;

internal sealed class StudioEmulator : IDisposable
{
    private readonly GraphicsDevice _gd;
    private readonly ICommodoreMachine _sys;
    private Texture2D? _texture;
    private byte[]? _bgraScratch;

    public Texture2D? PreviewTexture => _texture;
    public int FrameWidth { get; }
    public int FrameHeight { get; }

    public StudioEmulator(GraphicsDevice gd, RomProvider roms)
    {
        _gd = gd;
        _sys = CommodoreSystem.Build("c64", roms);

        var blank = BlankSplash();
        var cart = BitmapPlayerCart.Build(blank);
        var boot = CartridgeBoot.AttachAndWaitForMarker(_sys, cart, maxFrames: 600);
        if (!boot.MarkerSeen) throw new InvalidOperationException($"BitmapPlayerCart did not boot within 600 frames (saw at {boot.FramesUntilMarker}).");

        // VideoPlayer expects a stream; we don't actually use one - reuse PumpFrame indirectly via a per-frame helper.
        // For now we'll do the writes inline because the static splash deposit already configured VIC.
        FrameWidth = _sys.VideoChip.FrameWidth;
        FrameHeight = _sys.VideoChip.FrameHeight;
        _bgraScratch = new byte[FrameWidth * FrameHeight * 4];
        _texture = new Texture2D(_gd, FrameWidth, FrameHeight, false, SurfaceFormat.Color);
    }

    public void RenderFrame(EncodedSplashBitmap frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        // Mode-flip via WriteIo if needed.
        byte d016 = frame.Mode == SplashBitmapMode.HiRes ? (byte)0xC8 : (byte)0xD8;
        _sys.Memory.WriteIo(0xD016, new[] { d016 });
        // Background color.
        _sys.Memory.WriteIo(0xD021, new[] { frame.BackgroundColorIndex });
        // Bitmap, screen, color.
        _sys.Memory.WriteRange(0x6000, frame.Bitmap);
        _sys.Memory.WriteRange(0x4400, frame.ScreenRam);
        _sys.Memory.WriteIo(0xD800, frame.ColorRam);

        // Let VIC render two frames so any latched state catches up cleanly.
        _sys.RunFrame();
        _sys.RunFrame();

        // Copy BGRA -> RGBA into our scratch (MonoGame's Color is RGBA).
        var src = _sys.VideoChip.FrameBuffer;
        var dst = _bgraScratch!;
        for (int i = 0; i < dst.Length; i += 4)
        {
            dst[i] = src[i + 2];      // R
            dst[i + 1] = src[i + 1];  // G
            dst[i + 2] = src[i];      // B
            dst[i + 3] = src[i + 3];  // A
        }
        _texture!.SetData(_bgraScratch);
    }

    public void Dispose()
    {
        _texture?.Dispose();
    }

    private static EncodedSplashBitmap BlankSplash()
    {
        var bitmap = new byte[EncodedSplashBitmap.BitmapByteSize];
        var screen = new byte[EncodedSplashBitmap.ScreenRamSize];
        var color = new byte[EncodedSplashBitmap.ColorRamSize];
        Array.Fill(screen, (byte)0x10);
        Array.Fill(color, (byte)0x01);
        return new EncodedSplashBitmap(SplashBitmapMode.Multicolor, 0, bitmap, screen, color);
    }
}
