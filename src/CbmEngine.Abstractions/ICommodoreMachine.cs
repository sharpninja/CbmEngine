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

    /// <summary>
    /// Pub/sub bus carrying per-scanline <c>RasterLineEvent</c> notifications from the VIC-II, for
    /// host-driven raster splits. Connected to the video chip when the machine is assembled.
    /// </summary>
    IPubSub PubSub { get; }

    void RunFrame();
}
