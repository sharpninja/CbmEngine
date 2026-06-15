using CbmEngine.Abstractions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ViceSharp.Abstractions;

namespace CbmEngine.Host.MonoGame;

/// <summary>
/// Reusable, <see cref="Game"/>-agnostic embeddable emulator viewport. Composes the threaded
/// emulator pump, the BGRA-&gt;RGBA blit target, the keyboard input bridge, and the SID audio
/// backend from an <see cref="IMachine"/> and a <see cref="GraphicsDevice"/>.
/// <para>
/// It deliberately does NOT derive from or reference MonoGame <see cref="Game"/>, so an arbitrary
/// MonoGame <c>Game</c> subclass (for example a host that also drives a Myra UI and therefore cannot
/// use <see cref="MonoGameHost"/>) can embed a live emulated framebuffer by calling
/// <see cref="Update(GameTime)"/>/<see cref="Tick"/> from its own <c>Update</c> and
/// <see cref="Draw(SpriteBatch, Rectangle)"/> from its own <c>Draw</c>, forwarding input via
/// <see cref="EnqueueKey(byte, bool)"/>, and disposing it on teardown.
/// </para>
/// </summary>
public sealed class CbmViewport : IDisposable
{
    private readonly IMachine _machine;
    private readonly IVideoChip _video;
    private readonly IKeyboardMatrix? _keyboard;
    private readonly IBlitTarget _blit;
    private readonly bool _ownsBlit;
    private readonly IInputScript _input;
    private readonly IGame? _game;
    private readonly IGameContext? _gameContext;
    private readonly IAudioBackend? _audioBackend;
    private readonly SidPump? _sidPump;
    private readonly EmulatorPump? _pump;
    private int _frameIndex;
    private bool _gameInitialized;

    /// <summary>Width of the emulated framebuffer, in pixels.</summary>
    public int FrameWidth { get; }

    /// <summary>Height of the emulated framebuffer, in pixels.</summary>
    public int FrameHeight { get; }

    /// <summary>
    /// Number of emulated frames completed. Reflects the threaded pump when hybrid pumping is in
    /// use, otherwise the count of <see cref="Tick"/> calls.
    /// </summary>
    public long FramesCompleted => _pump?.FramesCompleted ?? _frameIndex;

    /// <summary><c>true</c> when the viewport runs the emulator on a background pump thread.</summary>
    public bool UsesHybridPump => _pump is not null;

    /// <summary>
    /// The most recently uploaded frame texture, or <c>null</c> before the first
    /// <see cref="RefreshTexture"/>/<see cref="Draw(SpriteBatch, Rectangle)"/> (or when a non-MonoGame
    /// blit target is in use). The consumer may draw this however it likes instead of using
    /// <see cref="Draw(SpriteBatch, Rectangle)"/>.
    /// </summary>
    public Texture2D? CurrentTexture => (_blit as MonoGameBlitTarget)?.Texture;

