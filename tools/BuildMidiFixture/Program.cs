using System.Buffers.Binary;

string repo = FindRepoRoot();
string outDir = Path.Combine(repo, "fixtures", "midi");
Directory.CreateDirectory(outDir);

// Simple SMF Type 0 sanity fixture: C major arpeggio.
WriteSmf(
    Path.Combine(outDir, "test.mid"),
    format: 0,
    ticksPerQuarter: 96,
    microsecondsPerQuarter: 500_000,
    tracks: new Action<MemoryStream>[] { BuildTestTrack });

// Für Elise (Beethoven, composition public domain). Multi-track:
//   ch 0 (track 1) - melody (right hand) on a bright lead patch.
//   ch 1 (track 2) - alberti-style bass arpeggio (left hand low) on a plucked bass.
//   ch 2 (track 3) - sustained middle-voice harmony (left hand upper) on a pad.
// Tempo 480_000 us / quarter = 125 BPM matches typical performance pace.
WriteSmf(
    Path.Combine(outDir, "fur_elise.mid"),
    format: 1,
    ticksPerQuarter: 96,
    microsecondsPerQuarter: 480_000,
    tracks: new Action<MemoryStream>[]
    {
        BuildFurEliseMelody,
        BuildFurEliseBass,
        BuildFurEliseHarmony,
    });
return;

static void BuildTestTrack(MemoryStream t)
{
    NoteOn (t, 0,  0, 60, 100);
    NoteOff(t, 96, 0, 60);
    NoteOn (t, 0,  0, 64, 100);
    NoteOff(t, 96, 0, 64);
    NoteOn (t, 0,  0, 67, 100);
    NoteOff(t, 96, 0, 67);
    NoteOn (t, 0,  0, 72, 100);
    NoteOff(t, 96, 0, 72);
}

static void BuildFurEliseMelody(MemoryStream t)
{
    // 96 tpq -> eighth = 48 ticks, quarter = 96 ticks.
    // Notes: E5=76 D#5=75 B4=71 D5=74 C5=72 A4=69 E4=64 G#4=68 C4=60.
    const int E = 48;

    void N(int dt, byte note, byte vel) { NoteOn(t, dt, 0, note, vel); NoteOff(t, E, 0, note); }

    // Measure 1: E5 D#5 E5 D#5 E5 B4 D5 C5
    N(0,  76, 95); N(0,  75, 95); N(0,  76, 95); N(0,  75, 95);
    N(0,  76, 95); N(0,  71, 95); N(0,  74, 95); N(0,  72, 95);
    // Measure 2: A4 quarter, then E4 (rest in melody until accent)
    NoteOn(t, 0, 0, 69, 105); NoteOff(t, E * 2, 0, 69);
    NoteOn(t, E, 0, 64, 70); NoteOff(t, E, 0, 64);
    // Measure 3: B4 quarter, then E4
    NoteOn(t, E, 0, 71, 100); NoteOff(t, E * 2, 0, 71);
    NoteOn(t, E, 0, 64, 70); NoteOff(t, E, 0, 64);
    // Measure 4: C5 quarter, then E4, E5 D#5
    NoteOn(t, E, 0, 72, 100); NoteOff(t, E * 2, 0, 72);
    NoteOn(t, E, 0, 64, 70); NoteOff(t, E, 0, 64);
    N(0,  76, 90); N(0,  75, 90);
    // Measure 5: repeat measure 1
    N(0,  76, 95); N(0,  75, 95); N(0,  76, 95); N(0,  75, 95);
    N(0,  76, 95); N(0,  71, 95); N(0,  74, 95); N(0,  72, 95);
    // Measure 6: A4 quarter, then E4
    NoteOn(t, 0, 0, 69, 105); NoteOff(t, E * 2, 0, 69);
    NoteOn(t, E, 0, 64, 70); NoteOff(t, E, 0, 64);
    // Measure 7: B4 quarter, then E4
    NoteOn(t, E, 0, 71, 100); NoteOff(t, E * 2, 0, 71);
    NoteOn(t, E, 0, 64, 70); NoteOff(t, E, 0, 64);
    // Measure 8: A4 dotted-half closing tonic
    NoteOn(t, E, 0, 69, 110); NoteOff(t, E * 6, 0, 69);
}

static void BuildFurEliseBass(MemoryStream t)
{
    // Alberti-style left-hand bass: rapid low note + middle + low pulses (eighths) under each chord.
    // Chord-per-measure plan (eighth-note pulses):
    //   M1 A min:    A2  A3  A2  A3  A2  A3  A2  A3
    //   M2 A min:    A2  A3  A2  A3  A2  A3  A2  A3
    //   M3 E maj:    E2  E3  E2  E3  E2  E3  E2  E3
    //   M4 C maj:    C2  C3  C2  C3  C2  C3  C2  C3
    //   M5 A min:    A2  A3  ...
    //   M6 A min:    A2  A3  ...
    //   M7 E maj:    E2  E3  ...
    //   M8 A min:    A2  E3  A2  E3  A3 (cadence) ----
    const int E = 48;
    byte A2 = 45, A3 = 57, E2 = 40, E3 = 52, C2 = 36, C3 = 48;
    void Eight(byte note, byte vel) { NoteOn(t, 0, 1, note, vel); NoteOff(t, E, 1, note); }

    void Pulse8(byte low, byte high, byte vel = 75)
    {
        for (int i = 0; i < 4; i++) { Eight(low, vel); Eight(high, vel); }
    }

    Pulse8(A2, A3);          // M1
    Pulse8(A2, A3);          // M2
    Pulse8(E2, E3);          // M3
    Pulse8(C2, C3);          // M4
    Pulse8(A2, A3);          // M5
    Pulse8(A2, A3);          // M6
    Pulse8(E2, E3);          // M7
    // M8 closing: A2 E3 A2 E3 A2 (cadence on tonic)
    Eight(A2, 90); Eight(E3, 80); Eight(A2, 90); Eight(E3, 80); Eight(A2, 95);
    NoteOn(t, 0, 1, A3, 85); NoteOff(t, E * 3, 1, A3);
}

