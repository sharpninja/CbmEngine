using CbmEngine.Abstractions;

namespace CbmEngine.Systems.Video;

/// <summary>
/// The VIC-II register values that describe a bitmap display configuration:
/// <c>$D011</c> (control 1, with the bitmap-mode bit), <c>$D016</c> (control 2, with the
/// multicolor bit), <c>$D018</c> (memory pointers for screen + bitmap base) and the CIA2
/// <c>$DD00</c> low two bits selecting the VIC bank.
/// </summary>
public readonly record struct VicBitmapRegisters(byte D011, byte D016, byte D018, byte BankBits);

/// <summary>
/// Typed helpers for VIC-II bitmap-mode register math so callers express intent
/// (bank, bitmap base, screen base, multicolor) instead of hand-rolling register bits.
/// </summary>
public static class Vic
{
    public const ushort D011 = 0xD011; // control register 1 (BMM in bit 5)
    public const ushort D016 = 0xD016; // control register 2 (MCM in bit 4)
    public const ushort D018 = 0xD018; // memory pointers (video matrix + char/bitmap base)
    public const ushort D020 = 0xD020; // border colour
    public const ushort D021 = 0xD021; // background colour 0
    public const ushort DD00 = 0xDD00; // CIA2 port A (VIC bank select in bits 0-1)

    // $D011 = DEN + RSEL(25 rows) + YSCROLL=3 ($1B) with BMM (bitmap) bit set ($20) => $3B.
    private const byte D011Bitmap = 0x3B;
    private const byte D016HiResValue = 0xC8;       // 40 cols, MCM clear
    private const byte D016MulticolorValue = 0xD8;  // 40 cols, MCM set

    private const int BankSize = 0x4000;
    private const int ScreenStep = 0x400;
    private const int BitmapStep = 0x2000;

    /// <summary>CIA2 <c>$DD00</c> low two bits for a VIC bank (0-3). The bits are inverted: bank 0 => %11.</summary>
    public static byte BankBits(int bank)
    {
        if (bank is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(bank), bank, "VIC bank must be 0-3.");
        return (byte)((3 - bank) & 0x03);
    }

    /// <summary>Register values for multicolor bitmap mode (160x200 effective).</summary>
    public static VicBitmapRegisters MulticolorBitmap(int bank, ushort bitmapBase, ushort screenBase) =>
        Build(bank, bitmapBase, screenBase, multicolor: true);

    /// <summary>Register values for hi-res bitmap mode (320x200).</summary>
    public static VicBitmapRegisters HiResBitmap(int bank, ushort bitmapBase, ushort screenBase) =>
        Build(bank, bitmapBase, screenBase, multicolor: false);

    private static VicBitmapRegisters Build(int bank, ushort bitmapBase, ushort screenBase, bool multicolor)
    {
        if (bank is < 0 or > 3)
            throw new ArgumentException("VIC bank must be 0-3.", nameof(bank));

        int bankBase = bank * BankSize;

        int bitmapOffset = bitmapBase - bankBase;
        if (bitmapOffset != 0 && bitmapOffset != BitmapStep)
            throw new ArgumentException(
                $"bitmapBase must be at the bank base or bank base + $2000 (bank {bank} base ${bankBase:X4}); got ${bitmapBase:X4}.",
                nameof(bitmapBase));

        int screenOffset = screenBase - bankBase;
        if (screenOffset < 0 || screenOffset >= BankSize || (screenOffset % ScreenStep) != 0)
            throw new ArgumentException(
                $"screenBase must be within the VIC bank on a $400 boundary (bank {bank} base ${bankBase:X4}); got ${screenBase:X4}.",
                nameof(screenBase));

        byte d018 = (byte)(((screenOffset / ScreenStep) << 4) | ((bitmapOffset / BitmapStep) << 3));
        byte d016 = multicolor ? D016MulticolorValue : D016HiResValue;
        return new VicBitmapRegisters(D011Bitmap, d016, d018, BankBits(bank));
    }

    /// <summary>Assert multicolor bitmap registers ($D011/$D016/$D018) and the VIC bank ($DD00) into memory.</summary>
    public static void SetMulticolorBitmap(IMemoryService memory, int bank, ushort bitmapBase, ushort screenBase) =>
        Apply(memory, MulticolorBitmap(bank, bitmapBase, screenBase));

    /// <summary>Assert hi-res bitmap registers ($D011/$D016/$D018) and the VIC bank ($DD00) into memory.</summary>
    public static void SetHiResBitmap(IMemoryService memory, int bank, ushort bitmapBase, ushort screenBase) =>
        Apply(memory, HiResBitmap(bank, bitmapBase, screenBase));

    private static void Apply(IMemoryService memory, VicBitmapRegisters r)
    {
        ArgumentNullException.ThrowIfNull(memory);
        WriteIo(memory, D011, r.D011);
        WriteIo(memory, D016, r.D016);
        WriteIo(memory, D018, r.D018);
        // Preserve the upper $DD00 bits (serial / RS232); only the low two select the VIC bank.
        byte dd00 = (byte)((memory.ReadIo(DD00) & 0xFC) | (r.BankBits & 0x03));
        WriteIo(memory, DD00, dd00);
    }

    /// <summary>Write the border colour ($D020), masked to the low nibble.</summary>
    public static void SetBorder(IMemoryService memory, byte colorIndex)
    {
        ArgumentNullException.ThrowIfNull(memory);
        WriteIo(memory, D020, (byte)(colorIndex & 0x0F));
    }

    /// <summary>Write background colour 0 ($D021), masked to the low nibble.</summary>
    public static void SetBackground(IMemoryService memory, byte colorIndex)
    {
        ArgumentNullException.ThrowIfNull(memory);
        WriteIo(memory, D021, (byte)(colorIndex & 0x0F));
    }

    internal static void WriteIo(IMemoryService memory, ushort address, byte value)
    {
        Span<byte> b = stackalloc byte[1];
        b[0] = value;
        memory.WriteIo(address, b);
    }
}
