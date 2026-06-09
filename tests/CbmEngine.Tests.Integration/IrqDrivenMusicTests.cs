using CbmEngine.Systems;
using CbmEngine.Systems.Audio;
using CbmEngine.Systems.Native;
using CbmEngine.Systems.Strategy;
using CbmEngine.Tests.Integration.Helpers;
using CbmEngine.Tests.Shared.Helpers;
using ViceSharp.Abstractions;
using Xunit;

namespace CbmEngine.Tests.Integration;

[Trait("Speed", "Slow")]
public class IrqDrivenMusicTests
{
    private static (GameContext Ctx, PsidProgram Program) BuildAndInstall()
    {
        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        for (int i = 0; i < 120; i++) sys.RunFrame();
        var ctx = new GameContext(sys);
        using var ms = new MemoryStream(PsidFixtures.BuildSyntheticPsid());
        var prog = PsidLoader.Load(ms);
        ctx.Music.Install(prog);
        return (ctx, prog);
    }

    [Fact]
    public void InstallNativeIrqDriver_WritesStubBytesToMemory()
    {
        var (ctx, _) = BuildAndInstall();
        ctx.Music.InstallNativeIrqDriver(stubAddress: 0xC000);

        Assert.True(ctx.Music.NativeIrqDriverInstalled);
        Assert.Equal((ushort)0xC000, ctx.Music.NativeIrqStubAddress);
        Assert.Equal(0x78, ctx.Machine.Bus.Read(0xC000));
        Assert.Equal(0xA9, ctx.Machine.Bus.Read(0xC010));
        Assert.Equal(0x20, ctx.Machine.Bus.Read(0xC015));
    }

    [Fact]
    public void InstallNativeIrqDriver_RedirectsIrqVectorTo0314_0315()
    {
        var (ctx, _) = BuildAndInstall();
        ctx.Music.InstallNativeIrqDriver(stubAddress: 0xC000);

        Assert.Equal(0x10, ctx.Machine.Bus.Read(0x0314));
        Assert.Equal(0xC0, ctx.Machine.Bus.Read(0x0315));
    }

    [Fact]
    public void Tick_IsNoOp_WhenNativeIrqDriverInstalled()
    {
        var (ctx, _) = BuildAndInstall();
        ctx.Music.InstallNativeIrqDriver();
        byte before = ctx.Machine.Bus.Read(0xD418);
        ctx.Machine.Memory.WriteIo(0xD418, new byte[] { 0x00 });
        ctx.Music.Tick();
        Assert.Equal(0x00, ctx.Machine.Bus.Read(0xD418));
    }

    [Fact]
    public void Handler_StubBytes_RunDirectly_AdvancePlay()
    {
        var (ctx, _) = BuildAndInstall();
        ctx.Music.InstallNativeIrqDriver();
        ctx.Machine.Memory.WriteIo(0xD418, new byte[] { 0x00 });

        var cpu = (ViceSharp.Chips.Cpu.Mos6502)ctx.Machine.Underlying.Devices.GetByRole(DeviceRole.Cpu)!;
        ushort handler = (ushort)(ctx.Music.NativeIrqStubAddress + IrqPlayerStub.HandlerOffset);
        ctx.Machine.Bus.Write((ushort)(0x0100 + cpu.S), 0xFF);
        cpu.S--;
        ctx.Machine.Bus.Write((ushort)(0x0100 + cpu.S), 0xFE);
        cpu.S--;
        cpu.PC = handler;

        int instructions = 0;
        while (cpu.PC != 0xEA31 && instructions < 200)
        {
            cpu.ExecuteInstruction();
            instructions++;
        }

        Assert.True(cpu.PC == 0xEA31,
            $"Expected handler to JMP $EA31 after JSR play returns; PC={cpu.PC:X4} after {instructions} instructions.");
        Assert.Equal(0x0F, ctx.Machine.Bus.Read(0xD418) & 0x0F);
    }

    [Fact]
    public void RunFrame_AfterIrqInstall_NaturalCiaIrqFiresHandlerWithin200Frames()
    {
        var (ctx, _) = BuildAndInstall();
        ctx.Music.InstallNativeIrqDriver();
        ctx.Machine.Memory.WriteIo(0xD418, new byte[] { 0x00 });

        bool fired = false;
        for (int i = 0; i < 200 && !fired; i++)
        {
            ctx.Machine.RunFrame();
            if ((ctx.Machine.Bus.Read(0xD418) & 0x0F) == 0x0F) fired = true;
        }

        Assert.True(fired,
            $"Expected CIA-driven IRQ to route through $0314 stub within 200 frames. $D418=${ctx.Machine.Bus.Read(0xD418):X2}.");
    }

    [Fact]
    public void Stop_RestoresKernalIrqVector_AndSilences()
    {
        var (ctx, _) = BuildAndInstall();
        ctx.Music.InstallNativeIrqDriver();
        ctx.Music.Stop();

        Assert.False(ctx.Music.NativeIrqDriverInstalled);
        Assert.Equal(0x31, ctx.Machine.Bus.Read(0x0314));
        Assert.Equal(0xEA, ctx.Machine.Bus.Read(0x0315));
        Assert.Equal(0, ctx.Machine.Bus.Read(0xD418) & 0x0F);
    }
}
