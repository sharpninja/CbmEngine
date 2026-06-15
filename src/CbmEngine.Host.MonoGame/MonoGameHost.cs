using CbmEngine.Abstractions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ViceSharp.Abstractions;

namespace CbmEngine.Host.MonoGame;

/// <summary>
/// MonoGame <see cref="Game"/> host that owns the window, the demo run, and the FPS overlay, and
/// delegates all emulator composition (pump + blit + input + audio) to a single
/// <see cref="CbmViewport"/>. There is no parallel pump/blit/input/audio wiring here.
/// </summary>
public sealed class MonoGameHost : Game
{
    private readonly GraphicsDeviceManager _gdm;
    private readonly IMachine _machine;
    private readonly IVideoChip _video;
    private readonly IGame? _game;
    private readonly IGameContext? _gameContext;
    private readonly double _refreshHz;
    private readonly int _sampleRate;
    private readonly bool _useHybridPump;
    private CbmViewport? _viewport;
    private SpriteBatch? _spriteBatch;
    private FpsOverlay? _fps;

    public MonoGameHost(IMachine machine, double refreshHz = 50.125, int sampleRate = 44100, int windowScale = 2, IGame? game = null, IGameContext? gameContext = null, bool useHybridPump = true)
    {
        _machine = machine ?? throw new ArgumentNullException(nameof(machine));
        _video = machine.Devices.GetByRole(DeviceRole.VideoChip) as IVideoChip
            ?? throw new InvalidOperationException("Machine has no IVideoChip.");
        _game = game;
        _gameContext = gameContext;
        if (game is not null && gameContext is null)
            throw new ArgumentException("gameContext is required when game is supplied.", nameof(gameContext));
        _refreshHz = refreshHz;
        _sampleRate = sampleRate;
        _useHybridPump = useHybridPump;

        _gdm = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = _video.FrameWidth * windowScale,
            PreferredBackBufferHeight = _video.FrameHeight * windowScale,
            SynchronizeWithVerticalRetrace = true,
        };
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / refreshHz);
        IsMouseVisible = true;
        Window.Title = $"CbmEngine - Press Esc to exit{(useHybridPump ? "  (hybrid)" : "")}";
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _viewport = new CbmViewport(_machine, GraphicsDevice, _refreshHz, _sampleRate, _game, _gameContext, _useHybridPump);
        _fps = new FpsOverlay(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        if (Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
        {
            Exit();
            return;
        }

        _viewport?.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _fps?.RegisterFrame();
        GraphicsDevice.Clear(Color.Black);
        if (_viewport is not null && _spriteBatch is not null)
        {
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _viewport.Draw(_spriteBatch, new Rectangle(0, 0, _gdm.PreferredBackBufferWidth, _gdm.PreferredBackBufferHeight));
            _fps?.Draw(_spriteBatch);
            _spriteBatch.End();
        }
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _viewport?.Dispose();
        _spriteBatch?.Dispose();
        _fps?.Dispose();
        base.UnloadContent();
    }
}
