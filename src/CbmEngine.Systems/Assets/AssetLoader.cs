using CbmEngine.Pipeline;

namespace CbmEngine.Systems.Assets;

public static class AssetLoader
{
    public static CompiledCharset LoadCharset(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return CompiledCharset.Deserialize(File.ReadAllBytes(path));
    }

    public static CompiledTilemap LoadTilemap(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return CompiledTilemap.Deserialize(File.ReadAllBytes(path));
    }

    public static CompiledCharset DeserializeCharset(ReadOnlySpan<byte> bytes) => CompiledCharset.Deserialize(bytes);
    public static CompiledTilemap DeserializeTilemap(ReadOnlySpan<byte> bytes) => CompiledTilemap.Deserialize(bytes);
}
