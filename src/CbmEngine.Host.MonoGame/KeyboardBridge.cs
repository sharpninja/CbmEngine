using CbmEngine.Abstractions;
using Microsoft.Xna.Framework.Input;

namespace CbmEngine.Host.MonoGame;

public sealed class KeyboardBridge : IInputScript
{
    private static readonly Dictionary<Keys, byte> Map = new()
    {
        [Keys.A] = 0x0A, [Keys.B] = 0x1C, [Keys.C] = 0x14, [Keys.D] = 0x12,
        [Keys.E] = 0x0E, [Keys.F] = 0x15, [Keys.G] = 0x1A, [Keys.H] = 0x1D,
        [Keys.I] = 0x21, [Keys.J] = 0x22, [Keys.K] = 0x25, [Keys.L] = 0x2A,
        [Keys.M] = 0x24, [Keys.N] = 0x27, [Keys.O] = 0x26, [Keys.P] = 0x29,
        [Keys.Q] = 0x3E, [Keys.R] = 0x11, [Keys.S] = 0x0D, [Keys.T] = 0x16,
        [Keys.U] = 0x1E, [Keys.V] = 0x1F, [Keys.W] = 0x09, [Keys.X] = 0x17,
        [Keys.Y] = 0x19, [Keys.Z] = 0x0C,
        [Keys.D0] = 0x23, [Keys.D1] = 0x38, [Keys.D2] = 0x3B, [Keys.D3] = 0x08,
        [Keys.D4] = 0x0B, [Keys.D5] = 0x10, [Keys.D6] = 0x13, [Keys.D7] = 0x18,
        [Keys.D8] = 0x1B, [Keys.D9] = 0x20,
        [Keys.Space] = 0x3C, [Keys.Enter] = 0x01, [Keys.Back] = 0x00,
        [Keys.Right] = 0x02, [Keys.Down] = 0x07,
        [Keys.LeftShift] = 0x0F, [Keys.RightShift] = 0x34,
        [Keys.LeftControl] = 0x3A, [Keys.RightControl] = 0x3A,
        [Keys.Escape] = 0x3F,
        [Keys.F1] = 0x04, [Keys.F3] = 0x05, [Keys.F5] = 0x06, [Keys.F7] = 0x03,
    };

    private readonly HashSet<Keys> _pressed = new();
    private readonly List<InputEvent> _frameEvents = new();

    public IReadOnlyList<InputEvent> DrainForFrame(int frameIndex)
    {
        _frameEvents.Clear();
        var state = Keyboard.GetState();

        foreach (var (key, code) in Map)
        {
            bool isDown = state.IsKeyDown(key);
            bool wasDown = _pressed.Contains(key);
            if (isDown && !wasDown)
            {
                _pressed.Add(key);
                _frameEvents.Add(new InputEvent(code, true));
            }
            else if (!isDown && wasDown)
            {
                _pressed.Remove(key);
                _frameEvents.Add(new InputEvent(code, false));
            }
        }

        return _frameEvents;
    }
}