    /// <summary>
    /// Creates a viewport that renders the supplied machine into the supplied graphics device.
    /// </summary>
    /// <param name="machine">The emulated machine to drive and present.</param>
    /// <param name="graphicsDevice">The MonoGame graphics device used to allocate the frame texture.</param>
    /// <param name="refreshHz">Target emulator refresh rate (default PAL 50.125 Hz).</param>
    /// <param name="sampleRate">Audio sample rate, in Hz.</param>
    /// <param name="game">Optional game logic driven once per emulated frame.</param>
    /// <param name="gameContext">Required when <paramref name="game"/> is supplied.</param>
    /// <param name="useHybridPump">Run the emulator on a background pump thread (default) instead of stepping it inline on <see cref="Tick"/>.</param>
    /// <param name="enableAudio">Wire SID audio output to a MonoGame audio backend when the machine exposes an <see cref="IAudioChip"/>.</param>
    public CbmViewport(
        IMachine machine,
        GraphicsDevice graphicsDevice,
        double refreshHz = 50.125,
        int sampleRate = 44100,
        IGame? game = null,
        IGameContext? gameContext = null,
        bool useHybridPump = true,
        bool enableAudio = true)
        : this(
            machine,
            new MonoGameBlitTarget(graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice))),
            ownsBlit: true,
            new KeyboardBridge(),
            CreateAudioBackend(machine, enableAudio, sampleRate),
            refreshHz,
            sampleRate,
            game,
            gameContext,
            useHybridPump)
    {
    }

    /// <summary>
    /// Dependency-injection seam used by tests and non-MonoGame composition. Accepts the blit target,
    /// input source, and audio backend directly so the full pipeline can be exercised without a real
    /// <see cref="GraphicsDevice"/>.
    /// </summary>
    internal CbmViewport(
        IMachine machine,
        IBlitTarget blit,
        bool ownsBlit,
        IInputScript input,
        IAudioBackend? audioBackend,
        double refreshHz,
        int sampleRate,
        IGame? game,
        IGameContext? gameContext,
        bool useHybridPump)
    {
        _machine = machine ?? throw new ArgumentNullException(nameof(machine));
        _video = machine.Devices.GetByRole(DeviceRole.VideoChip) as IVideoChip
            ?? throw new InvalidOperationException("Machine has no IVideoChip.");
        _keyboard = machine.Devices.GetAll<IKeyboardMatrix>() is { Count: > 0 } kl ? kl[0] : null;
        _blit = blit ?? throw new ArgumentNullException(nameof(blit));
        _ownsBlit = ownsBlit;
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _game = game;
        _gameContext = gameContext;
        if (game is not null && gameContext is null)
            throw new ArgumentException("gameContext is required when game is supplied.", nameof(gameContext));

        _audioBackend = audioBackend;
        if (machine.Devices.GetByRole(DeviceRole.AudioChip) is IAudioChip audioChip && audioBackend is not null)
            _sidPump = new SidPump(audioChip, audioBackend, sampleRate, refreshHz);

        FrameWidth = _video.FrameWidth;
        FrameHeight = _video.FrameHeight;

        if (useHybridPump)
        {
            _pump = new EmulatorPump(machine, refreshHz, game, gameContext, _sidPump);
            _pump.Start();
            _gameInitialized = true; // EmulatorPump.Start performs game initialization.
        }
    }

    private static IAudioBackend? CreateAudioBackend(IMachine? machine, bool enableAudio, int sampleRate)
    {
        if (!enableAudio || machine is null)
            return null;
        return machine.Devices.GetByRole(DeviceRole.AudioChip) is IAudioChip
            ? new MonoGameAudioBackend(sampleRate)
            : null;
    }

    /// <summary>Advances the viewport by one frame. Call once per host <c>Update</c>.</summary>
    public void Update(GameTime gameTime) => Tick();

    /// <summary>
    /// Game-agnostic per-frame advance. In hybrid mode this only forwards drained input to the pump
    /// thread; otherwise it drains input into the C64 matrix, runs the game logic, steps the machine
    /// one frame, and pumps SID audio.
    /// </summary>
    public void Tick()
    {
        if (!_gameInitialized && _game is not null && _gameContext is not null)
        {
            _game.Initialize(_gameContext);
            _gameInitialized = true;
        }

        if (_pump is not null)
        {
            foreach (var ev in _input.DrainForFrame(_frameIndex))
                _pump.EnqueueKey(ev.MatrixCode, ev.Pressed);
            _frameIndex++;
            return;
        }

        if (_keyboard is not null)
        {
            foreach (var ev in _input.DrainForFrame(_frameIndex))
                _keyboard.SetKey(ev.MatrixCode, ev.Pressed);
        }

        if (_game is not null && _gameContext is not null)
            _game.Update(_gameContext, _frameIndex);

        _machine.RunFrame();
        _sidPump?.PumpFrame();
        _frameIndex++;
    }

    /// <summary>
    /// Uploads the latest emulated frame into <see cref="CurrentTexture"/>. Call once per host
    /// <c>Draw</c> (or rely on <see cref="Draw(SpriteBatch, Rectangle)"/>, which calls this for you).
    /// </summary>
    public void RefreshTexture()
    {
        if (_pump is not null)
        {
            try
            {
                var frame = _pump.AcquireFrameForUpload();
                _blit.Upload(frame, _pump.FrameWidth, _pump.FrameHeight);
            }
            finally
            {
                _pump.ReleaseFrame();
            }
        }
        else
        {
            _blit.Upload(_video.FrameBuffer, _video.FrameWidth, _video.FrameHeight);
        }
    }

    /// <summary>
    /// Refreshes the frame texture and draws it into <paramref name="destination"/> using the
    /// caller's <paramref name="spriteBatch"/>. The caller owns the <c>Begin</c>/<c>End</c> pair, so
    /// the viewport composes cleanly inside a host that also draws its own UI (for example Myra).
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Rectangle destination)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        RefreshTexture();
        if (CurrentTexture is { } texture)
            spriteBatch.Draw(texture, destination, Color.White);
    }

    /// <summary>
    /// Forwards a raw C64 key matrix event into the machine, letting an external host inject input it
    /// has gathered itself (in addition to the viewport's built-in keyboard bridge).
    /// </summary>
    public void EnqueueKey(byte matrixCode, bool pressed)
    {
        if (_pump is not null)
            _pump.EnqueueKey(matrixCode, pressed);
        else
            _keyboard?.SetKey(matrixCode, pressed);
    }

    /// <summary>Stops the pump thread and disposes owned MonoGame resources.</summary>
    public void Dispose()
    {
        _pump?.Dispose();
        (_audioBackend as IDisposable)?.Dispose();
        if (_ownsBlit)
            (_blit as IDisposable)?.Dispose();
    }
}
