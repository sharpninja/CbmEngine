using CbmEngine.Systems.Midi;
using Xunit;

namespace CbmEngine.Tests.Unit.Phase9;

[Trait("Speed", "Fast")]
public class VoiceAllocatorTests
{
    [Fact]
    public void TEST_CBM_MIDI_006_FourthNote_StealsLongestSustained()
    {
        var alloc = new VoiceAllocator();
        int v0 = alloc.NoteOn(channel: 0, midiNote: 60, startFrame: 0);
        int v1 = alloc.NoteOn(channel: 0, midiNote: 64, startFrame: 1);
        int v2 = alloc.NoteOn(channel: 0, midiNote: 67, startFrame: 2);

        Assert.NotEqual(v0, v1);
        Assert.NotEqual(v0, v2);
        Assert.NotEqual(v1, v2);
        Assert.Equal(3, alloc.ActiveCount);

        int v3 = alloc.NoteOn(channel: 0, midiNote: 72, startFrame: 3);
        Assert.Equal(v0, v3);
        Assert.Equal(3, alloc.ActiveCount);
        Assert.Equal(72, alloc.GetNote(v3));
    }

    [Fact]
    public void NoteOff_ClearsVoice_AndAllowsReuse()
    {
        var alloc = new VoiceAllocator();
        int v0 = alloc.NoteOn(0, 60, 0);
        int v1 = alloc.NoteOn(0, 64, 1);
        int v2 = alloc.NoteOn(0, 67, 2);
        Assert.Equal(3, alloc.ActiveCount);

        int releasedVoice = alloc.NoteOff(0, 64, releaseFrame: 5);
        Assert.Equal(v1, releasedVoice);
        Assert.Equal(2, alloc.ActiveCount);

        int v3 = alloc.NoteOn(0, 72, startFrame: 6);
        Assert.Equal(v1, v3);
    }

    [Fact]
    public void NoteOff_NoMatch_ReturnsFalse()
    {
        var alloc = new VoiceAllocator();
        alloc.NoteOn(0, 60, 0);
        Assert.Equal(-1, alloc.NoteOff(0, 99, releaseFrame: 1));
    }

    [Fact]
    public void Stealing_Prefers_ReleasedVoice_OverSustained()
    {
        var alloc = new VoiceAllocator();
        int v0 = alloc.NoteOn(0, 60, 0);
        int v1 = alloc.NoteOn(0, 64, 1);
        int v2 = alloc.NoteOn(0, 67, 2);
        alloc.NoteOff(0, 64, releaseFrame: 3);
        int v3 = alloc.NoteOn(0, 72, startFrame: 4);
        Assert.Equal(v1, v3);   // reuse the released voice, not steal a sustained one
        Assert.NotEqual(v0, v3);
        Assert.NotEqual(v2, v3);
    }
}
