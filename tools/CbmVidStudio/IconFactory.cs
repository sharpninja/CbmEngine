using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CbmEngine.Tools.CbmVidStudio;

/// <summary>
/// Generates small pixel-art toolbar icons at runtime so the tool needs no content pipeline or
/// image assets. Each icon is authored as a 16x16 character grid; Desktop.Scale handles DPI.
/// Palette: '.'=transparent, 'Y'=amber, 'D'=dark outline, 'W'=white, 'B'=blue, 'G'=green,
/// 'R'=red, 'K'=near-black.
/// </summary>
public sealed class IconFactory : IDisposable
{
    private readonly GraphicsDevice _gd;
    private readonly List<Texture2D> _owned = new();

    public Texture2D OpenFolder { get; }
    public Texture2D OpenVideo { get; }
    public Texture2D Save { get; }
    public Texture2D ExportGif { get; }
    public Texture2D Reencode { get; }

    private readonly int _pixelScale;

    public IconFactory(GraphicsDevice gd, int pixelScale = 1)
    {
        _gd = gd;
        _pixelScale = Math.Max(1, pixelScale);
        OpenFolder = Build(FolderPattern);
        OpenVideo = Build(FilmPattern);
        Save = Build(DiskPattern);
        ExportGif = Build(GifPattern);
        Reencode = Build(RedoPattern);
    }

    private static readonly string[] FolderPattern =
    {
        "................",
        "................",
        ".DDDDD..........",
        ".DYYYYD.........",
        ".DYYYYYDDDDDDDD.",
        ".DYYYYYYYYYYYYD.",
        ".DYYYYYYYYYYYYD.",
        ".DDDDDDDDDDDDDD.",
        ".DYYYYYYYYYYYYD.",
        ".DYYYYYYYYYYYYD.",
        ".DYYYYYYYYYYYYD.",
        ".DYYYYYYYYYYYYD.",
        ".DYYYYYYYYYYYYD.",
        ".DDDDDDDDDDDDDD.",
        "................",
        "................",
    };

    private static readonly string[] FilmPattern =
    {
        "................",
        ".KKKKKKKKKKKKKK.",
        ".KWKKKKKKKKKKWK.",
        ".KKKBBBBBBBBKKK.",
        ".KWKBBBBBBBBKWK.",
        ".KKKBBBBBBBBKKK.",
        ".KWKBBBBBBBBKWK.",
        ".KKKKKKKKKKKKKK.",
        ".KWKKKKKKKKKKWK.",
        ".KKKGGGGGGGGKKK.",
        ".KWKGGGGGGGGKWK.",
        ".KKKGGGGGGGGKKK.",
        ".KWKGGGGGGGGKWK.",
        ".KKKKKKKKKKKKKK.",
        "................",
        "................",
    };

    private static readonly string[] DiskPattern =
    {
        "................",
        ".BBBBBBBBBBBBB..",
        ".BBWWWWWWWWWBB..",
        ".BBWBBBBBBBWBB..",
        ".BBWWWWWWWWWBB..",
        ".BBBBBBBBBBBBBB.",
        ".BBBBBBBBBBBBBB.",
        ".BBBWWWWWWWWBBB.",
        ".BBBWWWWWWWWBBB.",
        ".BBBWWWWWWWWBBB.",
        ".BBBWWWWWWWWBBB.",
        ".BBBWWWWWWWWBBB.",
        ".BBBWWWWWWWWBBB.",
        ".BBBBBBBBBBBBBB.",
        "................",
        "................",
    };

    private static readonly string[] GifPattern =
    {
        "................",
        ".GGGGGGGGGGGGG..",
        ".G...........G..",
        ".G.WW.WWW.WWW.G.",
        ".G.W..W.W.W...G.",
        ".G.W.WW.W.WWW.G.",
        ".G.W..W.W.W...G.",
        ".G.WW.WWW.W...G.",
        ".G............G.",
        ".GGGGGGGGGGGGGG.",
        "......GGG.......",
        ".....GGGGG......",
        "......GGG.......",
        ".......G........",
        "................",
        "................",
    };

    private static readonly string[] RedoPattern =
    {
        "................",
        "................",
        "....WWWWWWW.....",
        "...W.......W....",
        "..W.........W...",
        ".W...........W..",
        ".W...........W..",
        ".W.........WWWWW",
        ".W..........WWW.",
        ".W...........W..",
        ".W..............",
        "..W.............",
        "...W.........W..",
        "....WWWWWWWWW...",
        "................",
        "................",
    };

    private Texture2D Build(string[] pattern)
    {
        const int baseSize = 16;
        int size = baseSize * _pixelScale;
        var pixels = new Color[size * size];
        for (int py = 0; py < baseSize; py++)
        {
            string row = py < pattern.Length ? pattern[py] : "................";
            for (int px = 0; px < baseSize; px++)
            {
                char c = px < row.Length ? row[px] : '.';
                Color color = c switch
                {
                    'Y' => new Color(0xE0, 0xA8, 0x30),
                    'D' => new Color(0x7A, 0x55, 0x10),
                    'W' => Color.White,
                    'B' => new Color(0x4F, 0x8F, 0xE8),
                    'G' => new Color(0x4F, 0xC0, 0x60),
                    'R' => new Color(0xD0, 0x50, 0x50),
                    'K' => new Color(0x20, 0x20, 0x24),
                    _ => Color.Transparent,
                };
                // Nearest-neighbor expand: each logical pixel becomes a pixelScale^2 block.
                for (int dy = 0; dy < _pixelScale; dy++)
                    for (int dx = 0; dx < _pixelScale; dx++)
                        pixels[(py * _pixelScale + dy) * size + px * _pixelScale + dx] = color;
            }
        }
        var tex = new Texture2D(_gd, size, size, false, SurfaceFormat.Color);
        tex.SetData(pixels);
        _owned.Add(tex);
        return tex;
    }

    public void Dispose()
    {
        foreach (var t in _owned) t.Dispose();
        _owned.Clear();
    }
}
