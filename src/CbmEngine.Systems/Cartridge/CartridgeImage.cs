namespace CbmEngine.Systems.Cartridge;

public static class CartridgeImage
{
    public const int Size16K = 0x4000;
    public const ushort RomBase = 0x8000;
    public const ushort CodeStart = 0x8009;
    public static readonly byte[] Cbm80Signature = { 0xC3, 0xC2, 0xCD, 0x38, 0x30 };

    public static byte[] Build16K(ReadOnlySpan<byte> code, ushort coldStartAddress = CodeStart, ushort? warmStartAddress = null)
    {
        if (coldStartAddress < CodeStart || coldStartAddress >= RomBase + Size16K)
            throw new ArgumentOutOfRangeException(nameof(coldStartAddress), $"Cold start must be in [${CodeStart:X4}, ${RomBase + Size16K - 1:X4}].");
        int maxCodeLen = Size16K - (CodeStart - RomBase);
        if (code.Length > maxCodeLen)
            throw new ArgumentException($"Cart code {code.Length} bytes exceeds available {maxCodeLen} bytes.", nameof(code));

        ushort warm = warmStartAddress ?? coldStartAddress;
        var image = new byte[Size16K];
        image[0] = (byte)(coldStartAddress & 0xFF);
        image[1] = (byte)((coldStartAddress >> 8) & 0xFF);
        image[2] = (byte)(warm & 0xFF);
        image[3] = (byte)((warm >> 8) & 0xFF);
        for (int i = 0; i < Cbm80Signature.Length; i++) image[4 + i] = Cbm80Signature[i];
        code.CopyTo(image.AsSpan(CodeStart - RomBase));
        return image;
    }
}
