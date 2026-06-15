using CbmEngine.Abstractions;
using CbmEngine.Systems.Video;

namespace CbmEngine.Game.Sample;

public sealed class VideoGame : IGame
{
    private readonly VideoPlayer _player;

    public VideoGame(VideoPlayer player)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _player.Loop = true;
    }

    public void Initialize(IGameContext context) { }

    public void Update(IGameContext context, int frameIndex)
    {
        _player.PumpFrame(context.Machine.Memory);
    }

    public void Draw(IGameContext context, int frameIndex) { }
}
