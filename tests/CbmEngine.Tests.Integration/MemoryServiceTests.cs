using System.Diagnostics;
using CbmEngine.Abstractions;
using CbmEngine.Systems.Strategy;
using CbmEngine.Tests.Integration.Helpers;
using Xunit;

namespace CbmEngine.Tests.Integration;

[Trait("Speed", "Slow")]
public class MemoryServiceTests
{
    private static ICommodoreMachine BuildC64()
    {
        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        for (int i = 0; i < 120; i++) sys.RunFrame();
        return sys;
    }

    [Fact]
    public void TEST_CBM_MEM_001_Snapshot_ReturnsLiveRamContents()
    {
        var sys = BuildC64();
        var snap = sys.Memory.Snapshot();
        Assert.Equal(65536, snap.Length);
        for (int addr = 0x0400; addr < 0x0420; addr++)
            Assert.Equal(sys.Bus.Read((ushort)addr), snap[addr]);
    }

    [Fact]
    public void TEST_CBM_MEM_002_View_RamRange_IsZeroCopyMutable()
    {
        var sys = BuildC64();
        var view = sys.Memory.View(0x0400, 8);
        view[0] = 0xAB;
        Assert.Equal(0xAB, sys.Bus.Read(0x0400));
    }

    [Fact]
    public void TEST_CBM_MEM_003_ViewMany_ReturnsAllRequestedRanges()
    {
        var sys = BuildC64();
        var handles = sys.Memory.ViewMany(new[] { ((ushort)0x0400, 1000), ((ushort)0x2000, 1000) });
        Assert.Equal(2, handles.Count);
        Assert.Equal(0x0400, handles[0].Address);
        Assert.Equal(0x2000, handles[1].Address);
        var span0 = sys.Memory.Materialize(handles[0]);
        var span1 = sys.Memory.Materialize(handles[1]);
        Assert.Equal(1000, span0.Length);
        Assert.Equal(1000, span1.Length);
        span0[0] = 0xC1; span1[0] = 0xC2;
        Assert.Equal(0xC1, sys.Bus.Read(0x0400));
        Assert.Equal(0xC2, sys.Bus.Read(0x2000));
    }

    [Fact]
    public void TEST_CBM_MEM_004_WriteRange_CopiesBytesAndIsObservableViaBus()
    {
        var sys = BuildC64();
        sys.Memory.WriteRange(0x2000, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
        Assert.Equal(0xDE, sys.Bus.Read(0x2000));
        Assert.Equal(0xAD, sys.Bus.Read(0x2001));
        Assert.Equal(0xBE, sys.Bus.Read(0x2002));
        Assert.Equal(0xEF, sys.Bus.Read(0x2003));
    }

    [Fact]
    public void TEST_CBM_MEM_005_View_OverIoRange_ThrowsWithAddress()
    {
        var sys = BuildC64();
        var ex = Assert.Throws<InvalidOperationException>(() => sys.Memory.View(0xD000, 64));
        Assert.Contains("IO range", ex.Message);
        Assert.Contains("D000", ex.Message);
    }

    [Fact]
    public void TEST_CBM_MEM_006_WriteIo_TriggersSideEffectsOnBus()
    {
        var sys = BuildC64();
        sys.Memory.WriteIo(0xD020, new byte[] { 0x06 });
        Assert.Equal(0x06, sys.Bus.Read(0xD020) & 0x0F);
    }

    [Fact]
    public void TEST_CBM_MEM_008_WriteRange_32K_BenchmarkUnder100Microseconds()
    {
        var sys = BuildC64();
        const int chunk = 32 * 1024;
        var src = new byte[chunk];
        for (int i = 0; i < src.Length; i++) src[i] = (byte)i;

        for (int warm = 0; warm < 10; warm++) sys.Memory.WriteRange(0x0000, src);

        long[] timingsTicks = new long[200];
        for (int run = 0; run < timingsTicks.Length; run++)
        {
            var sw = Stopwatch.StartNew();
            sys.Memory.WriteRange(0x0000, src);
            sw.Stop();
            timingsTicks[run] = sw.ElapsedTicks;
        }
        Array.Sort(timingsTicks);
        var medianMicros = timingsTicks[timingsTicks.Length / 2] * 1_000_000.0 / Stopwatch.Frequency;
        Assert.True(medianMicros < 100, $"Median WriteRange 32K = {medianMicros:F1}us; expected <100us.");
    }
}
