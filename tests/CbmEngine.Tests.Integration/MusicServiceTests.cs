using CbmEngine.Systems;
using CbmEngine.Systems.Audio;
using CbmEngine.Systems.Strategy;
using CbmEngine.Tests.Integration.Helpers;
using CbmEngine.Tests.Shared.Helpers;
using Xunit;

namespace CbmEngine.Tests.Integration;

[Trait("Speed", "Slow")]
public class MusicServiceTests
{
    private static GameContext BuildContext()
    {
        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        for (int i = 0; i < 120; i++) sys.RunFrame();
        return new GameContext(sys);
    }

    [Fact]
    public void TEST_CBM_PSID_004_Install_PlacesPayloadAtLoadAddress()
    {
        var ctx = BuildContext();
        using var ms = new MemoryStream(PsidFixtures.BuildSyntheticPsid());
        var prog = PsidLoader.Load(ms);

        ctx.Music.Install(prog, song: 1);

        for (int i = 0; i < prog.Payload.Length; i++)
            Assert.Equal(prog.Payload.Span[i], ctx.Machine.Bus.Read((ushort)(prog.Header.LoadAddress + i)));
    }

    [Fact]
    public void TEST_CBM_PSID_005_Install_RunsInitToCompletion()
    {
        var ctx = BuildContext();
        using var ms = new MemoryStream(PsidFixtures.BuildSyntheticPsid());
        var prog = PsidLoader.Load(ms);

        ctx.Music.Install(prog, song: 1);

        Assert.True(ctx.Music.IsPlaying);
        Assert.Equal(1, ctx.Music.CurrentSong);
    }

    [Fact]
    public void TEST_CBM_PSID_006_Tick_WritesSidRegistersAtPalRate()
    {
        var ctx = BuildContext();
        using var ms = new MemoryStream(PsidFixtures.BuildSyntheticPsid());
        var prog = PsidLoader.Load(ms);
        ctx.Music.Install(prog);

        byte initialD418 = ctx.Machine.Bus.Read(0xD418);
        for (int i = 0; i < 50; i++) ctx.Music.Tick();
        byte finalD418 = ctx.Machine.Bus.Read(0xD418);

        Assert.Equal(0x0F, finalD418 & 0x0F);
    }

    [Fact]
    public void TEST_CBM_PSID_008_Stop_SilencesAudio()
    {
        var ctx = BuildContext();
        using var ms = new MemoryStream(PsidFixtures.BuildSyntheticPsid());
        var prog = PsidLoader.Load(ms);
        ctx.Music.Install(prog);
        for (int i = 0; i < 5; i++) ctx.Music.Tick();

        ctx.Music.Stop();

        Assert.False(ctx.Music.IsPlaying);
        Assert.Equal(0, ctx.Machine.Bus.Read(0xD418) & 0x0F);
    }

    [Fact]
    public void TEST_CBM_PSID_009_Install_RejectsZeroPageOverlap()
    {
        var ctx = BuildContext();
        using var ms = new MemoryStream(PsidFixtures.BuildZeroPageOverlapPsid());
        var prog = PsidLoader.Load(ms);

        var ex = Assert.Throws<PsidPlacementException>(() => ctx.Music.Install(prog));
        Assert.Contains("zero page", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TEST_CBM_PSID_010_Install_RunInitOverBudget_Throws()
    {
        var ctx = BuildContext();
        using var ms = new MemoryStream(PsidFixtures.BuildInfiniteInitPsid());
        var prog = PsidLoader.Load(ms);

        var ex = Assert.Throws<PsidExecutionException>(() => ctx.Music.Install(prog));
        Assert.Contains("budget", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
