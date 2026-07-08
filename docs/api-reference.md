# API Reference

A condensed index of the public surface, grouped by namespace. For concepts and
examples, follow the links into the topic guides. Signatures here are the load-
bearing ones; minor overloads and private members are omitted.

## CbmEngine.Abstractions

The contracts and value types. No behavior.

### IGame / IGameContext
```csharp
public interface IGame
{
    void Initialize(IGameContext context);
    void Update(IGameContext context, int frameIndex);
    void Draw(IGameContext context, int frameIndex);
}

public interface IGameContext
{
    ICommodoreMachine Machine { get; }
}
```
The game lifecycle. `IGameContext` exposes only `Machine`; the gameplay services
live on the concrete `GameContext`. See [Architecture](architecture.md).

### ICommodoreMachine
```csharp
public interface ICommodoreMachine
{
    IMachine Underlying { get; }
    MachineCapabilities Capabilities { get; }
    IVideoChip VideoChip { get; }
    IAudioChip? AudioChip { get; }
    IKeyboardMatrix? KeyboardMatrix { get; }
    IBus Bus { get; }
    IClock Clock { get; }
    IMemoryService Memory { get; }
    ISoundChipStrategy Sound { get; }
    void RunFrame();
}
```
Your handle to the emulated C64.

### IMemoryService
```csharp
public interface IMemoryService
{
    ReadOnlySpan<byte> Snapshot();
    ReadOnlySpan<byte> SnapshotRange(ushort address, int length);
    Span<byte> View(ushort address, int length);
    IReadOnlyList<MemoryRangeHandle> ViewMany(IReadOnlyList<(ushort address, int length)> ranges);
    Span<byte> Materialize(MemoryRangeHandle handle);
    void WriteRange(ushort address, ReadOnlySpan<byte> source);
    void WriteIo(ushort address, ReadOnlySpan<byte> source);
    byte ReadIo(ushort address);
    bool IsIoAddress(ushort address);
}
public readonly record struct MemoryRangeHandle(ushort Address, int Length);
```
RAM versus I/O access. See [Memory and I/O](memory-and-io.md).

### ISoundChipStrategy
```csharp
public interface ISoundChipStrategy
{
    SidModel Model { get; }
    long ClockHz { get; }
    void SetVoiceFrequency(int voice, double hz);
    void SetVoicePulseWidth(int voice, int width);
    void SetVoiceWaveform(int voice, Waveform waveform, bool gate,
                          bool ringMod = false, bool sync = false, bool test = false);
    void SetVoiceAdsr(int voice, byte attack, byte decay, byte sustain, byte release);
    void SetFilter(int cutoff, byte resonance, byte voiceRouting,
                   bool lowPass, bool bandPass, bool highPass, bool voice3Off);
    void SetVolume(byte volume);
    void SilenceAll();
    ushort HzToRegister(double hz);
    double RegisterToHz(ushort registerValue);
}
```
High-level SID programming. See [Audio and SID](audio-and-sid.md).

### Other abstractions
```csharp
public interface IBlitTarget    { void Upload(ReadOnlySpan<byte> bgra, int width, int height); }
public interface IClockSource   { TimeSpan Now { get; } void Tick(TimeSpan duration); }
public interface IInputScript    { IReadOnlyList<InputEvent> DrainForFrame(int frameIndex); }
public readonly record struct InputEvent(byte MatrixCode, bool Pressed);

public enum SidModel { Mos6581, Mos8580 }
[Flags] public enum Waveform : byte { None=0, Triangle=1, Sawtooth=2, Pulse=4, Noise=8 }

public sealed record MachineCapabilities(
    string ProfileId, string DisplayName, VideoStandard VideoStandard,
    int CyclesPerLine, int RasterLines, long NominalClockHz, double RefreshRateHz,
    SidModel SidModel, ImmutableArray<uint> BgraPalette);
```

### LineProgram (raster effects)
```csharp
public readonly record struct MemoryWrite(ushort Address, byte Value);

public sealed class LineProgram
{
    public LineProgram(IDictionary<int, IReadOnlyList<MemoryWrite>> byLine);
    public bool TryGet(int line, out IReadOnlyList<MemoryWrite> writes);
    public int Count { get; }
    public IEnumerable<int> Lines { get; }

    public sealed class Builder
    {
        public Builder At(int line, ushort address, byte value);
        public Builder At(int line, IEnumerable<MemoryWrite> writes);
        public LineProgram Build();
    }
}
```
Per-raster-line register writes, applied by `LineDirector`.

## CbmEngine.Systems

