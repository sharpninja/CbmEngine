using CbmEngine.Abstractions;
using Microsoft.Xna.Framework.Graphics;

namespace CbmEngine.Host.MonoGame;

public sealed class MonoGameBlitTarget : IBlitTarget, IDisposable
{
    private readonly GraphicsDevice _device;
    private Texture2D? _texture;
    private byte[]? _rgbaBuffer;
    private int _w, _h;

    public Texture2D? Texture => _texture;

    public MonoGameBlitTarget(GraphicsDevice device)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
    }

    public void Upload(ReadOnlySpan<byte> bgra, int width, int height)
    {
        int bytes = width * height * 4;
        if (_texture is null || _w != width || _h != height)
        {
            _texture?.Dispose();
            _texture = new Texture2D(_device, width, height, false, SurfaceFormat.Color);
            _rgbaBuffer = new byte[bytes];
            _w = width;
            _h = height;
        }
        if (_rgbaBuffer is null || _rgbaBuffer.Length != bytes)
            _rgbaBuffer = new byte[bytes];

        for (int i = 0; i < bytes; i += 4)
        {
            _rgbaBuffer[i] = bgra[i + 2];
            _rgbaBuffer[i + 1] = bgra[i + 1];
            _rgbaBuffer[i + 2] = bgra[i];
            _rgbaBuffer[i + 3] = bgra[i + 3];
        }
        _texture.SetData(_rgbaBuffer);
    }

    public void Dispose() => _texture?.Dispose();
}
