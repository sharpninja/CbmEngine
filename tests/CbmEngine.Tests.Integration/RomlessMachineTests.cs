using CbmEngine.Systems;
using CbmEngine.Systems.Audio;
using CbmEngine.Systems.Boot;
using CbmEngine.Systems.Strategy;
using CbmEngine.Systems.Video;
using CbmEngine.Tests.Integration.Helpers;
using CbmEngine.Tests.Shared.Helpers;
using Xunit;

namespace CbmEngine.Tests.Integration;

/// <summary>
/// ROM-less C64: <see cref="CommodoreSystem.BuildRomless(string, ViceSharp.Abstractions.IAudioBackend?)"/>
/// builds a machine with no VICE ROM images (blank BASIC/CHARGEN, no KERNAL) whose managed VIC-II still
/// rasterises a host-driven multicolour bitmap frame, and whose SID still emits audio. This is what lets a
/// consumer (BBCrawler) render and play music without shipping the copyrighted VICE ROMs.
/// </summary>
[Trait("Speed", "Slow")]
public class RomlessMachineTests
{
    // Full-screen multicolour bitmap: screen RAM $0400, bitmap $2000, colour RAM $D800 (VIC bank 0).
    private const byte D011FullBitmap = 0x3B; // DEN + BMM, yscroll 3 (row-stable full bitmap)
    private const byte D016Mcm = 0x18;        // CSEL + MCM
    private const byte D018Bitmap = 0x18;     // screen $0400, bitmap $2000
    private const ushort BitmapBase = 0x2000;
    private const ushort ScreenBase = 0x0400;
    private const ushort ColorRamBase = 0xD800;

    [Fact]
    public void BuildRomless_ReturnsLiveMachine_WithVicIiFramebuffer()
    {
        var sys = CommodoreSystem.BuildRomless("c64");

        Assert.NotNull(sys);
        Assert.NotNull(sys.VideoChip);
        Assert.Equal(384, sys.VideoChip.FrameWidth);
        Assert.Equal(272, sys.VideoChip.FrameHeight);
        Assert.Equal(
            sys.VideoChip.FrameWidth * sys.VideoChip.FrameHeight * 4,
            sys.VideoChip.FrameBuffer.Length);
    }

    [Fact]
    public void BuildRomless_MulticolorBitmap_RendersBackgroundColorAcrossActiveArea()
    {
        const byte background = 6; // blue: distinct from the black border (index 0)
        var sys = CommodoreSystem.BuildRomless("c64");

        // Program a full-screen multicolour bitmap. An all-zero bitmap makes every pixel-pair "00",
        // which the VIC draws in the background colour ($D021) -- so the whole active area becomes blue
        // iff the managed VIC-II rasterises a ROM-less frame correctly.
        sys.Memory.WriteIo(Vic.D011, new[] { D011FullBitmap });
        sys.Memory.WriteIo(Vic.D016, new[] { D016Mcm });
        sys.Memory.WriteIo(Vic.D018, new[] { D018Bitmap });
        sys.Memory.WriteIo(Vic.D020, new byte[] { 0 });
        sys.Memory.WriteIo(Vic.D021, new[] { background });
        sys.Memory.WriteRange(BitmapBase, new byte[8000]);
        sys.Memory.WriteRange(ScreenBase, new byte[1000]);
        sys.Memory.WriteIo(ColorRamBase, new byte[1000]);

        for (int i = 0; i < 4; i++) sys.RunFrame();

        int blueInBand = PaletteAssertions.CountPixelsOfIndex(
            sys.VideoChip.FrameBuffer,
            sys.VideoChip.FrameWidth,
            sys.VideoChip.FrameHeight,
            paletteIndex: background,
            yMin: 80,
            yMax: 160);

        Assert.True(
            blueInBand > 10000,
            $"Expected the ROM-less multicolour bitmap active band (y 80..160) filled with background " +
            $"index {background}; got {blueInBand} matching pixels.");
    }

    [Fact]
    public void BuildRomless_Psid_ViaStandaloneIrq_StreamsOscillatingSid()
    {
        var audio = new RecordingAudioBackend();
        var sys = CommodoreSystem.BuildRomless("c64", audio);
        var ctx = new GameContext(sys);

        // Install a PSID and drive it from a stand-alone RAM IRQ (KERNAL banked out) - the ROM-less
        // playback path. No warm-boot / KERNAL CIA setup: InstallStandaloneIrqDriver programs CIA1
        // Timer A itself.
        using var ms = new MemoryStream(PsidFixtures.BuildSyntheticPsid());
        var program = PsidLoader.Load(ms);
        ctx.Music.Install(program);
        ctx.Music.InstallStandaloneIrqDriver();

        for (int i = 0; i < 120; i++) sys.RunFrame();

        Assert.True(
            audio.Samples.Count > 10_000,
            $"Expected the ROM-less SID to stream samples to the audio backend during RunFrame; " +
            $"got {audio.Samples.Count}.");

        float min = float.MaxValue, max = float.MinValue;
        foreach (var s in audio.Samples)
        {
            if (s < min) min = s;
            if (s > max) max = s;
        }

        Assert.True(
            max - min > 0.01f,
            $"Expected an oscillating (non-DC) ROM-less SID waveform; min={min}, max={max}, span={max - min}.");
    }
}
