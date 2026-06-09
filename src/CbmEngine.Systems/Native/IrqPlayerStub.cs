namespace CbmEngine.Systems.Native;

public static class IrqPlayerStub
{
    public const ushort InitOffset = 0x00;
    public const ushort HandlerOffset = 0x10;
    public const int Size = HandlerOffset + 11;

    public static byte[] Build(ushort stubBase, ushort playAddress, ushort kernalIrqExit = 0xEA31)
    {
        ushort handlerAddr = (ushort)(stubBase + HandlerOffset);
        byte handlerLo = (byte)(handlerAddr & 0xFF);
        byte handlerHi = (byte)((handlerAddr >> 8) & 0xFF);
        byte playLo = (byte)(playAddress & 0xFF);
        byte playHi = (byte)((playAddress >> 8) & 0xFF);
        byte kernalLo = (byte)(kernalIrqExit & 0xFF);
        byte kernalHi = (byte)((kernalIrqExit >> 8) & 0xFF);

        var bytes = new byte[Size];
        int i = 0;
        bytes[i++] = 0x78;                        // SEI
        bytes[i++] = 0xA9; bytes[i++] = handlerLo;    // LDA #<handler
        bytes[i++] = 0x8D; bytes[i++] = 0x14; bytes[i++] = 0x03;  // STA $0314
        bytes[i++] = 0xA9; bytes[i++] = handlerHi;    // LDA #>handler
        bytes[i++] = 0x8D; bytes[i++] = 0x15; bytes[i++] = 0x03;  // STA $0315
        bytes[i++] = 0x58;                        // CLI
        bytes[i++] = 0x60;                        // RTS
        while (i < HandlerOffset) bytes[i++] = 0xEA;  // NOP padding

        bytes[i++] = 0xA9; bytes[i++] = 0x01;             // LDA #$01
        bytes[i++] = 0x8D; bytes[i++] = 0x19; bytes[i++] = 0xD0;  // STA $D019 (ack VIC IRQ)
        bytes[i++] = 0x20; bytes[i++] = playLo; bytes[i++] = playHi;  // JSR play
        bytes[i++] = 0x4C; bytes[i++] = kernalLo; bytes[i++] = kernalHi;  // JMP kernal IRQ
        return bytes;
    }
}
