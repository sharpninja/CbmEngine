using CbmEngine.Abstractions;
using CbmEngine.Systems.Video;

namespace CbmEngine.Systems.Layout;

public enum ScreenRegionKind { CharBand, BitmapBand }

/// <summary>
/// The resolved placement of one screen region: its raster span, screen/colour RAM, and (depending on
/// kind) the bitmap base or charset base it renders from.
/// </summary>
public sealed record RegionPlacement(
    ScreenRegionKind Kind,
    int FirstRow,
    int RowCount,
    int StartRasterLine,
    ushort ScreenBase,
    ushort ColorBase,
    ushort? BitmapBase,
    ushort? CharsetBase,
    bool Multicolor);

/// <summary>
/// A compiled mixed-mode screen: the per-region placements and a steady-state <see cref="LineProgram"/>
/// that flips VIC mode ($D011/$D016/$D018) at each band's start raster line.
/// </summary>
public sealed record CompiledScreenLayout(
    LineProgram SteadyState,
    IReadOnlyList<RegionPlacement> Regions,
    int Bank);

/// <summary>
/// Declares an ordered set of char/bitmap bands and compiles them into allocated memory addresses plus
/// a raster-split <see cref="LineProgram"/>, so a game declares "text bar, bitmap area, text bar" and
/// gets a ready raster program without hand-rolling band raster lines and per-band register math.
/// </summary>
public static class ScreenLayout
{
    private const int Rows = 25;
    private const int DefaultDisplayTopRasterLine = 51;
    private const int BankSize = 0x4000;
    private const int ScreenSize = 0x400;     // 1KB video matrix
    private const int CharsetSize = 0x800;    // 2KB charset
    private const int BitmapSize = 0x2000;    // 8KB bitmap
    private const ushort ColorRam = 0xD800;

    // Char-mode $D011 (DEN + RSEL + YSCROLL=3) and standard hi-res $D016 (40 cols).
    private const byte D011Text = 0x1B;
    private const byte D011Bitmap = 0x3B;
    private const byte D016HiRes = 0xC8;
    private const byte D016Multicolor = 0xD8;

    // Screen-RAM slot offsets within the bank that avoid the charset ($1800) and bitmap ($2000) regions.
    private static readonly int[] ScreenSlotOffsets = { 0x0000, 0x0400, 0x0800, 0x0C00, 0x1000, 0x1400 };
    private const int CharsetOffset = 0x1800;
    private const int BitmapOffset = 0x2000;

    public sealed class Builder
    {
        private readonly List<(ScreenRegionKind kind, int rows, bool multicolor)> _bands = new();

        public Builder AddCharBand(int rows)
        {
            if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows), rows, "Band rows must be positive.");
            _bands.Add((ScreenRegionKind.CharBand, rows, false));
            return this;
        }

        public Builder AddBitmapBand(int rows, bool multicolor)
        {
            if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows), rows, "Band rows must be positive.");
            _bands.Add((ScreenRegionKind.BitmapBand, rows, multicolor));
            return this;
        }

        public CompiledScreenLayout Build(int bank = 1, int displayTopRasterLine = DefaultDisplayTopRasterLine)
        {
            if (bank is < 0 or > 3) throw new ArgumentException("VIC bank must be 0-3.", nameof(bank));
            if (_bands.Count == 0) throw new ArgumentException("A layout must contain at least one band.", nameof(_bands));

            int totalRows = _bands.Sum(b => b.rows);
            if (totalRows != Rows)
                throw new ArgumentException($"Band rows must sum to {Rows}; got {totalRows}.", nameof(_bands));

            if (_bands.Count > ScreenSlotOffsets.Length)
                throw new ArgumentException(
                    $"Too many bands: {_bands.Count} regions need {_bands.Count} screen slots but only {ScreenSlotOffsets.Length} are available in a VIC bank.",
                    nameof(_bands));

            int bankBase = bank * BankSize;
            bool hasBitmap = _bands.Any(b => b.kind == ScreenRegionKind.BitmapBand);
            bool hasChar = _bands.Any(b => b.kind == ScreenRegionKind.CharBand);
            ushort? bitmapBase = hasBitmap ? (ushort)(bankBase + BitmapOffset) : null;
            ushort? charsetBase = hasChar ? (ushort)(bankBase + CharsetOffset) : null;

            var regions = new List<RegionPlacement>(_bands.Count);
            var program = new LineProgram.Builder();

            int firstRow = 0;
            for (int i = 0; i < _bands.Count; i++)
            {
                var band = _bands[i];
                int screenOffset = ScreenSlotOffsets[i];
                var screenBase = (ushort)(bankBase + screenOffset);
                int screenNibble = screenOffset / ScreenSize;
                int startRaster = displayTopRasterLine + firstRow * 8;

                byte d011, d016, d018;
                if (band.kind == ScreenRegionKind.BitmapBand)
                {
                    d011 = D011Bitmap;
                    d016 = band.multicolor ? D016Multicolor : D016HiRes;
                    int bitmapBit = BitmapOffset / BitmapSize;                 // 1
                    d018 = (byte)((screenNibble << 4) | (bitmapBit << 3));
                }
                else
                {
                    d011 = D011Text;
                    d016 = D016HiRes;
                    int charBits = CharsetOffset / CharsetSize;                // 3
                    d018 = (byte)((screenNibble << 4) | (charBits << 1));
                }

                program.At(startRaster, Vic.D011, d011)
                       .At(startRaster, Vic.D016, d016)
                       .At(startRaster, Vic.D018, d018);

                regions.Add(new RegionPlacement(
                    band.kind, firstRow, band.rows, startRaster, screenBase, ColorRam,
                    band.kind == ScreenRegionKind.BitmapBand ? bitmapBase : null,
                    band.kind == ScreenRegionKind.CharBand ? charsetBase : null,
                    band.multicolor));

                firstRow += band.rows;
            }

            return new CompiledScreenLayout(program.Build(), regions, bank);
        }
    }
}
