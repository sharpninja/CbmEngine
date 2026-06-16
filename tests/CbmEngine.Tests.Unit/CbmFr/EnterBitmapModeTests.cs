using System.Linq;
using CbmEngine.Systems.LineDirector;
using CbmEngine.Systems.Video;
using Xunit;

namespace CbmEngine.Tests.Unit.CbmFr;

// FR-CBM-BITMAPMODE-001 (CBMFR-002): host-driven bitmap mode without CC65.
[Trait("Speed", "Fast")]
public class EnterBitmapModeTests
{
    [Fact]
    public void TEST_CBM_028_EnterBitmapMode_AssertsRegistersImmediately()
    {
        var m = new FakeMachine();
        m.EnterBitmapMode(1, 0x6000, 0x4400, multicolor: true);
        Assert.Equal(0x3B, m.Memory.ReadIo(0xD011));
        Assert.Equal(0xD8, m.Memory.ReadIo(0xD016));
        Assert.Equal(0x18, m.Memory.ReadIo(0xD018));
        Assert.Equal(2, m.Memory.ReadIo(0xDD00) & 0x03);
    }

    [Fact]
    public void TEST_CBM_029_SteadyState_ContainsRegisterWritesAtRasterLine()
    {
        var prog = new FakeMachine().EnterBitmapMode(1, 0x6000, 0x4400, multicolor: true, assertRasterLine: 48);
        Assert.True(prog.SteadyState.TryGet(48, out var writes));
        Assert.Contains(writes, w => w.Address == 0xD011 && w.Value == 0x3B);
        Assert.Contains(writes, w => w.Address == 0xD016 && w.Value == 0xD8);
        Assert.Contains(writes, w => w.Address == 0xD018 && w.Value == 0x18);
        Assert.Contains(writes, w => w.Address == 0xDD00 && (w.Value & 0x03) == 2);
    }

    [Fact]
    public void TEST_CBM_030_SteadyState_ReAssertsAfterClobber()
    {
        var m = new FakeMachine();
        var prog = m.EnterBitmapMode(1, 0x6000, 0x4400, multicolor: true, assertRasterLine: 0);
        m.Bus.Write(0xD011, 0x1B);                    // simulate BASIC/KERNAL clobber
        Assert.Equal(0x1B, m.Memory.ReadIo(0xD011));
        new LineDirector(m, prog.SteadyState).RunFrame();
        Assert.Equal(0x3B, m.Memory.ReadIo(0xD011));  // re-asserted
    }

    [Fact]
    public void TEST_CBM_031_MulticolorFlag_SelectsD016()
    {
        Assert.Equal(0xD8, new FakeMachine().EnterBitmapMode(1, 0x6000, 0x4400, multicolor: true).Registers.D016);
        Assert.Equal(0xC8, new FakeMachine().EnterBitmapMode(1, 0x6000, 0x4400, multicolor: false).Registers.D016);
    }

    [Fact]
    public void TEST_CBM_032_SteadyState_AssertsVicBank()
    {
        var bank0 = new FakeMachine().EnterBitmapMode(0, 0x2000, 0x0400, multicolor: true, assertRasterLine: 0);
        Assert.True(bank0.SteadyState.TryGet(0, out var w0));
        Assert.Equal(3, w0.First(x => x.Address == 0xDD00).Value & 0x03);   // bank 0 => %11

        var bank1 = new FakeMachine().EnterBitmapMode(1, 0x6000, 0x4400, multicolor: true, assertRasterLine: 0);
        Assert.True(bank1.SteadyState.TryGet(0, out var w1));
        Assert.Equal(2, w1.First(x => x.Address == 0xDD00).Value & 0x03);   // bank 1 => %10
    }

    [Fact]
    public void TEST_CBM_033_InvalidArgs_Throw()
    {
        var m = new FakeMachine();
        Assert.Throws<ArgumentException>(() => m.EnterBitmapMode(4, 0x6000, 0x4400, multicolor: true));
        Assert.Throws<ArgumentException>(() => m.EnterBitmapMode(1, 0x5000, 0x4400, multicolor: true));
    }

    [Fact]
    public void TEST_CBM_034_HostDriven_NoCc65()
    {
        // Purely in-memory: succeeds on a fake machine with no toolchain or filesystem, producing a
        // host-driven steady-state LineProgram instead of building a CC65 cartridge.
        var prog = new FakeMachine().EnterBitmapMode(1, 0x6000, 0x4400, multicolor: true);
        Assert.True(prog.SteadyState.Count > 0);
        Assert.Equal(0x6000, prog.BitmapBase);
        Assert.Equal(0x4400, prog.ScreenBase);
    }
}
