namespace CbmEngine.Abstractions;

public interface IClockSource
{
    TimeSpan Now { get; }
    void Tick(TimeSpan duration);
}