### CommodoreSystem (factory) / CommodoreMachine
```csharp
public static class CommodoreSystem
{
    public static readonly IReadOnlyList<string> SupportedProfileIds; // c64, c64c, ntsc, newntsc
    public static ICommodoreMachine Build(string profileId, IRomProvider roms);
    public static C64MachineProfile ResolveProfile(string profileId);
}
```
The top-level entry point. `CommodoreMachine` is the `ICommodoreMachine`
implementation it returns.

### RomDiscovery / RomCache (ROM acquisition)
```csharp
public static class RomDiscovery
{
    public const string RomBaseEnvVar = "CBMENGINE_ROM_BASE";
    public static string DiscoverRomBase(string? startDir = null);
    public static IRomProvider Discover(string? startDir = null);            // resolve only, no download
    public static Task<string> EnsureRomBaseAsync(string? startDir = null, CancellationToken ct = default);
    public static Task<IRomProvider> DiscoverOrDownloadAsync(string? startDir = null, CancellationToken ct = default);
}

public static class RomCache { public static string DefaultBasePath { get; } } // %LOCALAPPDATA%/CbmEngine/roms
```
The C64 ROMs (`basic`/`kernal`/`characters`) are resolved from
`CBMENGINE_ROM_BASE` if it points at an existing directory, otherwise the
per-user cache, and downloaded on demand (SHA256-verified) by
`DiscoverOrDownloadAsync` / `EnsureRomBaseAsync`. `CommodoreSystem.Build` and
`BootRunner.Run` take the resulting `IRomProvider`.

### GameContext
```csharp
public sealed class GameContext : IGameContext
{
    public GameContext(ICommodoreMachine machine);
    public ICommodoreMachine Machine { get; }
    public SpriteService Sprites { get; }
    public TilemapService Tilemap { get; }
    public MusicService Music { get; }
}
```

