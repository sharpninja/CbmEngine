using CbmEngine.Systems.Boot;
using Moq;
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
        var roms = new Mock<IRomProvider>(MockBehavior.Strict);
        roms.Setup(r => r.IsAvailable(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            BootRunner.Run(C64MachineProfiles.C64Pal, roms.Object, framesToWarm: 0));

        Assert.Contains("Commodore 64 PAL", ex.Message);
        Assert.Contains("ROM", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Run_NullProfile_Throws()
    {
        var roms = new Mock<IRomProvider>().Object;
        Assert.Throws<ArgumentNullException>(() => BootRunner.Run(null!, roms, 0));
    }

    [Fact]
    public void Run_NullRomProvider_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => BootRunner.Run(C64MachineProfiles.C64Pal, null!, 0));
    }
}
