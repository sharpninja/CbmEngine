using CbmEngine.Abstractions;
using CbmEngine.Pipeline.Midi;
using CbmEngine.Systems.Midi;
using CbmEngine.Systems.Strategy;
using CbmEngine.Tests.Integration.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace CbmEngine.Tests.Integration.Phase9;

[Trait("Speed", "Slow")]
public class MidiSidBridgeIntegrationTests
{
    private readonly ITestOutputHelper _out;
    public MidiSidBridgeIntegrationTests(ITestOutputHelper output) { _out = output; }

    private static ICommodoreMachine BuildC64()
    {
        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        for (int i = 0; i < 120; i++) sys.RunFrame();
        return sys;
    }

    [Fact]
    public void TEST_CBM_MIDI_004_NoteOn_WritesFrequencyAndGate()
    {
        var sys = BuildC64();
        var bridge = new MidiSidBridge(sys);

        bridge.NoteOn(voice: 0, midiNote: 69, velocity: 100);   // A4 = 440 Hz
        ushort expected = sys.Sound.HzToRegister(440.0);
        byte lo = sys.Bus.Read(0xD400);
        byte hi = sys.Bus.Read(0xD401);
        ushort observed = (ushort)(lo | (hi << 8));
        byte ctrl = sys.Bus.Read(0xD404);

        _out.WriteLine($"$D400/$D401 = {observed:X4} (expected {expected:X4})  $D404 = {ctrl:X2}");
        Assert.InRange(observed, (ushort)(expected - 1), (ushort)(expected + 1));
        Assert.True((ctrl & 0x01) == 0x01, $"gate bit expected; D404=${ctrl:X2}");
    }

    [Fact]
    public void TEST_CBM_MIDI_005_NoteOff_ClearsGate()
    {
        var sys = BuildC64();
        var bridge = new MidiSidBridge(sys);
        bridge.NoteOn(voice: 0, midiNote: 69, velocity: 100);
        Assert.True((sys.Bus.Read(0xD404) & 0x01) == 0x01);
        bridge.NoteOff(voice: 0);
        sys.RunFrame();
        Assert.True((sys.Bus.Read(0xD404) & 0x01) == 0x00, $"gate should be off after NoteOff; D404=${sys.Bus.Read(0xD404):X2}");
    }

    [Fact]
    public void TEST_CBM_MIDI_008_ProgramChange_SwapsWaveformBits()
    {
        var sys = BuildC64();
        var bridge = new MidiSidBridge(sys);
        // Default channel 0 patch is triangle. Switch to bass (sawtooth program).
        bridge.SetPatch(channel: 0, SidPatchLibrary.ForProgram(38));   // GM Synth Bass 1 -> sawtooth
        bridge.NoteOn(voice: 0, midiNote: 60, velocity: 100);

        byte ctrl = sys.Bus.Read(0xD404);
        _out.WriteLine($"D404 after sawtooth NoteOn = ${ctrl:X2}");
        Assert.True((ctrl & 0x20) == 0x20, "sawtooth bit (D5) should be set");
        Assert.True((ctrl & 0x10) == 0x00, "triangle bit (D4) should be clear");
    }

    [Fact]
    public void ThreeVoices_AllThreeGatesSet()
    {
        var sys = BuildC64();
        var bridge = new MidiSidBridge(sys);
        bridge.NoteOn(0, 60, 100);
        bridge.NoteOn(1, 64, 100);
        bridge.NoteOn(2, 67, 100);
        Assert.True((sys.Bus.Read(0xD404) & 0x01) == 0x01);
        Assert.True((sys.Bus.Read(0xD40B) & 0x01) == 0x01);
        Assert.True((sys.Bus.Read(0xD412) & 0x01) == 0x01);
    }
}
