namespace CbmEngine.Tests.Shared.Helpers;

public static class PsidFixtures
{
    public static byte[] BuildSyntheticPsid(
        string magic = "PSID",
        ushort loadAddress = 0x1000,
        ushort initOffset = 0,
        ushort playOffset = 3,
        ushort songCount = 1,
        ushort startSong = 1,
        IReadOnlyList<byte>? payloadCode = null)
    {
        payloadCode ??= new byte[]
        {
            0xA9, 0x00, 0x60,
            0xA9, 0x0F, 0x8D, 0x18, 0xD4, 0x60,
        };

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        foreach (var ch in magic) w.Write((byte)ch);
        WriteBe16(w, 2);
        WriteBe16(w, 0x7C);
        WriteBe16(w, loadAddress);
        WriteBe16(w, (ushort)(loadAddress + initOffset));
        WriteBe16(w, (ushort)(loadAddress + playOffset));
        WriteBe16(w, songCount);
        WriteBe16(w, startSong);
        WriteBe32(w, 0);
        WriteFixed(w, "Test", 32);
        WriteFixed(w, "Author", 32);
        WriteFixed(w, "2026", 32);
        WriteBe16(w, 0);
        w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); w.Write((byte)0);
        foreach (var b in payloadCode) w.Write(b);
        return ms.ToArray();
    }

    public static byte[] BuildInfiniteInitPsid()
    {
        var infinite = new byte[] { 0x4C, 0x00, 0x10 };
        return BuildSyntheticPsid(payloadCode: infinite);
    }

    public static byte[] BuildZeroPageOverlapPsid()
    {
        return BuildSyntheticPsid(loadAddress: 0x00C0, payloadCode: new byte[] { 0x60 });
    }

    private static void WriteBe16(BinaryWriter w, ushort v)
    {
        w.Write((byte)((v >> 8) & 0xFF));
        w.Write((byte)(v & 0xFF));
    }

    private static void WriteBe32(BinaryWriter w, uint v)
    {
        w.Write((byte)((v >> 24) & 0xFF));
        w.Write((byte)((v >> 16) & 0xFF));
        w.Write((byte)((v >> 8) & 0xFF));
        w.Write((byte)(v & 0xFF));
    }

    private static void WriteFixed(BinaryWriter w, string s, int length)
    {
        for (int i = 0; i < length; i++) w.Write(i < s.Length ? (byte)s[i] : (byte)0);
    }
}
