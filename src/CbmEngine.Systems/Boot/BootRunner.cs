using ViceSharp.Abstractions;
using ViceSharp.Architectures.C64;
using ViceSharp.Core;

namespace CbmEngine.Systems.Boot;

public readonly record struct BootResult(IMachine Machine, byte[] FrameBuffer, int Width, int Height);

public static class BootRunner
{
    public static BootResult Run(C64MachineProfile profile, IRomProvider roms, int framesToWarm)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(roms);
        if (framesToWarm < 0) throw new ArgumentOutOfRangeException(nameof(framesToWarm));

        var descriptor = new C64Descriptor(profile);
        var builder = new ArchitectureBuilder(roms);
        var machine = builder.Build(descriptor);

        for (int i = 0; i < framesToWarm; i++)
            machine.RunFrame();

        if (machine.Devices.GetByRole(DeviceRole.VideoChip) is not IVideoChip video)
            throw new InvalidOperationException("Built machine does not expose an IVideoChip via DeviceRole.VideoChip.");

        var snapshot = video.FrameBuffer.AsSpan().ToArray();
        return new BootResult(machine, snapshot, video.FrameWidth, video.FrameHeight);
    }
}
