using CbmEngine.Abstractions;
using CbmEngine.Systems;

namespace CbmEngine.Game.Sample;

public sealed class DemoGame : IGame
{
    private int _frameCounter;

    public int FrameCounter => _frameCounter;
    public int LastBorderObserved { get; private set; }

    public void Initialize(IGameContext context) { }

    public void Update(IGameContext context, int frameIndex)
    {
        _frameCounter = frameIndex;
        LastBorderObserved = ((GameContext)context).Machine.Bus.Read(0xD020) & 0x0F;
    }

    public void Draw(IGameContext context, int frameIndex) { }
}
