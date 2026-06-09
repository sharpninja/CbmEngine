using CbmEngine.Abstractions;
using CbmEngine.Systems.Audio;
using CbmEngine.Systems.Cartridge;
using CbmEngine.Systems.Strategy;
using CbmEngine.Tests.Integration.Helpers;
using ViceSharp.Abstractions;
using ViceSharp.Core;
using Xunit;
using Xunit.Abstractions;

namespace CbmEngine.Tests.Integration;

[Trait("Speed", "Slow")]
public class CartLockstepTests
{
    private readonly ITestOutputHelper _out;
    public CartLockstepTests(ITestOutputHelper output) { _out = output; }

    private const int CyclesPerPalFrame = 19656;

    [Fact]
    public void Lockstep_FrostPointCart_StrictCycleCompare_FindsFirstDivergence()
    {
        if (!ViceNative.IsAvailable) { _out.WriteLine($"Skipping: {ViceNative.AvailabilityMessage}"); return; }

        var psid = LoadFrostPointPsid();
        var cart = PsidPlayerCart.Build(psid, backgroundColor: 0x01);
        var (managed, native) = SetupBothWithCart(cart);
        try
        {
            const int totalCycles = 500_000;
            int firstMismatch = FindFirstCycleMismatch(managed, native, totalCycles, out var detail);
            if (firstMismatch < 0)
                _out.WriteLine($"STRICT CONVERGED for {totalCycles} cycles - bit-identical CPU state.");
            else
                _out.WriteLine($"STRICT DIVERGED at cycle {firstMismatch}: {detail}");

            Assert.True(firstMismatch < 0 || firstMismatch >= 100,
                $"Divergence too early ({firstMismatch}) to be plausible cart behavior - emulator instruction is suspect: {detail}");
        }
        finally { ViceNative.Destroy(native); }
    }

    [Fact]
    public void Lockstep_FrostPointCart_FunctionalAgreesAtFrameBoundaries()
    {
        if (!ViceNative.IsAvailable) { _out.WriteLine($"Skipping: {ViceNative.AvailabilityMessage}"); return; }

        var psid = LoadFrostPointPsid();
        var cart = PsidPlayerCart.Build(psid, backgroundColor: 0x01, initialBorderColor: 0x00, borderCyclePeriodFrames: 50);
        var (managed, native) = SetupBothWithCart(cart);
        try
        {
            const int frames = 120;
            for (int f = 0; f < frames; f++)
            {
                for (int c = 0; c < CyclesPerPalFrame; c++)
                {
                    managed.Clock.Step();
                    ViceNative.StepNative(native);
                }
            }

            byte mD021 = managed.Bus.Read(0xD021);
            byte nD021 = ViceNative.ReadMemory(native, 0xD021);
            byte mMarkerHi = managed.Bus.Read(0x0334);
            byte nMarkerHi = ViceNative.PeekRam(native, 0x0334);
            byte mMarkerLo = managed.Bus.Read(0x0335);
            byte nMarkerLo = ViceNative.PeekRam(native, 0x0335);
            byte m314 = managed.Bus.Read(0x0314);
            byte n314 = ViceNative.PeekRam(native, 0x0314);
            byte m315 = managed.Bus.Read(0x0315);
            byte n315 = ViceNative.PeekRam(native, 0x0315);
            byte mD020 = managed.Bus.Read(0xD020);
            byte nD020 = ViceNative.ReadMemory(native, 0xD020);
            byte mD418 = managed.Bus.Read(0xD418);
            byte nD418 = ViceNative.ReadMemory(native, 0xD418);

            _out.WriteLine($"After {frames} frames functional snapshot:");
            _out.WriteLine($"  $D021 (bg)       managed=${mD021:X2}  native=${nD021:X2}");
            _out.WriteLine($"  $D020 (border)   managed=${mD020:X2}  native=${nD020:X2}");
            _out.WriteLine($"  $0334/$0335 mark managed=${mMarkerHi:X2}${mMarkerLo:X2}  native=${nMarkerHi:X2}${nMarkerLo:X2}");
            _out.WriteLine($"  $0314/$0315 vec  managed=${m315:X2}{m314:X2}  native=${n315:X2}{n314:X2}");
            _out.WriteLine($"  $D418 (sid vol)  managed=${mD418:X2}  native=${nD418:X2}");

            Assert.Equal(BootstrapCart.MarkerHi, mMarkerHi);
            Assert.Equal(BootstrapCart.MarkerHi, nMarkerHi);
            Assert.Equal(BootstrapCart.MarkerLo, mMarkerLo);
            Assert.Equal(BootstrapCart.MarkerLo, nMarkerLo);
            Assert.Equal(0x01, mD021 & 0x0F);
            Assert.Equal(0x01, nD021 & 0x0F);
        }
        finally { ViceNative.Destroy(native); }
    }

    private static int FindFirstCycleMismatch(ICommodoreMachine managed, IntPtr native, int totalCycles, out string detail)
    {
        detail = "";
        for (int i = 0; i < totalCycles; i++)
        {
            managed.Clock.Step();
            ViceNative.StepNative(native);
            var (mPc, mA, mX, mY, mSP, mP) = ReadManagedCpu(managed);
            ushort nPc = ViceNative.GetPC(native); byte nA = ViceNative.GetA(native); byte nX = ViceNative.GetX(native);
            byte nY = ViceNative.GetY(native); byte nSP = ViceNative.GetS(native); byte nP = ViceNative.GetP(native);
            if (mPc != nPc || mA != nA || mX != nX || mY != nY || mSP != nSP || (mP & 0xCF) != (nP & 0xCF))
            {
                detail = $"managed PC={mPc:X4} A={mA:X2} X={mX:X2} Y={mY:X2} S={mSP:X2} P={mP:X2}  " +
                         $"native PC={nPc:X4} A={nA:X2} X={nX:X2} Y={nY:X2} S={nSP:X2} P={nP:X2}";
                return i;
            }
        }
        return -1;
    }

    private static (ICommodoreMachine Managed, IntPtr Native) SetupBothWithCart(byte[] cart)
    {
        var managed = CommodoreSystem.Build("c64", TestRomProvider.Create());
        var managedPort = managed.Underlying.Devices.GetAll<ICartridgePort>().Single();
        managedPort.AttachCartridge(cart, CartridgeMappingMode.Standard16K);
        managed.Underlying.Reset();
        var native = ViceNative.CreateModel("c64");
        ViceNative.AttachCartridge(native, cart, CartridgeMappingMode.Standard16K);
        ViceNative.ResetNative(native);
        return (managed, native);
    }

    private static (ushort PC, byte A, byte X, byte Y, byte S, byte P) ReadManagedCpu(ICommodoreMachine machine)
    {
        var cpu = (ViceSharp.Chips.Cpu.Mos6502)machine.Underlying.Devices.GetByRole(DeviceRole.Cpu)!;
        return (cpu.PC, cpu.A, cpu.X, cpu.Y, cpu.S, cpu.Flags);
    }

    private static PsidProgram LoadFrostPointPsid()
    {
        var path = Path.Combine(BootSpikeTests.RepoRootPublic, "assets", "sid", "Frost_Point.sid");
        using var fs = File.OpenRead(path);
        return PsidLoader.Load(fs);
    }
}
