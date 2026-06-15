using System.Diagnostics;
using CbmEngine.Abstractions;
using CbmEngine.Systems.Midi;

namespace CbmEngine.Game.Sample;

/// <summary>
/// IGame wrapper that ticks a <see cref="MidiSidBridge"/> in lock-step with wall-clock time
/// rather than the emulator pump's frame counter. The hybrid pump runs at whatever rate the
/// emulator can sustain (typically 40-50 Hz on slow machines); using its raw frame index would
/// stretch MIDI playback by the pump's slip ratio. Computing a virtual frame index from a
/// <see cref="Stopwatch"/> instead keeps tempo locked to wall-clock regardless of emulator speed.
/// </summary>
public sealed class MidiGame : IGame
{
    private readonly MidiSidBridge _bridge;
    private readonly double _refreshHz;
    private readonly Stopwatch _clock = new();
    private int _lastVirtualFrame;

    public MidiGame(MidiSidBridge bridge, double refreshHz = 50.125)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _refreshHz = refreshHz;
    }

    public void Initialize(IGameContext context) => _clock.Restart();

    public void Update(IGameContext context, int frameIndex)
    {
        if (!_clock.IsRunning) _clock.Start();
        int virtualFrame = (int)(_clock.Elapsed.TotalSeconds * _refreshHz);
        if (virtualFrame > _lastVirtualFrame) _lastVirtualFrame = virtualFrame;
        _bridge.Tick(_lastVirtualFrame);
    }

    public void Draw(IGameContext context, int frameIndex) { }
}
