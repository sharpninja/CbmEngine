using System.Diagnostics;
using CbmEngine.Abstractions;
using ViceSharp.Abstractions;

namespace CbmEngine.Host.MonoGame;

public sealed class EmulatorPump : IDisposable
{
    private readonly IMachine _machine;
    private readonly IVideoChip _video;
    private readonly IKeyboardMatrix? _keyboard;
    private readonly IGame? _game;
    private readonly IGameContext? _gameContext;
    private readonly SidPump? _sidPump;
    private readonly Thread _thread;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _visibleLock = new();
    private readonly object _inputLock = new();
    private readonly byte[] _visibleBuffer;
    private readonly Queue<KeyEvent> _keyQueue = new();
    private readonly double _targetHz;
    private long _framesCompleted;
    private long _lateFrames;
    private long _emuStepTicks;

    public int FrameWidth { get; }
    public int FrameHeight { get; }
    public long FramesCompleted => Interlocked.Read(ref _framesCompleted);
    public long LateFrames => Interlocked.Read(ref _lateFrames);
    public double AverageEmulatorStepMs => Interlocked.Read(ref _framesCompleted) is var f && f > 0
        ? Interlocked.Read(ref _emuStepTicks) * 1000.0 / Stopwatch.Frequency / f
        : 0;

    public EmulatorPump(
        IMachine machine,
        double targetHz,
        IGame? game = null,
        IGameContext? gameContext = null,
        SidPump? sidPump = null)
    {
        _machine = machine ?? throw new ArgumentNullException(nameof(machine));
        _video = machine.Devices.GetByRole(DeviceRole.VideoChip) as IVideoChip
            ?? throw new InvalidOperationException("Machine has no IVideoChip.");
        _keyboard = machine.Devices.GetAll<IKeyboardMatrix>() is { Count: > 0 } kl ? kl[0] : null;
        _game = game;
        _gameContext = gameContext;
        _sidPump = sidPump;
        _targetHz = targetHz;
        FrameWidth = _video.FrameWidth;
        FrameHeight = _video.FrameHeight;
        _visibleBuffer = new byte[FrameWidth * FrameHeight * 4];
        _thread = new Thread(Run) { IsBackground = true, Name = "CbmEmuPump" };
    }

    public void Start()
    {
        if (_game is not null && _gameContext is not null)
            _game.Initialize(_gameContext);
        _thread.Start();
    }

    public void EnqueueKey(byte matrixCode, bool pressed)
    {
        lock (_inputLock) _keyQueue.Enqueue(new KeyEvent(matrixCode, pressed));
    }

    public void CopyLatestFrame(Span<byte> dest)
    {
        lock (_visibleLock) _visibleBuffer.AsSpan().CopyTo(dest);
    }

    public ReadOnlySpan<byte> AcquireFrameForUpload()
    {
        Monitor.Enter(_visibleLock);
        return _visibleBuffer;
    }

    public void ReleaseFrame() => Monitor.Exit(_visibleLock);

    private void Run()
    {
        long periodTicks = (long)(Stopwatch.Frequency / _targetHz);
        long nextDue = Stopwatch.GetTimestamp();
        int frameIndex = 0;
        var sw = new Stopwatch();
        var ct = _cts.Token;

        while (!ct.IsCancellationRequested)
        {
            if (_keyboard is not null)
            {
                lock (_inputLock)
                {
                    while (_keyQueue.Count > 0)
                    {
                        var ev = _keyQueue.Dequeue();
                        _keyboard.SetKey(ev.MatrixCode, ev.Pressed);
                    }
                }
            }

            if (_game is not null && _gameContext is not null)
                _game.Update(_gameContext, frameIndex);

            sw.Restart();
            _machine.RunFrame();
            sw.Stop();
            Interlocked.Add(ref _emuStepTicks, sw.ElapsedTicks);

            _sidPump?.PumpFrame();

            lock (_visibleLock) _video.FrameBuffer.AsSpan().CopyTo(_visibleBuffer);
            Interlocked.Increment(ref _framesCompleted);
            frameIndex++;

            nextDue += periodTicks;
            long now = Stopwatch.GetTimestamp();
            long deltaTicks = nextDue - now;
            if (deltaTicks > 0)
            {
                long deltaMs = deltaTicks * 1000 / Stopwatch.Frequency;
                if (deltaMs > 2) Thread.Sleep((int)(deltaMs - 1));
                while (Stopwatch.GetTimestamp() < nextDue && !ct.IsCancellationRequested) Thread.SpinWait(50);
            }
            else
            {
                Interlocked.Increment(ref _lateFrames);
                nextDue = now;
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        if (_thread.IsAlive) _thread.Join(TimeSpan.FromSeconds(2));
        _cts.Dispose();
    }

    private readonly record struct KeyEvent(byte MatrixCode, bool Pressed);

    /// <summary>
    /// Internal disposable lease for safe framebuffer access from the pump's visible buffer.
    /// Guarantees release of the underlying synchronization primitive when disposed (even on exceptions).
    /// Visibility internal per review feedback. Implemented as ref struct for zero-alloc.
    /// </summary>
    internal readonly ref struct FrameLease : IDisposable
    {
        private readonly EmulatorPump _owner;

        public ReadOnlySpan<byte> Span { get; }

        internal FrameLease(EmulatorPump owner, ReadOnlySpan<byte> span)
        {
            _owner = owner;
            Span = span;
        }

        public void Dispose()
        {
            _owner?.ReleaseFromLease();
        }
    }

    // Acquire now enters the lock and returns owning lease (real impl after BDP stub validation).
    // Internal per review feedback (Lease visibility: Internal).
    internal FrameLease AcquireFrameLease()
    {
        Monitor.Enter(_visibleLock);
        return new FrameLease(this, _visibleBuffer);
    }

    private void ReleaseFromLease()
    {
        Monitor.Exit(_visibleLock);
    }
}
