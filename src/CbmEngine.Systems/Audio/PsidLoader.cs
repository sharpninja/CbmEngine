using System.Text;

namespace CbmEngine.Systems.Audio;

public static class PsidLoader
{
    public static PsidProgram Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var br = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        var magicBytes = ReadOrThrow(br, 4, "magic");
        string magic = Encoding.ASCII.GetString(magicBytes);
        if (magic is not ("PSID" or "RSID"))
            throw new PsidFormatException($"Bad magic '{magic}'; expected 'PSID' or 'RSID'.",
                offset: 0, expectedMagic: "PSID|RSID", observedMagic: magic);

        ushort version = ReadBeUInt16(br, "version");
        ushort dataOffset = ReadBeUInt16(br, "dataOffset");
        ushort loadAddress = ReadBeUInt16(br, "loadAddress");
        ushort initAddress = ReadBeUInt16(br, "initAddress");
        ushort playAddress = ReadBeUInt16(br, "playAddress");
        ushort songCount = ReadBeUInt16(br, "songCount");
        ushort startSong = ReadBeUInt16(br, "startSong");
        uint speedFlags = ReadBeUInt32(br, "speedFlags");
        string name = ReadFixedString(br, 32, "name");
        string author = ReadFixedString(br, 32, "author");
        string released = ReadFixedString(br, 32, "released");

        long bytesReadSoFar = br.BaseStream.Position;
        long remainingHeader = dataOffset - bytesReadSoFar;
        if (remainingHeader > 0)
            br.BaseStream.Seek(remainingHeader, SeekOrigin.Current);

        var payloadList = new List<byte>();
        var buffer = new byte[4096];
        int n;
        while ((n = br.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
            payloadList.AddRange(new ArraySegment<byte>(buffer, 0, n));
        var payload = payloadList.ToArray();

        if (loadAddress == 0 && payload.Length >= 2)
        {
            loadAddress = (ushort)(payload[0] | (payload[1] << 8));
            payload = payload.AsSpan(2).ToArray();
        }
        if (initAddress == 0) initAddress = loadAddress;

        var header = new PsidHeader(magic, version, dataOffset, loadAddress, initAddress, playAddress,
            songCount, startSong == 0 ? (ushort)1 : startSong, speedFlags, name, author, released);
        return new PsidProgram(header, payload);
    }

    private static byte[] ReadOrThrow(BinaryReader br, int count, string field)
    {
        var bytes = br.ReadBytes(count);
        if (bytes.Length != count)
            throw new PsidFormatException($"Truncated stream while reading {field}.", offset: (int)br.BaseStream.Position);
        return bytes;
    }

    private static ushort ReadBeUInt16(BinaryReader br, string field)
    {
        var b = ReadOrThrow(br, 2, field);
        return (ushort)((b[0] << 8) | b[1]);
    }

    private static uint ReadBeUInt32(BinaryReader br, string field)
    {
        var b = ReadOrThrow(br, 4, field);
        return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
    }

    private static string ReadFixedString(BinaryReader br, int length, string field)
    {
        var bytes = ReadOrThrow(br, length, field);
        int len = Array.IndexOf<byte>(bytes, 0);
        if (len < 0) len = bytes.Length;
        return Encoding.ASCII.GetString(bytes, 0, len);
    }
}
