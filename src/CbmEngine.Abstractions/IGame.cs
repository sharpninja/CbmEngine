namespace CbmEngine.Abstractions;

public interface IGame
{
    void Initialize(IGameContext context);
    void Update(IGameContext context, int frameIndex);
    void Draw(IGameContext context, int frameIndex);
}

public interface IGameContext
{
    ICommodoreMachine Machine { get; }
}
