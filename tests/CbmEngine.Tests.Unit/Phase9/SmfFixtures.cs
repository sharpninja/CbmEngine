using System.Buffers.Binary;

namespace CbmEngine.Tests.Unit.Phase9;

internal static class SmfFixtures
{
    public static byte[] BuildType0(int ticksPerQuarter, params (long Tick, byte Status, byte Data1, byte Data2)[] events)
    {
        var trackBytes = BuildTrackBytes(events);
        return BuildFile(format: 0, trackCount: 1, ticksPerQuarter, new[] { trackBytes });
    }

    public static byte[] BuildType1(int ticksPerQuarter, params (long Tick, byte Status, byte Data1, byte Data2)[][] tracks)
    {
        var trackList = new List<byte[]>(tracks.Length);
        foreach (var t in tracks) trackList.Add(BuildTrackBytes(t));
        return BuildFile(format: 1, trackCount: (short)tracks.Length, ticksPerQuarter, trackList);
    }

    public static byte[] BuildType0WithTempo(int ticksPerQuarter, IEnumerable<MidiEventBytes> events)
    {
        var ms = new MemoryStream();
        long prevTick = 0;
        foreach (var e in events)
        {
            WriteVarLen(ms, (uint)(e.Tick - prevTick));
            prevTick = e.Tick;
            ms.Write(e.Bytes);
        }
        var raw = ms.ToArray();
        var withEnd = AppendEndOfTrack(raw);
        return BuildFile(format: 0, trackCount: 1, ticksPerQuarter, new[] { withEnd });
    }

    public sealed record MidiEventBytes(long Tick, byte[] Bytes);

    public static MidiEventBytes NoteOn(long tick, int channel, byte note, byte velocity)
        => new(tick, new byte[] { (byte)(0x90 | (channel & 0x0F)), note, velocity });

    public static MidiEventBytes NoteOff(long tick, int channel, byte note)
        => new(tick, new byte[] { (byte)(0x80 | (channel & 0x0F)), note, 0 });

    public static MidiEventBytes Tempo(long tick, int microsecondsPerQuarter)
    {
        byte b1 = (byte)((microsecondsPerQuarter >> 16) & 0xFF);
        byte b2 = (byte)((microsecondsPerQuarter >> 8) & 0xFF);
        byte b3 = (byte)(microsecondsPerQuarter & 0xFF);
        return new(tick, new byte[] { 0xFF, 0x51, 0x03, b1, b2, b3 });
    }

    public static MidiEventBytes ProgramChange(long tick, int channel, byte program)
        => new(tick, new byte[] { (byte)(0xC0 | (channel & 0x0F)), program });

    private static byte[] BuildTrackBytes((long Tick, byte Status, byte Data1, byte Data2)[] events)
    {
        var ms = new MemoryStream();
        long prevTick = 0;
        foreach (var (tick, status, d1, d2) in events)
        {
            WriteVarLen(ms, (uint)(tick - prevTick));
            prevTick = tick;
            ms.WriteByte(status);
            ms.WriteByte(d1);
            // Two-data-byte messages: NoteOn (0x9_), NoteOff (0x8_), CC (0xB_), PolyAT (0xA_), PitchBend (0xE_).
            int high = status & 0xF0;
            if (high == 0x80 || high == 0x90 || high == 0xA0 || high == 0xB0 || high == 0xE0)
                ms.WriteByte(d2);
        }
        return AppendEndOfTrack(ms.ToArray());
    }

    private static byte[] AppendEndOfTrack(byte[] payload)
    {
        var ms = new MemoryStream();
        ms.Write(payload);
        WriteVarLen(ms, 0);
        ms.Write(new byte[] { 0xFF, 0x2F, 0x00 });
        return ms.ToArray();
    }

    private static byte[] BuildFile(int format, int trackCount, int ticksPerQuarter, IList<byte[]> tracks)
    {
        var ms = new MemoryStream();
        ms.Write("MThd"u8);
        WriteUInt32BE(ms, 6);
        WriteUInt16BE(ms, (ushort)format);
        WriteUInt16BE(ms, (ushort)trackCount);
        WriteUInt16BE(ms, (ushort)ticksPerQuarter);
        foreach (var t in tracks)
        {
            ms.Write("MTrk"u8);
            WriteUInt32BE(ms, (uint)t.Length);
            ms.Write(t);
        }
        return ms.ToArray();
    }

    private static void WriteUInt32BE(MemoryStream ms, uint value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, value);
        ms.Write(buf);
    }

    private static void WriteUInt16BE(MemoryStream ms, ushort value)
    {
        Span<byte> buf = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buf, value);
        ms.Write(buf);
    }

    private static void WriteVarLen(MemoryStream ms, uint value)
    {
        Span<byte> buf = stackalloc byte[4];
        int count = 0;
        buf[count++] = (byte)(value & 0x7F);
        value >>= 7;
        while (value > 0)
        {
            buf[count++] = (byte)((value & 0x7F) | 0x80);
            value >>= 7;
        }
        for (int i = count - 1; i >= 0; i--) ms.WriteByte(buf[i]);
    }
}
