using System.Collections.Immutable;
using CbmEngine.Abstractions;
using CbmEngine.Systems.Memory;
using CbmEngine.Systems.Sound;
using ViceSharp.Abstractions;
using ViceSharp.Architectures.C64;
using ViceSharp.Chips.VicIi;
using ViceSharp.Core;

namespace CbmEngine.Systems.Strategy;

public sealed class CommodoreMachine : ICommodoreMachine
{
    public IMachine Underlying { get; }
    public MachineCapabilities Capabilities { get; }
    public IVideoChip VideoChip { get; }
    public IAudioChip? AudioChip { get; }
    public IKeyboardMatrix? KeyboardMatrix { get; }
    public IBus Bus => Underlying.Bus;
    public IClock Clock => Underlying.Clock;
    public IMemoryService Memory { get; }
    public ISoundChipStrategy Sound { get; }
    public IPubSub PubSub { get; }

    public CommodoreMachine(IMachine machine, C64MachineProfile profile)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(profile);
        Underlying = machine;

        VideoChip = machine.Devices.GetByRole(DeviceRole.VideoChip) as IVideoChip
            ?? throw new InvalidOperationException("Built machine has no IVideoChip.");

        // Wire a pub/sub bus to the VIC-II so it emits per-scanline RasterLineEvents. This is the
        // plumbing for host-driven raster splits (e.g. char-mode bars around a multicolor bitmap).
        PubSub = new LockFreePubSub();
        if (VideoChip is Mos6569 vic)
        {
            vic.ConnectPubSub(PubSub);
        }
        AudioChip = machine.Devices.GetByRole(DeviceRole.AudioChip) as IAudioChip;
        KeyboardMatrix = machine.Devices.GetAll<IKeyboardMatrix>() is { Count: > 0 } list ? list[0] : null;

        var ram = machine.Devices.GetByRole(DeviceRole.SystemRam) as IMemory
            ?? throw new InvalidOperationException("Built machine has no SystemRam IMemory.");
        Memory = new MemoryService(ram, machine.Bus);

        var sidModel = MapSidModel(profile.Sid);
        Sound = sidModel == SidModel.Mos8580
            ? new Sid8580Strategy(Memory, profile.NominalClockHz)
            : new Sid6581Strategy(Memory, profile.NominalClockHz);

        Capabilities = new MachineCapabilities(
            ProfileId: profile.Id,
            DisplayName: profile.DisplayName,
            VideoStandard: profile.VideoStandard,
            CyclesPerLine: profile.CyclesPerLine,
            RasterLines: profile.RasterLines,
            NominalClockHz: profile.NominalClockHz,
            RefreshRateHz: profile.RefreshRateHz,
            SidModel: sidModel,
            BgraPalette: BuildBgraPalette());
    }

    public void RunFrame() => Underlying.RunFrame();

    private static SidModel MapSidModel(C64SidModel m) => m switch
    {
        C64SidModel.Mos8580 => SidModel.Mos8580,
        _ => SidModel.Mos6581,
    };

    private static ImmutableArray<uint> BuildBgraPalette()
    {
        var arr = new uint[16];
        for (int i = 0; i < 16; i++)
        {
            var c = VicPalette.Colors[i];
            arr[i] = 0xFF000000u | ((uint)c.B) | ((uint)c.G << 8) | ((uint)c.R << 16);
        }
        return ImmutableArray.Create(arr);
    }
}
