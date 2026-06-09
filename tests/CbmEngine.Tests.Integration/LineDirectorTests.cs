using System.Security.Cryptography;
using CbmEngine.Abstractions;
using CbmEngine.Systems.LineDirector;
using CbmEngine.Systems.Strategy;
using CbmEngine.Tests.Integration.Helpers;
using Xunit;

namespace CbmEngine.Tests.Integration;

[Trait("Speed", "Slow")]
public class LineDirectorTests
{
    private static ICommodoreMachine BuildC64(int warmupFrames = 60)
    {
        var sys = CommodoreSystem.Build("c64", TestRomProvider.Create());
        for (int i = 0; i < warmupFrames; i++) sys.RunFrame();
        return sys;
    }

    [Fact]
    public void TEST_CBM_LD_001_StepLine_AdvancesCurrentLineThroughAllLinesOfFrame()
    {
        var sys = BuildC64();
        var director = new LineDirector(sys, new LineProgram.Builder().Build());

        var observed = new HashSet<int>();
        for (int i = 0; i < director.TotalLines; i++)
        {
            observed.Add(director.CurrentLine);
            director.StepLine();
        }
        Assert.Equal(director.TotalLines, observed.Count);
        for (int i = 0; i < director.TotalLines; i++) Assert.Contains(i, observed);
    }

    [Fact]
    public void TEST_CBM_LD_005_IdenticalProgram_ProducesByteIdenticalFramebuffer()
    {
        var program = new LineProgram.Builder()
            .At(60, 0xD020, 0x06)
            .At(120, 0xD020, 0x0E)
            .At(180, 0xD020, 0x02)
            .Build();

        var sysA = BuildC64();
        var sysB = BuildC64();

        new LineDirector(sysA, program).RunFrame();
        new LineDirector(sysB, program).RunFrame();

        string hashA = Convert.ToHexString(SHA256.HashData(sysA.VideoChip.FrameBuffer));
        string hashB = Convert.ToHexString(SHA256.HashData(sysB.VideoChip.FrameBuffer));
        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public void D020_PerLineProgram_ChangesBorderColorObservably()
    {
        var sys = BuildC64();
        var beforeHash = Convert.ToHexString(SHA256.HashData(sys.VideoChip.FrameBuffer));

        var program = new LineProgram.Builder()
            .At(60, 0xD020, 0x02)
            .At(80, 0xD020, 0x05)
            .At(100, 0xD020, 0x06)
            .At(120, 0xD020, 0x07)
            .Build();
        new LineDirector(sys, program).RunFrame();

        var afterHash = Convert.ToHexString(SHA256.HashData(sys.VideoChip.FrameBuffer));
        Assert.NotEqual(beforeHash, afterHash);
    }

    [Fact]
    public void EmptyProgram_RunFrame_StillRendersValidFramebuffer()
    {
        var sys = BuildC64();
        var director = new LineDirector(sys, new LineProgram.Builder().Build());
        director.RunFrame();
        Assert.True(sys.VideoChip.FrameBuffer.Length > 0);
        Assert.Equal(sys.VideoChip.FrameWidth * sys.VideoChip.FrameHeight * 4, sys.VideoChip.FrameBuffer.Length);
    }

    [Fact]
    public void LineDirector_NullArgs_Throw()
    {
        var sys = BuildC64();
        Assert.Throws<ArgumentNullException>(() => new LineDirector(null!, new LineProgram.Builder().Build()));
        Assert.Throws<ArgumentNullException>(() => new LineDirector(sys, null!));
    }
}
