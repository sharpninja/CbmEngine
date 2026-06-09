using CbmEngine.Abstractions;

namespace CbmEngine.Tests.Shared.Helpers;

public sealed class FakeInputScript : IInputScript
{
    private static readonly IReadOnlyList<InputEvent> Empty = Array.Empty<InputEvent>();
    private readonly Dictionary<int, List<InputEvent>> _byFrame = new();
    public int DrainCallCount { get; private set; }

    public FakeInputScript Press(int frame, byte matrixCode) => Add(frame, matrixCode, true);
    public FakeInputScript Release(int frame, byte matrixCode) => Add(frame, matrixCode, false);

    private FakeInputScript Add(int frame, byte matrixCode, bool pressed)
    {
        if (!_byFrame.TryGetValue(frame, out var list))
            _byFrame[frame] = list = new List<InputEvent>();
        list.Add(new InputEvent(matrixCode, pressed));
        return this;
    }

    public IReadOnlyList<InputEvent> DrainForFrame(int frameIndex)
    {
        DrainCallCount++;
        return _byFrame.TryGetValue(frameIndex, out var list) ? list : Empty;
    }
}
