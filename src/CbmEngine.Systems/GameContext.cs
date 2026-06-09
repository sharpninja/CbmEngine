using CbmEngine.Abstractions;
using CbmEngine.Systems.Services;

namespace CbmEngine.Systems;

public sealed class GameContext : IGameContext
{
    public ICommodoreMachine Machine { get; }
    public SpriteService Sprites { get; }
    public TilemapService Tilemap { get; }
    public MusicService Music { get; }

    public GameContext(ICommodoreMachine machine)
    {
        Machine = machine ?? throw new ArgumentNullException(nameof(machine));
        Sprites = new SpriteService(machine.Memory);
        Tilemap = new TilemapService(machine.Memory);
        Music = new MusicService(machine);
    }
}
