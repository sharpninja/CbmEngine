using System.Buffers.Binary;

namespace CbmEngine.Pipeline.Midi;

public static class SmfReader
{
    public static SmfFile Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var headerBytes = ReadExact(stream, 14, what: "MThd header");
        if (headerBytes[0] != 'M' || headerBytes[1] != 'T' || headerBytes[2] != 'h' || headerBytes[3] != 'd')
            throw new InvalidDataException("Not a SMF: missing MThd magic at offset 0.");
        uint headerLen = BinaryPrimitives.ReadUInt32BigEndian(headerBytes.AsSpan(4, 4));
        if (headerLen != 6) throw new InvalidDataException($"Unexpected MThd length {headerLen}; expected 6.");
        int format = BinaryPrimitives.ReadUInt16BigEndian(headerBytes.AsSpan(8, 2));
        int trackCount = BinaryPrimitives.ReadUInt16BigEndian(headerBytes.AsSpan(10, 2));
        int division = BinaryPrimitives.ReadUInt16BigEndian(headerBytes.AsSpan(12, 2));
        if ((division & 0x8000) != 0) throw new NotSupportedException("SMPTE timing not supported; ticks-per-quarter only.");
        int ticksPerQuarter = division & 0x7FFF;

        var tracks = new List<IReadOnlyList<MidiEvent>>(trackCount);
        for (int t = 0; t < trackCount; t++)
        {
            var trkHeader = ReadExact(stream, 8, what: $"MTrk header (track {t})");
            if (trkHeader[0] != 'M' || trkHeader[1] != 'T' || trkHeader[2] != 'r' || trkHeader[3] != 'k')
                throw new InvalidDataException($"Track {t}: missing MTrk magic.");
            uint trkLen = BinaryPrimitives.ReadUInt32BigEndian(trkHeader.AsSpan(4, 4));
            var body = ReadExact(stream, (int)trkLen, what: $"MTrk body (track {t}, expected {trkLen} bytes)");
            tracks.Add(ParseTrack(body, t));
        }

        return new SmfFile(format, trackCount, ticksPerQuarter, tracks);
    }

    private static IReadOnlyList<MidiEvent> ParseTrack(byte[] body, int trackIndex)
    {
        var events = new List<MidiEvent>(64);
        int pos = 0;
        long tick = 0;
        byte runningStatus = 0;

        while (pos < body.Length)
        {
            int delta = ReadVarLen(body, ref pos, trackIndex);
            tick += delta;

            if (pos >= body.Length) throw new InvalidDataException($"Track {trackIndex}: truncated after delta at offset {pos}.");
            byte status = body[pos];
            if ((status & 0x80) == 0)
            {
                // Running status: previous status with this byte as the first data byte.
                status = runningStatus;
            }
            else
            {
                pos++;
                if (status < 0xF8) runningStatus = status;   // ignore real-time messages
            }

            byte high = (byte)(status & 0xF0);
            int channel = status & 0x0F;

            if (status == 0xFF)
            {
                // Meta event: type, len, payload
                if (pos >= body.Length) throw new InvalidDataException($"Track {trackIndex}: truncated meta event.");
                byte metaType = body[pos++];
                int metaLen = ReadVarLen(body, ref pos, trackIndex);
                if (pos + metaLen > body.Length)
                    throw new InvalidDataException($"Track {trackIndex}: meta event payload truncated at offset {pos} (need {metaLen} bytes).");
                if (metaType == 0x51 && metaLen == 3)
                {
                    int uspq = (body[pos] << 16) | (body[pos + 1] << 8) | body[pos + 2];
                    events.Add(new TempoEvent(tick, uspq));
                }
                else if (metaType == 0x2F)
                {
                    events.Add(new EndOfTrackEvent(tick));
                }
                pos += metaLen;
                runningStatus = 0;   // meta events reset running status
                continue;
            }
            if (status == 0xF0 || status == 0xF7)
            {
                // SysEx: variable-length; skip.
                int sysLen = ReadVarLen(body, ref pos, trackIndex);
                if (pos + sysLen > body.Length)
                    throw new InvalidDataException($"Track {trackIndex}: SysEx payload truncated at offset {pos}.");
                pos += sysLen;
                runningStatus = 0;
                continue;
            }

            int needed = high switch
            {
                0x80 or 0x90 or 0xA0 or 0xB0 or 0xE0 => 2,
                0xC0 or 0xD0 => 1,
                _ => 0,
            };
            if (pos + needed > body.Length)
                throw new InvalidDataException($"Track {trackIndex}: voice message truncated at offset {pos} (need {needed} byte(s)).");

            switch (high)
            {
                case 0x80:
                {
                    byte note = body[pos++];
                    byte _ = body[pos++];
                    events.Add(new NoteOffEvent(tick, channel, note));
                    break;
                }
                case 0x90:
                {
                    byte note = body[pos++];
                    byte vel = body[pos++];
                    if (vel == 0) events.Add(new NoteOffEvent(tick, channel, note));
                    else events.Add(new NoteOnEvent(tick, channel, note, vel));
                    break;
                }
                case 0xA0:
                {
                    pos += 2;   // PolyAT ignored
                    break;
                }
                case 0xB0:
                {
                    byte controller = body[pos++];
                    byte value = body[pos++];
                    events.Add(new ControlChangeEvent(tick, channel, controller, value));
                    break;
                }
                case 0xC0:
                {
                    byte program = body[pos++];
                    events.Add(new ProgramChangeEvent(tick, channel, program));
                    break;
                }
                case 0xD0:
                {
                    pos += 1;   // ChannelAT ignored
                    break;
                }
                case 0xE0:
                {
                    byte lsb = body[pos++];
                    byte msb = body[pos++];
                    short value = (short)(((msb << 7) | lsb) - 8192);
                    events.Add(new PitchBendEvent(tick, channel, value));
                    break;
                }
                default:
                    throw new InvalidDataException($"Track {trackIndex}: unsupported status ${status:X2} at offset {pos}.");
            }
        }

        return events;
    }

    private static int ReadVarLen(byte[] buf, ref int pos, int trackIndex)
    {
        int result = 0;
        for (int i = 0; i < 4; i++)
        {
            if (pos >= buf.Length) throw new InvalidDataException($"Track {trackIndex}: var-length quantity truncated at offset {pos}.");
            byte b = buf[pos++];
            result = (result << 7) | (b & 0x7F);
            if ((b & 0x80) == 0) return result;
        }
        throw new InvalidDataException($"Track {trackIndex}: var-length quantity exceeds 4 bytes at offset {pos}.");
    }

    private static byte[] ReadExact(Stream stream, int count, string what)
    {
        var buf = new byte[count];
        int total = 0;
        while (total < count)
        {
            int n = stream.Read(buf, total, count - total);
            if (n <= 0)
                throw new InvalidDataException($"SMF stream truncated reading {what}: got {total} of {count} byte(s).");
            total += n;
        }
        return buf;
    }
}
