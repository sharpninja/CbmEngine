using CbmEngine.Abstractions;

namespace CbmEngine.Systems.Sound;

public sealed class Sid6581Strategy : SidStrategyBase
{
    public Sid6581Strategy(IMemoryService memory, long clockHz) : base(memory, clockHz) { }
    public override SidModel Model => SidModel.Mos6581;
}
