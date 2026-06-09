using ViceSharp.Abstractions;

namespace CbmEngine.Host.MonoGame;

public sealed class SidPump
{
    private readonly IAudioChip _sid;
    private readonly IAudioBackend _backend;
    private readonly float[] _buffer;

    public int SamplesPerFrame => _buffer.Length;

    public SidPump(IAudioChip sid, IAudioBackend backend, int sampleRate, double refreshHz)
    {
        ArgumentNullException.ThrowIfNull(sid);
        ArgumentNullException.ThrowIfNull(backend);
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (refreshHz <= 0) throw new ArgumentOutOfRangeException(nameof(refreshHz));

        _sid = sid;
        _backend = backend;
        _buffer = new float[(int)Math.Round(sampleRate / refreshHz)];
    }

    public void PumpFrame()
    {
        for (int i = 0; i < _buffer.Length; i++)
            _buffer[i] = _sid.GenerateSample();
        _backend.SubmitSamples(_buffer);
    }
}
