using CbmEngine.Pipeline.Midi;
using Xunit;

namespace CbmEngine.Tests.Unit.Phase9;

[Trait("Speed", "Fast")]
public class SmfReaderTests
{
    [Fact]
    public void TEST_CBM_MIDI_001_LoadType0_SingleTrack_ReturnsExpectedEvents()
    {
        var bytes = SmfFixtures.BuildType0(ticksPerQuarter: 96,
            (0,  0x90, 60, 100),   // NoteOn  ch0 C4
            (96, 0x80, 60, 0),     // NoteOff ch0 C4
            (96, 0x90, 64, 100),   // NoteOn  ch0 E4
            (192,0x80, 64, 0),     // NoteOff ch0 E4
            (192,0x90, 67, 100),   // NoteOn  ch0 G4
            (288,0x80, 67, 0),     // NoteOff ch0 G4
            (288,0x90, 72, 100),   // NoteOn  ch0 C5
            (384,0x80, 72, 0));    // NoteOff ch0 C5

        using var ms = new MemoryStream(bytes);
        var smf = SmfReader.Load(ms);

        Assert.Equal(0, smf.Format);
        Assert.Equal(1, smf.TrackCount);
        Assert.Equal(96, smf.TicksPerQuarter);
        Assert.Single(smf.Tracks);

        var track = smf.Tracks[0];
        var notes = track.OfType<NoteOnEvent>().ToList();
        Assert.Equal(4, notes.Count);
        Assert.Equal(60, notes[0].Note);
        Assert.Equal(64, notes[1].Note);
        Assert.Equal(67, notes[2].Note);
        Assert.Equal(72, notes[3].Note);
        Assert.Equal(4, track.OfType<NoteOffEvent>().Count());
    }

    [Fact]
    public void TEST_CBM_MIDI_002_LoadType1_MultiTrack_PreservesTrackBoundaries()
    {
        var bytes = SmfFixtures.BuildType1(ticksPerQuarter: 480,
            new (long, byte, byte, byte)[] { (0, 0x90, 36, 100), (480, 0x80, 36, 0) },
            new (long, byte, byte, byte)[] { (0, 0x90, 60, 100), (480, 0x80, 60, 0) },
            new (long, byte, byte, byte)[] { (0, 0x90, 72, 100), (480, 0x80, 72, 0) });

        using var ms = new MemoryStream(bytes);
        var smf = SmfReader.Load(ms);

        Assert.Equal(1, smf.Format);
        Assert.Equal(3, smf.TrackCount);
        Assert.Equal(3, smf.Tracks.Count);
        Assert.Equal(36, smf.Tracks[0].OfType<NoteOnEvent>().Single().Note);
        Assert.Equal(60, smf.Tracks[1].OfType<NoteOnEvent>().Single().Note);
        Assert.Equal(72, smf.Tracks[2].OfType<NoteOnEvent>().Single().Note);
    }

    [Fact]
    public void TEST_CBM_MIDI_003_Truncated_ThrowsWithOffset()
    {
        var bytes = SmfFixtures.BuildType0(ticksPerQuarter: 96, (0, 0x90, 60, 100), (96, 0x80, 60, 0));
        var truncated = bytes.AsSpan(0, bytes.Length - 5).ToArray();
        using var ms = new MemoryStream(truncated);
        var ex = Assert.Throws<InvalidDataException>(() => SmfReader.Load(ms));
        Assert.Contains("truncated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_BadHeaderMagic_Throws()
    {
        var bytes = new byte[8] { (byte)'X', (byte)'X', (byte)'X', (byte)'X', 0, 0, 0, 6 };
        using var ms = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() => SmfReader.Load(ms));
    }

    [Fact]
    public void Load_TempoMetaEvent_ParsedAsTempoEvent()
    {
        var bytes = SmfFixtures.BuildType0WithTempo(ticksPerQuarter: 96, new[]
        {
            SmfFixtures.Tempo(0, microsecondsPerQuarter: 500_000),    // 120 BPM
            SmfFixtures.NoteOn(0, 0, 60, 100),
            SmfFixtures.NoteOff(96, 0, 60),
        });
        using var ms = new MemoryStream(bytes);
        var smf = SmfReader.Load(ms);
        var tempo = smf.Tracks[0].OfType<TempoEvent>().Single();
        Assert.Equal(500_000, tempo.MicrosecondsPerQuarter);
    }
}
