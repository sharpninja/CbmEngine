using CbmEngine.Host.MonoGame;
using CbmEngine.Systems.Boot;
using ViceSharp.Abstractions;
using ViceSharp.Architectures.C64;
using ViceSharp.RomFetch;
using Xunit;

namespace CbmEngine.Tests.Integration;

[Trait("Speed", "Slow")]
public class HostAudioTests
{
    private const int SampleRate = 44100;
    private const double PalRefresh = 50.125;

    [Fact]
    public void TEST_CBM_HOST_002_SidPokeProducesAudibleSamples()
    {
        var romBase = Path.Combine(BootSpikeTests.RepoRootPublic, "external", "vice-sharp", "native", "vice", "vice", "data");
        var roms = new RomProvider(romBase);
        var result = BootRunner.Run(C64MachineProfiles.C64Pal, roms, framesToWarm: 120);
        var machine = result.Machine;
        var sid = machine.Devices.GetByRole(DeviceRole.AudioChip) as IAudioChip;
        Assert.NotNull(sid);

        var bus = machine.Bus;
        bus.Write(0xD404, 0x00);
        bus.Write(0xD405, 0x09);
        bus.Write(0xD406, 0x00);
        bus.Write(0xD400, 0x25);
        bus.Write(0xD401, 0x11);
        bus.Write(0xD418, 0x0F);
        bus.Write(0xD404, 0x21);

        var backend = new global::CbmEngine.Tests.Shared.Helpers.RecordingAudioBackend();
        var pump = new SidPump(sid, backend, SampleRate, PalRefresh);

        const int framesFor1Second = 50;
        for (int i = 0; i < framesFor1Second; i++)
        {
            machine.RunFrame();
            pump.PumpFrame();
        }

        int audibleCount = 0;
        for (int i = 0; i < backend.Samples.Count; i++)
            if (Math.Abs(backend.Samples[i]) > 0.001f) audibleCount++;

        Assert.True(audibleCount >= 1000, $"Expected >=1000 audible samples; got {audibleCount} of {backend.Samples.Count}.");
    }
}
