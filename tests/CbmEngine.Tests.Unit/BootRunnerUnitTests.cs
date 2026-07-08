using CbmEngine.Systems.Boot;
using NSubstitute;
using ViceSharp.Abstractions;
using ViceSharp.Architectures.C64;
using Xunit;

namespace CbmEngine.Tests.Unit;

[Trait("Speed", "Fast")]
public class BootRunnerUnitTests
{
    [Fact]
    public void MissingRoms_ThrowsWithMachineNameInMessage()
    {
        var roms = Substitute.For<IRomProvider>();
        roms.IsAvailable(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            BootRunner.Run(C64MachineProfiles.C64Pal, roms, framesToWarm: 0));

        Assert.Contains("Commodore 64 PAL", ex.Message);
        Assert.Contains("ROM", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Run_NullProfile_Throws()
    {
        var roms = Substitute.For<IRomProvider>();
        Assert.Throws<ArgumentNullException>(() => BootRunner.Run(null!, roms, 0));
    }

    [Fact]
    public void Run_NullRomProvider_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => BootRunner.Run(C64MachineProfiles.C64Pal, null!, 0));
    }
}
