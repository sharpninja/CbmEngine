namespace CbmEngine.Abstractions;

public readonly record struct InputEvent(byte MatrixCode, bool Pressed);

public interface IInputScript
{
    IReadOnlyList<InputEvent> DrainForFrame(int frameIndex);
}