static void BuildFurEliseHarmony(MemoryStream t)
{
    // Pad: a single sustained "alto" note per measure, voiced as the 3rd or 5th of the
    // underlying chord. Sits between melody and bass to fill the texture.
    //   M1 A min  -> C4  (60)  whole measure
    //   M2 A min  -> C4
    //   M3 E maj  -> G#4 (68)
    //   M4 C maj  -> E4  (64)
    //   M5 A min  -> C4
    //   M6 A min  -> C4
    //   M7 E maj  -> G#4
    //   M8 A min  -> E4
    const int M = 48 * 8;   // one measure = 8 eighths

    void Whole(byte note, byte vel)
    {
        NoteOn(t, 0, 2, note, vel);
        NoteOff(t, M, 2, note);
    }

    Whole(60, 55);   // M1 C4
    Whole(60, 55);   // M2
    Whole(68, 55);   // M3 G#4
    Whole(64, 55);   // M4 E4
    Whole(60, 55);   // M5
    Whole(60, 55);   // M6
    Whole(68, 55);   // M7
    Whole(64, 55);   // M8
}

static void NoteOn(MemoryStream track, int dt, int channel, byte note, byte vel)
{
    WriteVarLen(track, (uint)dt);
    track.WriteByte((byte)(0x90 | (channel & 0x0F)));
    track.WriteByte(note);
    track.WriteByte(vel);
}

static void NoteOff(MemoryStream track, int dt, int channel, byte note)
{
    WriteVarLen(track, (uint)dt);
    track.WriteByte((byte)(0x80 | (channel & 0x0F)));
    track.WriteByte(note);
    track.WriteByte(0);
}

static void WriteSmf(string path, int format, int ticksPerQuarter, int microsecondsPerQuarter, IReadOnlyList<Action<MemoryStream>> tracks)
{
    var trackBytes = new List<byte[]>(tracks.Count);
    for (int i = 0; i < tracks.Count; i++)
    {
        var track = new MemoryStream();
        // First track in a Type 1 file carries the tempo meta event so all tracks share it; in
        // Type 0 there is only one track and it also gets the tempo here.
        if (i == 0)
        {
            WriteVarLen(track, 0);
            track.WriteByte(0xFF); track.WriteByte(0x51); track.WriteByte(0x03);
            track.WriteByte((byte)((microsecondsPerQuarter >> 16) & 0xFF));
            track.WriteByte((byte)((microsecondsPerQuarter >> 8) & 0xFF));
            track.WriteByte((byte)(microsecondsPerQuarter & 0xFF));
        }
        tracks[i](track);
        WriteVarLen(track, 0);
        track.WriteByte(0xFF); track.WriteByte(0x2F); track.WriteByte(0x00);
        trackBytes.Add(track.ToArray());
    }

    var file = new MemoryStream();
    file.Write("MThd"u8);
    WriteUInt32BE(file, 6);
    WriteUInt16BE(file, (ushort)format);
    WriteUInt16BE(file, (ushort)tracks.Count);
    WriteUInt16BE(file, (ushort)ticksPerQuarter);
    foreach (var bytes in trackBytes)
    {
        file.Write("MTrk"u8);
        WriteUInt32BE(file, (uint)bytes.Length);
        file.Write(bytes);
    }

    File.WriteAllBytes(path, file.ToArray());
    Console.WriteLine($"Wrote {path}  ({file.Length} bytes, {tracks.Count} track(s))");
}

static void WriteUInt32BE(MemoryStream ms, uint v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(b, v); ms.Write(b); }
static void WriteUInt16BE(MemoryStream ms, ushort v) { Span<byte> b = stackalloc byte[2]; BinaryPrimitives.WriteUInt16BigEndian(b, v); ms.Write(b); }
static void WriteVarLen(MemoryStream ms, uint v)
{
    Span<byte> buf = stackalloc byte[4]; int n = 0;
    buf[n++] = (byte)(v & 0x7F); v >>= 7;
    while (v > 0) { buf[n++] = (byte)((v & 0x7F) | 0x80); v >>= 7; }
    for (int i = n - 1; i >= 0; i--) ms.WriteByte(buf[i]);
}

static string FindRepoRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d is not null && !File.Exists(Path.Combine(d.FullName, "CbmEngine.slnx"))) d = d.Parent;
    return d?.FullName ?? throw new InvalidOperationException("repo root not found");
}
