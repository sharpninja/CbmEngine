using CbmEngine.Abstractions;
using CbmEngine.Host.MonoGame;
using CbmEngine.Pipeline.Midi;
using CbmEngine.Systems.Midi;
using CbmEngine.Systems.Strategy;
using CbmEngine.Tests.Integration.Helpers;
using ViceSharp.Abstractions;
using ViceSharp.Core.Media;
using SharedRecordingBackend = CbmEngine.Tests.Shared.Helpers.RecordingAudioBackend;
using Xunit;

namespace CbmEngine.Tests.Integration.Phase9;

[Trait("Speed", "Slow")]
public class FurEliseAudioCaptureTests
{
    private const int SampleRate = 44100;
    private const double PalRefresh = 50.125;
    private const int DurationSeconds = 12;

    private readonly ITestOutputHelper _out;
    public FurEliseAudioCaptureTests(ITestOutputHelper output) { _out = output; }

    [Fact]
    public void TEST_CBM_MIDI_011_FurElise_CapturesAudibleWavForHumanReview()
    {
        var repoRoot = FindRepoRoot();
        var midiPath = Path.Combine(repoRoot, "fixtures", "midi", "fur_elise.mid");
        Assert.True(File.Exists(midiPath), $"Fur Elise fixture missing at {midiPath}; run tools/BuildMidiFixture first.");

        var wavDir = Path.Combine(repoRoot, "artifacts", "phase9");
        Directory.CreateDirectory(wavDir);
        var wavPath = Path.Combine(wavDir, "fur_elise.wav");

        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        for (int i = 0; i < 120; i++) sys.RunFrame();
        sys.Sound.SetVolume(15);

        var sid = sys.Underlying.Devices.GetByRole(DeviceRole.AudioChip) as IAudioChip
            ?? throw new InvalidOperationException("No SID device");

        using var wavStream = File.Create(wavPath);
        using var wavRecorder = new WavAudioRecorder(wavStream, sampleRate: SampleRate, channels: 1);
        var capturingBackend = new SharedRecordingBackend();
        var teeBackend = new TeeBackend(wavRecorder, capturingBackend, SampleRate);

        var pump = new SidPump(sid, teeBackend, SampleRate, PalRefresh);

        using var fs = File.OpenRead(midiPath);
        var smf = SmfReader.Load(fs);
        _out.WriteLine($"Loaded {midiPath}: format={smf.Format} tracks={smf.TrackCount} tpq={smf.TicksPerQuarter}");

        var bridge = new MidiSidBridge(sys);
        // Channel-aware voicing: melody on lead pulse, bass on plucked sawtooth, harmony on pad triangle.
        bridge.SetPatch(0, SidPatch.LeadPulse);
        bridge.SetPatch(1, SidPatch.BassPluck);
        bridge.SetPatch(2, SidPatch.Pad);
        bridge.Load(smf);
        bridge.Play();

        int totalFrames = (int)(DurationSeconds * PalRefresh);
        for (int f = 0; f < totalFrames; f++)
        {
            bridge.Tick(f);
            sys.RunFrame();
            pump.PumpFrame();
        }

        wavRecorder.Stop();
        wavStream.Flush();

        int audible = 0;
        float maxAbs = 0f;
        double sumAbs = 0;
        for (int i = 0; i < capturingBackend.Samples.Count; i++)
        {
            float a = Math.Abs(capturingBackend.Samples[i]);
            if (a > maxAbs) maxAbs = a;
            sumAbs += a;
            if (a > 0.001f) audible++;
        }
        double meanAbs = capturingBackend.Samples.Count > 0 ? sumAbs / capturingBackend.Samples.Count : 0;

        _out.WriteLine($"WAV: {wavPath}");
        _out.WriteLine($"Samples captured: {capturingBackend.Samples.Count}; audible (|s|>0.001): {audible}");
        _out.WriteLine($"Peak |sample|: {maxAbs:F4}; mean |sample|: {meanAbs:F4}");
        _out.WriteLine($"WAV bytes: {new FileInfo(wavPath).Length}");

        Assert.True(maxAbs > 0.05f, $"Peak signal {maxAbs:F4} below 0.05; SID effectively silent.");
        Assert.True(audible >= 5000, $"Expected >=5000 audible samples over {DurationSeconds}s of Fur Elise; got {audible}.");
        Assert.True(new FileInfo(wavPath).Length > WavAudioRecorder.HeaderSize + 1000, "WAV is too small to be a real recording.");
    }

    private static string FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "CbmEngine.slnx"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    /// <summary>
    /// Tees float samples to the captured backend (raw) and to the WAV file (DC-blocked + gained).
    /// Vice-sharp's SID output is unipolar (~0.05..1.05 with a +0.05 digi-DC offset and a /3 voice
    /// mixer) which makes the WAV technically correct but sit on a constant positive rail. A 1-pole
    /// high-pass IIR removes the DC and a uniform gain pushes the peak toward -3 dBFS so the
    /// human-review WAV is actually loud enough to listen to without amplification.
    /// </summary>
    private sealed class TeeBackend : IAudioBackend
    {
        private const float HighPassCutoffHz = 20f;
        private const float TargetPeak = 0.7f;

        private readonly WavAudioRecorder _recorder;
        private readonly IAudioBackend _captured;
        private readonly short[] _scratch = new short[4096];
        private readonly float _alpha;
        private float _prevIn;
        private float _prevOut;
        private float _runningPeak;
        private float _gain = 1.0f;

        public TeeBackend(WavAudioRecorder recorder, IAudioBackend captured, int sampleRate)
        {
            _recorder = recorder;
            _captured = captured;
            _alpha = 1f - (2f * MathF.PI * HighPassCutoffHz / sampleRate);
        }

        public int QueuedSampleCount => 0;

        public void SubmitSamples(ReadOnlySpan<float> samples)
        {
            int pos = 0;
            while (pos < samples.Length)
            {
                int chunk = Math.Min(_scratch.Length, samples.Length - pos);
                for (int i = 0; i < chunk; i++)
                {
                    float x = samples[pos + i];
                    float y = _alpha * (_prevOut + x - _prevIn);
                    _prevIn = x;
                    _prevOut = y;

                    float a = MathF.Abs(y);
                    if (a > _runningPeak) _runningPeak = a;
                    if (_runningPeak > 0.001f) _gain = TargetPeak / _runningPeak;

                    float boosted = y * _gain;
                    if (boosted > 1f) boosted = 1f;
                    else if (boosted < -1f) boosted = -1f;
                    _scratch[i] = (short)(boosted * 32767f);
                }
                _recorder.WriteSamples(new ReadOnlySpan<short>(_scratch, 0, chunk));
                pos += chunk;
            }
            _captured.SubmitSamples(samples);
        }

        public void Pause() { }
        public void Resume() { }
        public void Stop() { }
    }
}
