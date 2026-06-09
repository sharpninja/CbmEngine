using CbmEngine.Abstractions;

namespace CbmEngine.Tests.Shared.Helpers;

public sealed class FakeClock : IClockSource
{
    public TimeSpan Now { get; private set; } = TimeSpan.Zero;
    public int TickCount { get; private set; }

    public void Tick(TimeSpan duration)
    {
        Now += duration;
        TickCount++;
    }
}
