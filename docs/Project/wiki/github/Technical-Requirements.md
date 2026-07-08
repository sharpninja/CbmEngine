# Technical Requirements (MCP Server)

## TR-CBM-BITMAPMODE-001

**Host-driven bitmap mode via LineProgram re-assert** — EnterBitmapMode asserts registers once via IMemoryService and returns a steady-state LineProgram re-asserting D011/D016/D018/DD00 each frame; no CC65.
Scope: layer-1+

## TR-CBM-BOOT-001

**BootRunner.Run entry point** — CbmEngine.Systems.BootRunner.Run(IMachineProfile profile, IRomProvider roms, int framesToWarm) returns (IMachine machine, byte[] bgra, int w, int h). Throws on missing ROMs.
Scope: layer-1+

## TR-CBM-BOOT-002

**FramebufferPng.Write produces valid PNG** — CbmEngine.Systems.FramebufferPng.Write(string path, ReadOnlySpan<byte> bgra, int w, int h) produces a valid PNG using SixLabors.ImageSharp.
Scope: layer-1+

## TR-CBM-BOOT-003

**PaletteAssertions.CountPixelsOfIndex helper** — CbmEngine.Systems.PaletteAssertions.CountPixelsOfIndex(ReadOnlySpan<byte> bgra, int w, int h, int paletteIndex, (int y0, int y1) band) uses VicPalette.Colors and returns inclusive count.
Scope: layer-1+

## TR-CBM-CANVAS-001

**Paletted 160x200 canvas feeding the encoder** — MulticolorCanvas stores palette indices; doubles logical columns to 320 and calls the raw-span encoder; bundled CbmFont8x8 glyphs for text.
Scope: layer-1+

## TR-CBM-ENCODE-001

**Raw-span encode core shared by Image overload** — Extract multicolor encode core to operate on ReadOnlySpan<Rgba32>; Image overload copies rows then delegates; identical sampling (every 2nd column).
Scope: layer-1+

## TR-CBM-HOST-001

**IBlitTarget abstraction** — IBlitTarget.Upload(ReadOnlySpan<byte> bgra, int w, int h) abstracts Texture2D.SetData. MonoGameBlitTarget is real impl; FakeBlitTarget counts uploads.
Scope: layer-1+

## TR-CBM-HOST-002

**SidPump polls IAudioChip** — SidPump polls IAudioChip.GenerateSample at SID sample rate and submits to IAudioBackend.SubmitSamples per frame, zero-alloc.
Scope: layer-1+

## TR-CBM-HOST-003

**KeyboardBridge maps MonoGame Keys to C64 matrix** — KeyboardBridge maps MonoGame Keys to C64 matrix codes via IKeyboardMatrix.SetKey, reads once per frame.
Scope: layer-1+

## TR-CBM-HOST-004

**HeadlessHost orchestrates pipeline** — HeadlessHost(IMachine, IBlitTarget, IAudioBackend, IInputScript, IClockSource, double refreshHz).Run(TimeSpan) - unit test entry point.
Scope: layer-1+

## TR-CBM-HOST-005

**CbmViewport public API and composition** — public sealed class CbmViewport : IDisposable, NOT deriving from Game. Public ctor CbmViewport(IMachine machine, GraphicsDevice graphicsDevice, double refreshHz=50.125, int sampleRate=44100, IGame? game=null, IGameContext? gameContext=null, bool useHybridPump=true, bool enableAudio=true). Public members: int FrameWidth; int FrameHeight; long FramesCompleted; Texture2D? CurrentTexture; void Update(GameTime); void Tick(); void RefreshTexture(); void Draw(SpriteBatch, Rectangle); void EnqueueKey(byte matrixCode, bool pressed); void Dispose(). Composes EmulatorPump (hybrid) or direct machine stepping (non-hybrid) plus MonoGameBlitTarget, KeyboardBridge, and SidPump+MonoGameAudioBackend. Internal DI ctor accepts IBlitTarget+IInputScript+IAudioBackend for headless tests.
Scope: layer-1+

## TR-CBM-HOST-006

**MonoGameHost delegates emulator composition to CbmViewport** — MonoGameHost holds a single CbmViewport instance built in LoadContent. Update forwards Escape-to-exit then calls viewport.Update; Draw clears, begins a PointClamp SpriteBatch, calls viewport.Draw with the full back-buffer Rectangle, draws the FPS overlay, ends the batch. UnloadContent disposes the viewport. No EmulatorPump/MonoGameBlitTarget/SidPump/MonoGameAudioBackend fields remain on MonoGameHost.
Scope: layer-1+

## TR-CBM-HOST-FRAME-001