### MemoryService
The `IMemoryService` implementation. I/O ranges: `$D000`-`$D3FF` (VIC),
`$D400`-`$D7FF` (SID), `$D800`-`$DBFF` (color RAM), `$DC00`-`$DCFF` (CIA #1),
`$DD00`-`$DDFF` (CIA #2).

### SpriteService
```csharp
public sealed class SpriteService
{
    public SpriteService(IMemoryService memory);
    public void SetPosition(int slot, int x, int y);  // slot 0-7, x 0-511, y 0-255
    public void SetEnabled(int slot, bool enabled);
    public void SetColor(int slot, byte color);
    public void SetPointer(int slot, byte spriteBlockIndex);
}
```
See [Graphics](graphics-sprites-tilemap.md).

### TilemapService
```csharp
public sealed class TilemapService
{
    public const int ScreenRamBase = 0x0400, ColorRamBase = 0xD800, Columns = 40, Rows = 25;
    public TilemapService(IMemoryService memory);
    public void SetCell(int col, int row, byte glyph, byte color);
    public void Fill(byte glyph, byte color);
    public byte ReadGlyph(int col, int row);
}
```

### MusicService
```csharp
public sealed class MusicService
{
    public MusicService(ICommodoreMachine machine);
    public bool IsPlaying { get; }
    public int CurrentSong { get; }
    public PsidHeader? CurrentHeader { get; }
    public bool NativeIrqDriverInstalled { get; }
    public ushort NativeIrqStubAddress { get; }
    public void Install(PsidProgram program, int song = 1);
    public void Tick();
    public byte[] InjectIrqStubBytes(ushort stubAddress = 0xC000);
    public void InstallNativeIrqDriver(ushort stubAddress = 0xC000);
    public void SetSong(int subTune);
    public void Stop();
}
```
See [Audio and SID](audio-and-sid.md).

### Sound strategies
```csharp
public abstract class SidStrategyBase : ISoundChipStrategy { /* SID register writer */ }
public sealed class Sid6581Strategy : SidStrategyBase { public override SidModel Model => Mos6581; }
public sealed class Sid8580Strategy : SidStrategyBase { public override SidModel Model => Mos8580; }
```

### Audio (PSID)
```csharp
public sealed record PsidHeader(string Magic, ushort Version, ushort DataOffset,
    ushort LoadAddress, ushort InitAddress, ushort PlayAddress, ushort SongCount,
    ushort StartSong, uint SpeedFlags, string Name, string Author, string Released);
public sealed record PsidProgram(PsidHeader Header, ReadOnlyMemory<byte> Payload);
public static class PsidLoader { public static PsidProgram Load(Stream stream); }
// Exceptions: PsidFormatException, PsidPlacementException, PsidExecutionException
```
See the format table in [Audio and SID](audio-and-sid.md).

### Cartridge
```csharp
public static class CartridgeImage
{
    public const int Size16K = 0x4000;
    public const ushort RomBase = 0x8000, CodeStart = 0x8009;
    public static byte[] Build16K(ReadOnlySpan<byte> code,
        ushort coldStartAddress = CodeStart, ushort? warmStartAddress = null);
}

public static class BitmapPlayerCart
{ public static byte[] Build(EncodedSplashBitmap initialSplash, Ca65Assembler? assembler = null); }

public static class PsidPlayerCart
{ public static byte[] Build(PsidProgram program, byte backgroundColor = 0x01,
    byte initialBorderColor = 0x00, int borderCyclePeriodFrames = 50,
    EncodedSplashBitmap? splash = null, Ca65Assembler? assembler = null); }

public sealed record CapturedSplashAssets(/* charset, screen, color, sprites, VIC regs */);
public static class CapturedSplashCart
{ public static byte[] Build(PsidProgram program, CapturedSplashAssets assets, Ca65Assembler? assembler = null); }

public static class CrtFile
{ public static byte[] WrapStandard16K(ReadOnlySpan<byte> rom16K, string cartridgeName = "CBMENGINE CART"); }

public sealed class Ca65Assembler
{
    public Ca65Assembler(string? ca65Path = null, string? ld65Path = null);
    public string Ca65Path { get; }
    public string Ld65Path { get; }
    public static bool IsAvailable();
    public byte[] Build(string asmSource, string linkerConfig,
        IReadOnlyDictionary<string, byte[]>? includeBinaries = null);
}

public static class CartridgeBoot
{ public static CartridgeBootResult AttachAndWaitForMarker(
    ICommodoreMachine machine, ReadOnlyMemory<byte> cartImage,
    ushort markerAddress = 0x0334, byte expectedHi = 0xCB, byte expectedLo = 0x42, int maxFrames = 300); }
public readonly record struct CartridgeBootResult(int FramesUntilMarker, bool MarkerSeen);

public static class BootstrapCart
{ public const ushort MarkerAddress = 0x0334; public const byte MarkerHi = 0xCB, MarkerLo = 0x42;
  public static byte[] BuildMarkerOnly16K(byte borderColor = 0x00, byte backgroundColor = 0x06, ...); }
```
See [Cartridges and .CRT](cartridges-and-crt.md).

#### Cart ROM segment maps (for reference)

PSID player cart (`PsidPlayerCartSource`):
`HEADER=$8000  BOOT=$8009  IRQ=$8200  BITMAP=$8300  SCREEN=$A240  COLOR=$A628  PAYLOAD=$AA10`

Bitmap player cart (`BitmapPlayerCartSource`):
`HEADER=$8000  BOOT=$8009  BITMAP=$8300  SCREEN=$A240  COLOR=$A628`

RAM targets at boot: bitmap `$6000`, screen `$4400`, color RAM `$D800`.

### Boot helpers
```csharp
public static class BootRunner
{ public static BootResult Run(C64MachineProfile profile, IRomProvider roms, int framesToWarm); }
public readonly record struct BootResult(IMachine Machine, byte[] FrameBuffer, int Width, int Height);

public static class FramebufferPng
{ public static void Write(string path, ReadOnlySpan<byte> bgra, int width, int height); }

public static class PaletteAssertions
{ public static int CountPixelsOfIndex(ReadOnlySpan<byte> bgra, int width, int height,
    int paletteIndex, int yMin, int yMax); }
```

### Video (CbmVid playback)
```csharp
public sealed class VideoPlayer : IDisposable
{
    public VideoPlayer(Stream stream, bool leaveOpen = false);
    public CbmVidHeader Header { get; }
    public int CurrentFrame { get; }
    public bool Loop { get; set; }
    public bool IsFinished { get; }
    public void Reset();
    public void Seek(int frameIndex);
    public EncodedSplashBitmap PeekFrame(int frameIndex);
    public EncodedSplashBitmap PeekFrame0AsSplash();
    public bool PumpFrame(IMemoryService memory);
    public static CbmVidHeader Validate(Stream stream);
    public static CbmVidHeader ValidateFile(string path);
}

public static class CbmVidGifExporter
{
    public static void Export(string cbmvidPath, string gifPath, IRomProvider roms, Action<string>? log = null);
    public static void Export(Stream cbmvidStream, string gifPath, IRomProvider roms, Action<string>? log = null);
}
```
See [CbmVid Video Format](cbmvid-format.md).

### MIDI to SID
```csharp
public sealed class MidiSidBridge : IDisposable
{
    public MidiSidBridge(ICommodoreMachine machine, double refreshHz = 50.125);
    public MidiSidBridge(ISoundChipStrategy strategy, double refreshHz = 50.125);
    public bool IsPlaying { get; }
    public bool IsFinished { get; }
    public long CurrentTick { get; }
    public int VoicesActive { get; }
    public void Load(Stream midiStream);
    public void Load(SmfFile smf);
    public void Play();
    public void Pause();
    public void Stop();
    public void Tick(int frameIndex);
    public SidPatch GetPatch(int channel);
    public void SetPatch(int channel, SidPatch patch);
    public void NoteOn(int voice, byte midiNote, byte velocity);
    public void NoteOff(int voice);
}

public sealed class VoiceAllocator
{
    public const int VoiceCount = 3;
    public int ActiveCount { get; }
    public byte GetNote(int voice);
    public bool IsActive(int voice);
    public int NoteOn(int channel, byte midiNote, long startFrame);
    public int NoteOff(int channel, byte midiNote, long releaseFrame);
    public void Reset();
}

public readonly record struct SidPatch(Waveform Waveform, byte Attack, byte Decay,
    byte Sustain, byte Release, ushort PulseWidth = 0x0800, bool RingMod = false, bool Sync = false);
// static: LeadPulse, LeadSawtooth, LeadTriangle, BassPluck, BassWarm, Pad, NoiseHit
//         DefaultTriangle, DefaultSawtooth, DefaultPulse, DefaultNoise

public static class SidPatchLibrary
{ public static SidPatch ForProgram(byte program); public static SidPatch DrumPatch { get; } }
```
See [MIDI to SID](midi-to-sid.md).

### Assets
```csharp
public static class AssetLoader
{
    public static CompiledCharset LoadCharset(string path);
    public static CompiledTilemap LoadTilemap(string path);
    public static CompiledCharset DeserializeCharset(ReadOnlySpan<byte> bytes);
    public static CompiledTilemap DeserializeTilemap(ReadOnlySpan<byte> bytes);
}
```

## CbmEngine.Pipeline

### VicPalette
```csharp
public static class VicPalette
{
    public readonly record struct Rgb(byte R, byte G, byte B);
    public static readonly Rgb[] Colors;                       // 16 entries, index = VIC color
    public static bool TryExact(byte r, byte g, byte b, out int index);
    public static void WritePaletteImage(string outPath);      // 16x16 PNG for ffmpeg paletteuse
}
```
The palette table is in [Graphics](graphics-sprites-tilemap.md).

### Bitmap encoder
```csharp
public enum SplashBitmapMode { Multicolor, HiRes }
public sealed record EncodedSplashBitmap(SplashBitmapMode Mode, byte BackgroundColorIndex,
    byte[] Bitmap, byte[] ScreenRam, byte[] ColorRam)
{ public const int BitmapByteSize = 8000, ScreenRamSize = 1000, ColorRamSize = 1000; }

public static class C64MulticolorBitmapEncoder
{
    public static EncodedSplashBitmap Encode(string pngPath,
        byte? forceBackgroundColor = null, string? debugDecodedPngPath = null);
    public static EncodedSplashBitmap EncodeHiRes(string pngPath, string? debugDecodedPngPath = null);
}
```

### CbmVid format and encoder
```csharp
public enum CbmVidFrameMode : byte { Multicolor = 0, HiRes = 1 }
public readonly record struct CbmVidHeader(ushort Width, ushort Height, ushort FrameRate,
    uint FrameCount, CbmVidFrameMode DefaultMode, byte Flags);

public static class CbmVidFormat { /* Magic, Version, HeaderSize=64, FrameRecordSize=10004, ... */ }

public sealed class CbmVidWriter : IDisposable
{
    public CbmVidWriter(Stream output, CbmVidHeader header, bool leaveOpen = false);
    public void WriteFrame(EncodedSplashBitmap frame);
    public void FinalizeFrameCount();
}

public sealed record CbmVidEncodeManifest(string OutputPath,
    IReadOnlyList<CbmVidEncodeManifest.Entry> Frames, ushort FrameRate = 50,
    CbmVidFrameMode DefaultMode = CbmVidFrameMode.Multicolor, byte Flags = 0, bool StrictPalette = true)
{ public sealed record Entry(string PngPath, CbmVidFrameMode? ModeOverride = null, byte? ForcedBackground = null); }

public static class CbmVidEncoder
{
    public static void EncodeAnimatedGif(string gifPath, string outputPath,
        CbmVidFrameMode defaultMode = CbmVidFrameMode.Multicolor,
        ushort? overrideFrameRate = null, Action<string>? log = null);
    public static void EncodeVideo(string videoPath, string outputPath, ushort frameRate = 50,
        CbmVidFrameMode defaultMode = CbmVidFrameMode.Multicolor, string? ffmpegPath = null,
        string? scratchDirectory = null, bool keepIntermediateFrames = false, Action<string>? log = null);
    public static void EncodeDirectory(string pngDirectory, string outputPath, ushort frameRate = 50,
        CbmVidFrameMode defaultMode = CbmVidFrameMode.Multicolor);
    public static void Encode(CbmVidEncodeManifest manifest);
}

public sealed class CbmVidEncodeException : Exception
{ public int FrameIndex { get; } public string? PngPath { get; } }
```

### MIDI (SMF reader)
```csharp
public static class SmfReader { public static SmfFile Load(Stream stream); }
public sealed record SmfFile(int Format, int TrackCount, int TicksPerQuarter,
    IReadOnlyList<IReadOnlyList<MidiEvent>> Tracks);

public abstract record MidiEvent(long Tick);
public sealed record NoteOnEvent(long Tick, int Channel, byte Note, byte Velocity) : MidiEvent;
public sealed record NoteOffEvent(long Tick, int Channel, byte Note) : MidiEvent;
public sealed record TempoEvent(long Tick, int MicrosecondsPerQuarter) : MidiEvent;
public sealed record ProgramChangeEvent(long Tick, int Channel, byte Program) : MidiEvent;
public sealed record ControlChangeEvent(long Tick, int Channel, byte Controller, byte Value) : MidiEvent;
public sealed record PitchBendEvent(long Tick, int Channel, short Value) : MidiEvent;
public sealed record EndOfTrackEvent(long Tick) : MidiEvent;

public sealed class MidiTempoMap
{
    public readonly record struct Entry(long Tick, int MicrosecondsPerQuarter);
    public MidiTempoMap(IEnumerable<Entry> entries);
    public int MicrosecondsPerQuarterAt(long tick);
    public double TickToFrame(long tick, int ticksPerQuarter, double refreshHz);
}
```

## CbmEngine.Host.MonoGame

```csharp
public sealed class MonoGameHost : Microsoft.Xna.Framework.Game
{
    public MonoGameHost(IMachine machine, double refreshHz = 50.125, int sampleRate = 44100,
        int windowScale = 2, IGame? game = null, IGameContext? gameContext = null, bool useHybridPump = true);
    // Run() is inherited from Game.
}

public sealed class CbmViewport : IDisposable
{
    public CbmViewport(IMachine machine, GraphicsDevice graphicsDevice, double refreshHz = 50.125,
        int sampleRate = 44100, IGame? game = null, IGameContext? gameContext = null,
        bool useHybridPump = true, bool enableAudio = true);
    public int FrameWidth { get; }
    public int FrameHeight { get; }
    public long FramesCompleted { get; }
    public bool UsesHybridPump { get; }
    public Texture2D? CurrentTexture { get; }
    public void Update(GameTime gameTime);
    public void Tick();
    public void RefreshTexture();
    public void Draw(SpriteBatch spriteBatch, Rectangle destination);
    public void EnqueueKey(byte matrixCode, bool pressed);
}

public sealed class EmulatorPump : IDisposable
{
    public EmulatorPump(IMachine machine, double targetHz, IGame? game = null,
        IGameContext? gameContext = null, SidPump? sidPump = null);
    public int FrameWidth { get; }
    public int FrameHeight { get; }
    public long FramesCompleted { get; }
    public long LateFrames { get; }
    public double AverageEmulatorStepMs { get; }
    public void Start();
    public void EnqueueKey(byte matrixCode, bool pressed);
    public void CopyLatestFrame(Span<byte> dest);
    public ReadOnlySpan<byte> AcquireFrameForUpload();
    public void ReleaseFrame();
}
```
See [Architecture](architecture.md).

## Conventions cheat sheet

- **Default refresh rate:** PAL `50.125` Hz everywhere.
- **Supported profiles:** `c64`, `c64c`, `ntsc`, `newntsc`.
- **Voices:** SID has 3 (0-2). Sprites: 8 (0-7). Screen: 40x25.
- **RAM versus I/O:** RAM via `View`/`WriteRange`/`Snapshot`; registers via
  `WriteIo`/`ReadIo`. Color RAM (`$D800`) is I/O.
- **Cart boot marker:** `$CB $42` at `$0334`/`$0335`.
- **Bitmap banks (engine convention):** bitmap `$6000`, screen `$4400`, color
  `$D800`; `$D016` = `$C8` (hires) or `$D8` (multicolor).
- **Endianness:** C64 data is little-endian; PSID and `.CRT` headers are
  big-endian; CbmVid is little-endian.
