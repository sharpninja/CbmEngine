namespace CbmEngine.Systems.Cartridge;

public static class BootstrapCart
{
    public const ushort MarkerAddress = 0x0334;
    public const byte MarkerHi = 0xCB;
    public const byte MarkerLo = 0x42;

    public static byte[] BuildMarkerOnly16K(byte borderColor = 0x00, byte backgroundColor = 0x06, byte markerHi = MarkerHi, byte markerLo = MarkerLo)
    {
        byte mAddrLo = (byte)(MarkerAddress & 0xFF);
        byte mAddrHi = (byte)((MarkerAddress >> 8) & 0xFF);
        ushort loopAddr = (ushort)(CartridgeImage.CodeStart + 0x20);

        var code = new byte[]
        {
            0x78,                       // SEI                ; disable IRQ (no kernel IRQ vector yet)
            0xA2, 0xFF,                 // LDX #$FF
            0x9A,                       // TXS                ; reset stack
            0xD8,                       // CLD                ; clear decimal
            0xA9, 0x37,                 // LDA #$37
            0x85, 0x01,                 // STA $01            ; CPU port: cart + KERNAL + IO visible
            0xA9, borderColor,          // LDA #borderColor
            0x8D, 0x20, 0xD0,           // STA $D020          ; border
            0xA9, backgroundColor,      // LDA #backgroundColor
            0x8D, 0x21, 0xD0,           // STA $D021          ; background
            0xA9, markerHi,             // LDA #markerHi
            0x8D, mAddrLo, mAddrHi,     // STA $0334
            0xA9, markerLo,             // LDA #markerLo
            0x8D, (byte)(mAddrLo + 1), mAddrHi,  // STA $0335
            0xEA, 0xEA,                 // NOP NOP padding so loop sits at fixed offset
            0x4C, (byte)(loopAddr & 0xFF), (byte)((loopAddr >> 8) & 0xFF),  // JMP loopAddr  (self)
        };

        return CartridgeImage.Build16K(code, coldStartAddress: CartridgeImage.CodeStart);
    }
}
