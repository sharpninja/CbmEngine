using CbmEngine.Abstractions;
using ViceSharp.Abstractions;

namespace CbmEngine.Host.MonoGame;

public sealed class HeadlessHost
{
    private readonly IMachine _machine;
    private readonly IVideoChip _video;
    private readonly IBlitTarget _blit;
    private readonly IInputScript _input;
    private readonly IClockSource _clock;
    private readonly IKeyboardMatrix? _keyboard;
    private readonly SidPump? _audio;
    private readonly double _refreshHz;

    public int FrameCount { get; private set; }

    public HeadlessHost(
        IMachine machine,
        IBlitTarget blit,
        IInputScript input,
        IClockSource clock,
        double refreshHz,
        SidPump? audio = null)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(blit);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(clock);
        if (refreshHz <= 0) throw new ArgumentOutOfRangeException(nameof(refreshHz));

        _machine = machine;
        _video = machine.Devices.GetByRole(DeviceRole.VideoChip) as IVideoChip
            ?? throw new InvalidOperationException("Machine has no IVideoChip.");
        _keyboard = machine.Devices.GetAll<IKeyboardMatrix>() is { Count: > 0 } list ? list[0] : null;
        _blit = blit;
        _input = input;
        _clock = clock;
        _refreshHz = refreshHz;
        _audio = audio;
    }

    public void Run(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return;
        var frameInterval = TimeSpan.FromSeconds(1.0 / _refreshHz);
        var end = _clock.Now + duration;
        while (_clock.Now < end)
        {
            RunOneFrame();
            _clock.Tick(frameInterval);
        }
    }

    public void RunFrames(int frames)
    {
        if (frames < 0) throw new ArgumentOutOfRangeException(nameof(frames));
        for (int i = 0; i < frames; i++) RunOneFrame();
    }

    private void RunOneFrame()
    {
        if (_keyboard is not null)
        {
            var events = _input.DrainForFrame(FrameCount);
            for (int i = 0; i < events.Count; i++)
                _keyboard.SetKey(events[i].MatrixCode, events[i].Pressed);
        }

        _machine.RunFrame();
        _blit.Upload(_video.FrameBuffer, _video.FrameWidth, _video.FrameHeight);
        _audio?.PumpFrame();
        FrameCount++;
    }
}
