using CbmEngine.Abstractions;

namespace CbmEngine.Systems.Sound;

public sealed class Sid8580Strategy : SidStrategyBase
{
    public Sid8580Strategy(IMemoryService memory, long clockHz) : base(memory, clockHz) { }
    public override SidModel Model => SidModel.Mos8580;
}
