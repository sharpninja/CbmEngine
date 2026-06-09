using CbmEngine.Abstractions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ViceSharp.Abstractions;

namespace CbmEngine.Host.MonoGame;

public sealed class MonoGameHost : Game
{
    private readonly GraphicsDeviceManager _gdm;
    private readonly IMachine _machine;
    private readonly IVideoChip _video;
    private readonly IKeyboardMatrix? _keyboard;
    private readonly IAudioChip? _audio;
    private readonly IGame? _game;
    private readonly IGameContext? _gameContext;
    private readonly double _refreshHz;
    private readonly int _sampleRate;
    private MonoGameBlitTarget? _blit;
    private MonoGameAudioBackend? _audioBackend;
    private SidPump? _pump;
    private KeyboardBridge? _keys;
    private SpriteBatch? _spriteBatch;
    private FpsOverlay? _fps;
    private int _frameIndex;
    private bool _gameInitialized;

    public MonoGameHost(IMachine machine, double refreshHz = 50.125, int sampleRate = 44100, int windowScale = 2, IGame? game = null, IGameContext? gameContext = null)
    {
        _machine = machine ?? throw new ArgumentNullException(nameof(machine));
        _video = machine.Devices.GetByRole(DeviceRole.VideoChip) as IVideoChip
            ?? throw new InvalidOperationException("Machine has no IVideoChip.");
        _keyboard = machine.Devices.GetAll<IKeyboardMatrix>() is { Count: > 0 } kl ? kl[0] : null;
        _audio = machine.Devices.GetByRole(DeviceRole.AudioChip) as IAudioChip;
        _game = game;
        _gameContext = gameContext;
        if (game is not null && gameContext is null)
            throw new ArgumentException("gameContext is required when game is supplied.", nameof(gameContext));
        _refreshHz = refreshHz;
        _sampleRate = sampleRate;

        _gdm = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = _video.FrameWidth * windowScale,
            PreferredBackBufferHeight = _video.FrameHeight * windowScale,
            SynchronizeWithVerticalRetrace = true,
        };
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / refreshHz);
        IsMouseVisible = true;
        Window.Title = "CbmEngine - Press Esc to exit";
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _blit = new MonoGameBlitTarget(GraphicsDevice);
        if (_audio is not null)
        {
            _audioBackend = new MonoGameAudioBackend(_sampleRate);
            _pump = new SidPump(_audio, _audioBackend, _sampleRate, _refreshHz);
        }
        _keys = new KeyboardBridge();
        _fps = new FpsOverlay(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        if (!_gameInitialized && _game is not null && _gameContext is not null)
        {
            _game.Initialize(_gameContext);
            _gameInitialized = true;
        }

        if (Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
        {
            Exit();
            return;
        }

        if (_keyboard is not null && _keys is not null)
        {
            foreach (var ev in _keys.DrainForFrame(_frameIndex))
                _keyboard.SetKey(ev.MatrixCode, ev.Pressed);
        }

        if (_game is not null && _gameContext is not null)
            _game.Update(_gameContext, _frameIndex);

        _machine.RunFrame();
        _pump?.PumpFrame();
        _frameIndex++;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _fps?.RegisterFrame();
        GraphicsDevice.Clear(Color.Black);
        _blit?.Upload(_video.FrameBuffer, _video.FrameWidth, _video.FrameHeight);
        if (_blit?.Texture is { } tex && _spriteBatch is not null)
        {
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _spriteBatch.Draw(tex, new Rectangle(0, 0, _gdm.PreferredBackBufferWidth, _gdm.PreferredBackBufferHeight), Color.White);
            _fps?.Draw(_spriteBatch);
            _spriteBatch.End();
        }
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _audioBackend?.Dispose();
        _blit?.Dispose();
        _spriteBatch?.Dispose();
        _fps?.Dispose();
        base.UnloadContent();
    }
}
