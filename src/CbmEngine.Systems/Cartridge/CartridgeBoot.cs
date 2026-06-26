using CbmEngine.Abstractions;
using CbmEngine.Systems.Strategy;
using ViceSharp.Abstractions;
using ViceSharp.RomFetch;

namespace CbmEngine.Systems.Cartridge;

public readonly record struct CartridgeBootResult(int FramesUntilMarker, bool MarkerSeen);

public static class CartridgeBoot
{
    public static CartridgeBootResult AttachAndWaitForMarker(
        ICommodoreMachine machine,
        ReadOnlyMemory<byte> cartImage,
        ushort markerAddress = BootstrapCart.MarkerAddress,
        byte expectedHi = BootstrapCart.MarkerHi,
        byte expectedLo = BootstrapCart.MarkerLo,
        int maxFrames = 300)
    {
        ArgumentNullException.ThrowIfNull(machine);
        if (cartImage.Length is not (CartridgeImage.Size16K or 0x2000))
            throw new ArgumentException("Cart image must be 8K or 16K.", nameof(cartImage));

        var port = machine.Underlying.Devices.GetAll<ICartridgePort>().SingleOrDefault()
            ?? throw new InvalidOperationException("Machine has no ICartridgePort.");

        var mappingMode = cartImage.Length == CartridgeImage.Size16K
            ? CartridgeMappingMode.Standard16K
            : CartridgeMappingMode.Standard8K;

        port.AttachCartridge(cartImage, mappingMode);
        machine.Underlying.Reset();

        for (int frame = 0; frame < maxFrames; frame++)
        {
            machine.RunFrame();
            if (machine.Bus.Read(markerAddress) == expectedHi &&
                machine.Bus.Read((ushort)(markerAddress + 1)) == expectedLo)
            {
                return new CartridgeBootResult(frame + 1, true);
            }
        }

        return new CartridgeBootResult(maxFrames, false);
    }

    /// <summary>
    /// Engine-provided test harness helper that encapsulates the common pattern:
    /// CommodoreSystem.Build + marker cart attach + wait for bootstrap marker + GameContext.
    /// This reduces duplication across unit/integration tests and sample setup.
    /// (Part of review remediation + BDP harness encapsulation request.)
    /// </summary>
    public static (ICommodoreMachine Machine, GameContext Context) CreateBootedTestContext(
        IRomProvider roms,
        ReadOnlyMemory<byte>? markerCart = null,
        string profileId = "c64",
        int maxFrames = 300)
    {
        ArgumentNullException.ThrowIfNull(roms);
        var cart = markerCart ?? BootstrapCart.BuildMarkerOnly16K();
        var sys = CommodoreSystem.Build(profileId, roms);
        var boot = AttachAndWaitForMarker(sys, cart, maxFrames: maxFrames);
        if (!boot.MarkerSeen)
            throw new InvalidOperationException($"Test harness marker not seen after {boot.FramesUntilMarker} frames.");
        var ctx = new GameContext(sys);
        return (sys, ctx);
    }
}
