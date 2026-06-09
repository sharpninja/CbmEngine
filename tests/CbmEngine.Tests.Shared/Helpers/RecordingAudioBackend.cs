using ViceSharp.Abstractions;

namespace CbmEngine.Tests.Shared.Helpers;

public sealed class RecordingAudioBackend : IAudioBackend
{
    private readonly List<float> _samples = new();
    public IReadOnlyList<float> Samples => _samples;
    public int SubmitCallCount { get; private set; }

    public int QueuedSampleCount => _samples.Count;

    public void SubmitSamples(ReadOnlySpan<float> samples)
    {
        SubmitCallCount++;
        for (int i = 0; i < samples.Length; i++) _samples.Add(samples[i]);
    }

    public void Pause() { }
    public void Resume() { }
    public void Stop() => _samples.Clear();
}