**Implement internal ref struct FrameLease for safe framebuffer acquire in EmulatorPump / CbmViewport** — Add readonly ref struct FrameLease : IDisposable inside or alongside EmulatorPump. AcquireFrameForUpload returns the lease (internal). Update CbmViewport to use using var. Lease guarantees release. Visibility internal per approval. Ref struct yes.
Scope: layer-1+

## TR-CBM-LAYOUT-001

**Mixed-mode band allocation + raster-split program** — ScreenLayout allocates non-overlapping screen/charset/bitmap within a VIC bank and emits per-band D011/D016/D018 writes at raster 51 + topRow*8.
Scope: layer-1+

## TR-CBM-MIDI-101

**AOT-safe SMF reader** — SmfReader.Load uses only BinaryReader and Stream.ReadExactly. No reflection. AOT-safe; compiles and runs under Phase 7 AOT publish smoke.
Scope: layer-1+

## TR-CBM-MIDI-102

**Zero-allocation Tick hot path** — MidiSidBridge.Tick is zero-allocation after warm-up. Verified by BenchmarkDotNet MemoryDiagnoser - Gen0/Gen1/Gen2 == 0.
Scope: layer-1+

## TR-CBM-MIDI-103

**Voice stealing algorithm** — Voice stealing is O(3) per NoteOn - scan a 3-element VoiceState[], pick by LRU-release-time then LRU-start-time.
Scope: layer-1+

## TR-CBM-MIDI-104

**Strategy-routed SID writes** — SID register writes route through ISoundChipStrategy (resolved per profile from Phase 2), never direct Bus.Write. Verified by a fake-strategy test recording the call sequence.
Scope: layer-1+

## TR-CBM-MIDI-105

**Tempo map storage and lookup** — Tempo map stored as ImmutableArray<(long Tick, int UsecPerQuarter)>. Binary-search lookup in O(log N).
Scope: layer-1+

## TR-CBM-MIDI-106

**Tick-to-frame conversion** — frame = (eventTick * usecPerQuarter / ticksPerQuarter) * refreshHz / 1_000_000. refreshHz from ICommodoreMachine.Capabilities.RefreshHz.
Scope: layer-1+

## TR-CBM-PUMP-001

**Stream-independent frame pump with mode-change suppression** — BitmapFramePump writes bitmap/screen via WriteRange, color via WriteIo, D016 only on mode change; span overload is alloc-free; VideoPlayer delegates.
Scope: layer-1+

## TR-CBM-ROM-001

**ROM base resolution + download-on-demand acquisition** — Resolution order: CBMENGINE_ROM_BASE (existing directory) else RomCache.DefaultBasePath (LocalApplicationData/CbmEngine/roms). An IRomAcquirer seam over the concrete ViceSharp RomProvider (RomProviderAcquirer) exposes IsAvailable + DownloadAsync (download, SHA256 verify), then materializes the canonical VICE filename via RomAcquisition.MaterializeCanonicalC64. RomAcquisition.EnsureC64RomsAsync ensures basic/kernal/characters and is serialized process-wide by a SemaphoreSlim so a shared cache is safe under parallel access. RomDiscovery exposes DiscoverRomBase, Discover, EnsureRomBaseAsync, and DiscoverOrDownloadAsync. No git submodule and no build-time ROM copies; ViceSharp.Core 1.0.1+ supplies the locator and the VICE mirror ROM URLs.
Scope: layer-1+

## TR-CBM-SAMPLE-LOG-001

**Introduce Microsoft.Extensions.Logging in sample to replace Console.WriteLine** — Add pkg ref to Abstractions, wire ILogger, replace all Console.*Write , preserve output semantics.
Scope: layer-1+

## TR-CBM-SAMPLE-ORG-001

**Extract duplicated boot/cart/headless logic and add ILogger support in sample; encapsulate harness as engine feature** — Refactor Program.cs per review (in scope). Add ILogger. Encapsulate harness (build+boot+warmup) as engine feature in Systems (Testing or Boot extension). Bridge to existing Action<string> loggers. Update tests.
Scope: layer-1+

## TR-CBM-TEXT-001

**Screen-code mapping + screen/color RAM poke** — ScreenCode maps ASCII to C64 screen codes; TextService writes screen RAM via WriteRange and color RAM via WriteIo when in the IO page.
Scope: layer-1+

## TR-CBM-VIC-001

**Pure VIC register math + IO writers** — Compute D011/D016/D018 + CIA2 DD00 bank bits from bank/bitmapBase/screenBase; setters route via IMemoryService.WriteIo with DD00 read-modify-write.
Scope: layer-1+

