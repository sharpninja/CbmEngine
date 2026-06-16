using CbmEngine.Abstractions;

namespace CbmEngine.Systems.Video;

/// <summary>
/// Result of <see cref="MachineBitmapModeExtensions.EnterBitmapMode"/>: the resolved register values,
/// the bitmap/screen base addresses, and a steady-state <see cref="LineProgram"/> that re-asserts the
/// VIC registers each frame so BASIC/KERNAL cannot win.
/// </summary>
public sealed record BitmapModeProgram(
    LineProgram SteadyState,
    ushort BitmapBase,
    ushort ScreenBase,
    VicBitmapRegisters Registers);

/// <summary>
/// Host-driven bitmap-mode entry that asserts the VIC registers from C# (no CC65, no cartridge).
/// </summary>
public static class MachineBitmapModeExtensions
{
    /// <summary>
    /// Switch <paramref name="machine"/> into bitmap mode now and return a steady-state program the
    /// host runs each frame (via <c>LineDirector</c>) to re-assert $D011/$D016/$D018 and the VIC bank.
    /// </summary>
    public static BitmapModeProgram EnterBitmapMode(
        this ICommodoreMachine machine,
        int bank,
        ushort bitmapBase,
        ushort screenBase,
        bool multicolor,
        int assertRasterLine = 0)
    {
        ArgumentNullException.ThrowIfNull(machine);

        var regs = multicolor
            ? Vic.MulticolorBitmap(bank, bitmapBase, screenBase)
            : Vic.HiResBitmap(bank, bitmapBase, screenBase);

        // Bake the full $DD00 byte (preserving the upper bits currently set) into the steady-state program.
        byte dd00 = (byte)((machine.Memory.ReadIo(Vic.DD00) & 0xFC) | (regs.BankBits & 0x03));

        // Immediate one-time assert.
        if (multicolor) Vic.SetMulticolorBitmap(machine.Memory, bank, bitmapBase, screenBase);
        else Vic.SetHiResBitmap(machine.Memory, bank, bitmapBase, screenBase);

        var program = new LineProgram.Builder()
            .At(assertRasterLine, Vic.D011, regs.D011)
            .At(assertRasterLine, Vic.D016, regs.D016)
            .At(assertRasterLine, Vic.D018, regs.D018)
            .At(assertRasterLine, Vic.DD00, dd00)
            .Build();

        return new BitmapModeProgram(program, bitmapBase, screenBase, regs);
    }
}
