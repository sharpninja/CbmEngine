using System.Buffers.Binary;
using System.Text;

namespace CbmEngine.Systems.Cartridge;

public static class CrtFile
{
    private static readonly byte[] Signature = Encoding.ASCII.GetBytes("C64 CARTRIDGE   ");
    private const int CrtHeaderSize = 0x40;
    private const int ChipHeaderSize = 0x10;

    public static byte[] WrapStandard16K(ReadOnlySpan<byte> rom16K, string cartridgeName = "CBMENGINE CART")
    {
        if (rom16K.Length != 0x4000) throw new ArgumentException("Standard 16K cart ROM must be 16384 bytes.", nameof(rom16K));

        var total = CrtHeaderSize + ChipHeaderSize + rom16K.Length;
        var crt = new byte[total];

        Signature.CopyTo(crt.AsSpan(0));
        BinaryPrimitives.WriteUInt32BigEndian(crt.AsSpan(0x10, 4), CrtHeaderSize);
        BinaryPrimitives.WriteUInt16BigEndian(crt.AsSpan(0x14, 2), 0x0100);
        BinaryPrimitives.WriteUInt16BigEndian(crt.AsSpan(0x16, 2), 0x0000);
        crt[0x18] = 0;
        crt[0x19] = 0;
        crt[0x1A] = 0;
        crt[0x1B] = 0;

        var nameBytes = Encoding.ASCII.GetBytes(cartridgeName);
        int nameLen = Math.Min(32, nameBytes.Length);
        Array.Copy(nameBytes, 0, crt, 0x20, nameLen);

        var chip = crt.AsSpan(CrtHeaderSize);
        Encoding.ASCII.GetBytes("CHIP").CopyTo(chip);
        BinaryPrimitives.WriteUInt32BigEndian(chip.Slice(0x04, 4), (uint)(ChipHeaderSize + rom16K.Length));
        BinaryPrimitives.WriteUInt16BigEndian(chip.Slice(0x08, 2), 0x0000);
        BinaryPrimitives.WriteUInt16BigEndian(chip.Slice(0x0A, 2), 0x0000);
        BinaryPrimitives.WriteUInt16BigEndian(chip.Slice(0x0C, 2), 0x8000);
        BinaryPrimitives.WriteUInt16BigEndian(chip.Slice(0x0E, 2), (ushort)rom16K.Length);
        rom16K.CopyTo(chip.Slice(ChipHeaderSize));

        return crt;
    }
}
