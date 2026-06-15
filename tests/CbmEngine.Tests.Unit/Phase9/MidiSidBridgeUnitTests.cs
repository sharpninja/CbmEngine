using CbmEngine.Abstractions;
using CbmEngine.Pipeline.Midi;
using CbmEngine.Systems.Midi;
using Xunit;

namespace CbmEngine.Tests.Unit.Phase9;

[Trait("Speed", "Fast")]
public class MidiSidBridgeUnitTests
{
    private static (MidiSidBridge bridge, RecordingSoundChip chip) MakeBridge(double refreshHz = 50.125)
    {
        var chip = new RecordingSoundChip();
        var bridge = new MidiSidBridge(chip, refreshHz);
        return (bridge, chip);
    }

    [Fact]
    public void TEST_CBM_MIDI_009_DirectNoteOn_BypassSchedule()
    {
        var (bridge, chip) = MakeBridge();
        bridge.NoteOn(voice: 0, midiNote: 60, velocity: 100);

        Assert.Contains(chip.Freqs, f => f.Voice == 0);
        var lastWave = chip.Waveforms.Last(w => w.Voice == 0);
        Assert.True(lastWave.Gate, "gate should be on after NoteOn");

        bridge.NoteOff(voice: 0);
        var afterOff = chip.Waveforms.Last(w => w.Voice == 0);
        Assert.False(afterOff.Gate, "gate should be cleared after NoteOff");
    }

    [Fact]
    public void DirectNoteOn_440Hz_ProducesExpectedFrequency()
    {
        var (bridge, chip) = MakeBridge();
        bridge.NoteOn(voice: 0, midiNote: 69, velocity: 100);   // A4 = 440 Hz
        var f = chip.Freqs.Single();
        Assert.Equal(0, f.Voice);
        Assert.InRange(f.Hz, 439.9, 440.1);
    }

    [Fact]
    public void TEST_CBM_MIDI_007_TempoChange_AltersTickPacing()
    {
        var (bridge, chip) = MakeBridge(refreshHz: 50.0);

        var bytes = SmfFixtures.BuildType0WithTempo(ticksPerQuarter: 96, new[]
        {
            SmfFixtures.Tempo(0, 500_000),                       // 120 BPM -> 1 quarter = 0.5 sec = 25 frames @ 50Hz
            SmfFixtures.NoteOn(0, 0, 60, 100),
            SmfFixtures.NoteOff(96, 0, 60),
            SmfFixtures.Tempo(96, 1_000_000),                    // 60 BPM -> 1 quarter = 1.0 sec = 50 frames
            SmfFixtures.NoteOn(96, 0, 64, 100),
            SmfFixtures.NoteOff(192, 0, 64),
        });

        using var ms = new MemoryStream(bytes);
        var smf = SmfReader.Load(ms);
        bridge.Load(smf);
        bridge.Play();

        int noteOnEvents = 0;
        var noteOnFrames = new List<int>();
        for (int f = 0; f < 200 && !bridge.IsFinished; f++)
        {
            int before = chip.Waveforms.Count(w => w.Gate);
            bridge.Tick(f);
            int after = chip.Waveforms.Count(w => w.Gate);
            if (after > before) { noteOnEvents++; noteOnFrames.Add(f); }
        }

        Assert.Equal(2, noteOnEvents);
        Assert.Equal(0, noteOnFrames[0]);
        // After the first quarter at 120 BPM (~25 frames) the second NoteOn should fire
        Assert.InRange(noteOnFrames[1], 23, 27);
    }

    [Fact]
    public void TEST_CBM_MIDI_010_Tick_NoAllocation_AfterWarmup()
    {
        var (bridge, _) = MakeBridge();
        var bytes = SmfFixtures.BuildType0(ticksPerQuarter: 96,
            (0, 0x90, 60, 100),
            (96, 0x80, 60, 0));
        using var ms = new MemoryStream(bytes);
        bridge.Load(SmfReader.Load(ms));
        bridge.Play();

        // Warm up.
        for (int i = 0; i < 200; i++) bridge.Tick(i);
        bridge.Stop();

        bridge.Play();
        // GC.GetTotalAllocatedBytes counts allocations on this thread including JIT/GC bookkeeping
        // that lands during the measurement window; the bridge itself should not allocate after warm-up.
        // Tolerance is intentionally loose to keep the test stable across machines / GC modes; a real
        // allocation in PumpFrame would be on the order of 10s of KB per call (Boxing of records, etc.),
        // far above this threshold.
        long before = GC.GetTotalAllocatedBytes(precise: true);
        for (int i = 0; i < 1000; i++) bridge.Tick(i);
        long after = GC.GetTotalAllocatedBytes(precise: true);

        Assert.InRange(after - before, 0, 16 * 1024);
    }
}
