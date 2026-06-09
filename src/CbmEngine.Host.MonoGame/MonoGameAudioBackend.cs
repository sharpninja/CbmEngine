using Microsoft.Xna.Framework.Audio;
using ViceSharp.Abstractions;

namespace CbmEngine.Host.MonoGame;

public sealed class MonoGameAudioBackend : IAudioBackend, IDisposable
{
    private readonly DynamicSoundEffectInstance _instance;
    private readonly int _sampleRate;

    public int QueuedSampleCount => _instance.PendingBufferCount * 1024;

    public MonoGameAudioBackend(int sampleRate = 44100)
    {
        _sampleRate = sampleRate;
        _instance = new DynamicSoundEffectInstance(sampleRate, AudioChannels.Mono);
        _instance.Play();
    }

    public void SubmitSamples(ReadOnlySpan<float> samples)
    {
        var pcm = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short s = (short)Math.Clamp(samples[i] * 32767f, -32768f, 32767f);
            pcm[i * 2] = (byte)(s & 0xFF);
            pcm[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }
        _instance.SubmitBuffer(pcm);
    }

    public void Pause() => _instance.Pause();
    public void Resume() => _instance.Resume();
    public void Stop() => _instance.Stop();
    public void Dispose() => _instance.Dispose();
}
