namespace CbmEngine.Systems.Native;

/// <summary>
/// A KERNAL-independent IRQ handler that drives a PSID PLAY routine each interrupt. Unlike
/// <see cref="IrqPlayerStub"/> (which redirects the KERNAL's $0314 vector and chains back into the
/// KERNAL IRQ at $EA31), this is a complete stand-alone handler: it saves and restores the registers
/// itself, acknowledges both interrupt sources (VIC $D019 and CIA1 $DC0D), calls PLAY, and returns
/// with RTI. It needs no ROM, so it works with the KERNAL banked out of $E000-$FFFF; install its
/// address into the now-RAM hardware IRQ vector at $FFFE/$FFFF.
/// </summary>
public static class StandaloneIrqStub
{
    /// <summary>Length in bytes of the handler produced by <see cref="BuildHandler"/>.</summary>
    public const int Size = 22;

    public static byte[] BuildHandler(ushort playAddress)
    {
        byte playLo = (byte)(playAddress & 0xFF);
        byte playHi = (byte)((playAddress >> 8) & 0xFF);
        return new byte[]
        {
            0x48,                         // PHA
            0x8A,                         // TXA
            0x48,                         // PHA
            0x98,                         // TYA
            0x48,                         // PHA
            0xA9, 0x01,                   // LDA #$01
            0x8D, 0x19, 0xD0,             // STA $D019   ; ack VIC interrupt latch
            0xAD, 0x0D, 0xDC,             // LDA $DC0D   ; ack CIA1 timer interrupt
            0x20, playLo, playHi,         // JSR play
            0x68,                         // PLA
            0xA8,                         // TAY
            0x68,                         // PLA
            0xAA,                         // TAX
            0x68,                         // PLA
            0x40,                         // RTI
        };
    }
}
