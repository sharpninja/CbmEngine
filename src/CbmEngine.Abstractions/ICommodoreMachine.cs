using ViceSharp.Abstractions;

namespace CbmEngine.Abstractions;

public interface ICommodoreMachine
{
    IMachine Underlying { get; }
    MachineCapabilities Capabilities { get; }
    IVideoChip VideoChip { get; }
    IAudioChip? AudioChip { get; }
    IKeyboardMatrix? KeyboardMatrix { get; }
    IBus Bus { get; }
    IClock Clock { get; }
    IMemoryService Memory { get; }
    ISoundChipStrategy Sound { get; }
    void RunFrame();
}
