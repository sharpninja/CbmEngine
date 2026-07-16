# Technical Requirements (MCP Server)

## TR-CBM-BITMAPMODE-001

**Host-driven bitmap mode via LineProgram re-assert** — EnterBitmapMode asserts registers once via IMemoryService and returns a steady-state LineProgram re-asserting D011/D016/D018/DD00 each frame; no CC65.
**Covered by:** FR: FR-CBM-BITMAPMODE-001; TEST: TEST-CBM-028, TEST-CBM-029, TEST-CBM-030, TEST-CBM-031, TEST-CBM-032, TEST-CBM-033, TEST-CBM-034
**Status:** pending
Scope: layer-1+

## TR-CBM-BOOT-001

**BootRunner.Run entry point** — CbmEngine.Systems.BootRunner.Run(IMachineProfile profile, IRomProvider roms, int framesToWarm) returns (IMachine machine, byte[] bgra, int w, int h). Throws on missing ROMs.
**Covered by:** FR: FR-CBM-BOOT-001, FR-CBM-BOOT-002, FR-CBM-BOOT-003; TEST: TEST-CBM-BOOT-001, TEST-CBM-BOOT-002, TEST-CBM-BOOT-003
**Status:** pending
Scope: layer-1+

## TR-CBM-BOOT-002

**FramebufferPng.Write produces valid PNG** — CbmEngine.Systems.FramebufferPng.Write(string path, ReadOnlySpan<byte> bgra, int w, int h) produces a valid PNG using SixLabors.ImageSharp.
**Covered by:** FR: FR-CBM-BOOT-001; TEST: TEST-CBM-BOOT-001
**Status:** pending
Scope: layer-1+

## TR-CBM-BOOT-003

**PaletteAssertions.CountPixelsOfIndex helper** — CbmEngine.Systems.PaletteAssertions.CountPixelsOfIndex(ReadOnlySpan<byte> bgra, int w, int h, int paletteIndex, (int y0, int y1) band) uses VicPalette.Colors and returns inclusive count.
**Covered by:** FR: FR-CBM-BOOT-001; TEST: TEST-CBM-BOOT-001
**Status:** pending
Scope: layer-1+

## TR-CBM-CANVAS-001

**Paletted 160x200 canvas feeding the encoder** — MulticolorCanvas stores palette indices; doubles logical columns to 320 and calls the raw-span encoder; bundled CbmFont8x8 glyphs for text.
**Covered by:** FR: FR-CBM-CANVAS-001; TEST: TEST-CBM-035, TEST-CBM-036, TEST-CBM-037, TEST-CBM-038, TEST-CBM-039, TEST-CBM-040, TEST-CBM-041, TEST-CBM-042
**Status:** pending
Scope: layer-1+

## TR-CBM-DEPS-001

**Gated per-slice dependency upgrade** — Upgrades applied in gated Byrd v4 slices (risk-ascending): each slice edits its versions/code, then restore + build -c Release + targeted test; 0 failed / 0 skipped before the next. Lockstep families move as one; risky changes (ImageSharp 2->4, xUnit v3) isolated with a rollback.
**Covered by:** FR: FR-CBM-DEPS-001, FR-CBM-DEPS-002, FR-CBM-DEPS-003; TEST: TEST-CBM-DEPS-001
**Status:** pending
Scope: layer-1+

## TR-CBM-ENCODE-001

**Raw-span encode core shared by Image overload** — Extract multicolor encode core to operate on ReadOnlySpan<Rgba32>; Image overload copies rows then delegates; identical sampling (every 2nd column).
**Covered by:** FR: FR-CBM-ENCODE-001; TEST: TEST-CBM-001, TEST-CBM-002, TEST-CBM-003, TEST-CBM-004, TEST-CBM-005, TEST-CBM-006
**Status:** pending
Scope: layer-1+

## TR-CBM-HOST-001

**IBlitTarget abstraction** — IBlitTarget.Upload(ReadOnlySpan<byte> bgra, int w, int h) abstracts Texture2D.SetData. MonoGameBlitTarget is real impl; FakeBlitTarget counts uploads.
**Status:** pending
Scope: layer-1+

## TR-CBM-HOST-002

**SidPump polls IAudioChip** — SidPump polls IAudioChip.GenerateSample at SID sample rate and submits to IAudioBackend.SubmitSamples per frame, zero-alloc.
**Status:** pending
Scope: layer-1+

## TR-CBM-HOST-003

**KeyboardBridge maps MonoGame Keys to C64 matrix** — KeyboardBridge maps MonoGame Keys to C64 matrix codes via IKeyboardMatrix.SetKey, reads once per frame.
**Status:** pending
Scope: layer-1+

## TR-CBM-HOST-004

**HeadlessHost orchestrates pipeline** — HeadlessHost(IMachine, IBlitTarget, IAudioBackend, IInputScript, IClockSource, double refreshHz).Run(TimeSpan) - unit test entry point.
**Status:** pending
Scope: layer-1+

## TR-CBM-HOST-005

**CbmViewport public API and composition** — public sealed class CbmViewport : IDisposable, NOT deriving from Game. Public ctor CbmViewport(IMachine machine, GraphicsDevice graphicsDevice, double refreshHz=50.125, int sampleRate=44100, IGame? game=null, IGameContext? gameContext=null, bool useHybridPump=true, bool enableAudio=true). Public members: int FrameWidth; int FrameHeight; long FramesCompleted; Texture2D? CurrentTexture; void Update(GameTime); void Tick(); void RefreshTexture(); void Draw(SpriteBatch, Rectangle); void EnqueueKey(byte matrixCode, bool pressed); void Dispose(). Composes EmulatorPump (hybrid) or direct machine stepping (non-hybrid) plus MonoGameBlitTarget, KeyboardBridge, and SidPump+MonoGameAudioBackend. Internal DI ctor accepts IBlitTarget+IInputScript+IAudioBackend for headless tests.
**Covered by:** FR: FR-CBM-HOST-005; TEST: TEST-CBM-HOST-005, TEST-CBM-HOST-006, TEST-CBM-HOST-007, TEST-CBM-HOST-008
**Status:** pending
Scope: layer-1+

## TR-CBM-HOST-006

**MonoGameHost delegates emulator composition to CbmViewport** — MonoGameHost holds a single CbmViewport instance built in LoadContent. Update forwards Escape-to-exit then calls viewport.Update; Draw clears, begins a PointClamp SpriteBatch, calls viewport.Draw with the full back-buffer Rectangle, draws the FPS overlay, ends the batch. UnloadContent disposes the viewport. No EmulatorPump/MonoGameBlitTarget/SidPump/MonoGameAudioBackend fields remain on MonoGameHost.
**Covered by:** FR: FR-CBM-HOST-006; TEST: TEST-CBM-HOST-009
**Status:** pending
Scope: layer-1+

## TR-CBM-HOST-FRAME-001

**Implement internal ref struct FrameLease for safe framebuffer acquire in EmulatorPump / CbmViewport** — Add readonly ref struct FrameLease : IDisposable inside or alongside EmulatorPump. AcquireFrameForUpload returns the lease (internal). Update CbmViewport to use using var. Lease guarantees release. Visibility internal per approval. Ref struct yes.
**Covered by:** FR: FR-CBM-HOST-007; TEST: TEST-CBM-HOST-008, TEST-CBM-HOST-020
**Status:** pending
Scope: layer-1+

## TR-CBM-LAYOUT-001

**Mixed-mode band allocation + raster-split program** — ScreenLayout allocates non-overlapping screen/charset/bitmap within a VIC bank and emits per-band D011/D016/D018 writes at raster 51 + topRow*8.
**Covered by:** FR: FR-CBM-LAYOUT-001; TEST: TEST-CBM-050, TEST-CBM-051, TEST-CBM-052, TEST-CBM-053, TEST-CBM-054, TEST-CBM-055, TEST-CBM-056
**Status:** pending
Scope: layer-1+

## TR-CBM-MIDI-101

**AOT-safe SMF reader** — SmfReader.Load uses only BinaryReader and Stream.ReadExactly. No reflection. AOT-safe; compiles and runs under Phase 7 AOT publish smoke.
**Covered by:** FR: FR-CBM-MIDI-001, FR-CBM-MIDI-002; TEST: TEST-CBM-MIDI-001, TEST-CBM-MIDI-003, TEST-CBM-MIDI-002
**Status:** completed
Scope: layer-1+

## TR-CBM-MIDI-102

**Zero-allocation Tick hot path** — MidiSidBridge.Tick is zero-allocation after warm-up. Verified by BenchmarkDotNet MemoryDiagnoser - Gen0/Gen1/Gen2 == 0.
**Covered by:** FR: FR-CBM-MIDI-010; TEST: TEST-CBM-MIDI-010, TEST-CBM-MIDI-011
**Status:** completed
Scope: layer-1+

## TR-CBM-MIDI-103

**Voice stealing algorithm** — Voice stealing is O(3) per NoteOn - scan a 3-element VoiceState[], pick by LRU-release-time then LRU-start-time.
**Covered by:** FR: FR-CBM-MIDI-005, FR-CBM-MIDI-006; TEST: TEST-CBM-MIDI-006
**Status:** completed
Scope: layer-1+

## TR-CBM-MIDI-104

**Strategy-routed SID writes** — SID register writes route through ISoundChipStrategy (resolved per profile from Phase 2), never direct Bus.Write. Verified by a fake-strategy test recording the call sequence.
**Covered by:** FR: FR-CBM-MIDI-003, FR-CBM-MIDI-004, FR-CBM-MIDI-008, FR-CBM-MIDI-009; TEST: TEST-CBM-MIDI-004, TEST-CBM-MIDI-005, TEST-CBM-MIDI-008, TEST-CBM-MIDI-009
**Status:** completed
Scope: layer-1+

## TR-CBM-MIDI-105

**Tempo map storage and lookup** — Tempo map stored as ImmutableArray<(long Tick, int UsecPerQuarter)>. Binary-search lookup in O(log N).
**Covered by:** FR: FR-CBM-MIDI-007; TEST: TEST-CBM-MIDI-007
**Status:** completed
Scope: layer-1+

## TR-CBM-MIDI-106

**Tick-to-frame conversion** — frame = (eventTick * usecPerQuarter / ticksPerQuarter) * refreshHz / 1_000_000. refreshHz from ICommodoreMachine.Capabilities.RefreshHz.
**Covered by:** FR: FR-CBM-MIDI-007; TEST: TEST-CBM-MIDI-007
**Status:** completed
Scope: layer-1+

## TR-CBM-PUBLISH-001

**Redact NuGet API key from Nuke command logging** — PublishNuGet must mark its API key parameter secret and use a tested command formatter that masks sensitive option values in informational and exception logging without mutating the launched process arguments.
**Covered by:** FR: FR-CBM-PUBLISH-001; TEST: TEST-CBM-PUBLISH-001
**Status:** completed
Scope: layer-1+
**Acceptance Criteria:**
- [x] The Nuke NuGetApiKey parameter is annotated with Secret and generated schema reflects secret entry handling. (evidence: _build/Build.cs contains [Secret]; .nuke/build.schema.json directs secret entry through nuke :secrets; source contract test passed.)
- [x] All build command log and process-failure renderings redact separate and inline --api-key forms. (evidence: All four RunProcessIn/CaptureProcessIn informational and failure renderings use CommandLogFormatter.Format; separate and inline tests passed.)
- [x] Redaction does not mutate or replace arguments supplied to the child process. (evidence: Production code passes the original arguments collection to CreateProcess; input array non-mutation test passed; dummy Compile invocation exited 0.)

## TR-CBM-PUMP-001

**Stream-independent frame pump with mode-change suppression** — BitmapFramePump writes bitmap/screen via WriteRange, color via WriteIo, D016 only on mode change; span overload is alloc-free; VideoPlayer delegates.
**Covered by:** FR: FR-CBM-PUMP-001; TEST: TEST-CBM-020, TEST-CBM-021, TEST-CBM-022, TEST-CBM-023, TEST-CBM-024, TEST-CBM-025, TEST-CBM-026, TEST-CBM-027
**Status:** pending
Scope: layer-1+

## TR-CBM-ROM-001

**ROM base resolution + download-on-demand acquisition** — Resolution order: CBMENGINE_ROM_BASE (existing directory) else RomCache.DefaultBasePath (LocalApplicationData/CbmEngine/roms). An IRomAcquirer seam over the concrete ViceSharp RomProvider (RomProviderAcquirer) exposes IsAvailable + DownloadAsync (download, SHA256 verify), then materializes the canonical VICE filename via RomAcquisition.MaterializeCanonicalC64. RomAcquisition.EnsureC64RomsAsync ensures basic/kernal/characters and is serialized process-wide by a SemaphoreSlim so a shared cache is safe under parallel access. RomDiscovery exposes DiscoverRomBase, Discover, EnsureRomBaseAsync, and DiscoverOrDownloadAsync. No git submodule and no build-time ROM copies; ViceSharp.Core 1.0.1+ supplies the locator and the VICE mirror ROM URLs.
**Covered by:** FR: FR-CBM-ROM-001; TEST: TEST-CBM-013, TEST-CBM-014, TEST-CBM-015, TEST-CBM-016, TEST-CBM-017, TEST-CBM-018, TEST-CBM-019
**Status:** completed
Scope: layer-1+

## TR-CBM-SAMPLE-LOG-001

**Introduce Microsoft.Extensions.Logging in sample to replace Console.WriteLine** — Add pkg ref to Abstractions, wire ILogger, replace all Console.*Write , preserve output semantics.
**Covered by:** FR: FR-CBM-SAMPLE-001; TEST: TEST-CBM-SAMPLE-001
**Status:** pending
Scope: layer-1+

## TR-CBM-SAMPLE-ORG-001

**Extract duplicated boot/cart/headless logic and add ILogger support in sample; encapsulate harness as engine feature** — Refactor Program.cs per review (in scope). Add ILogger. Encapsulate harness (build+boot+warmup) as engine feature in Systems (Testing or Boot extension). Bridge to existing Action<string> loggers. Update tests.
**Covered by:** FR: FR-CBM-SAMPLE-001; TEST: TEST-CBM-SAMPLE-001
**Status:** pending
Scope: layer-1+

## TR-CBM-TEXT-001

**Screen-code mapping + screen/color RAM poke** — ScreenCode maps ASCII to C64 screen codes; TextService writes screen RAM via WriteRange and color RAM via WriteIo when in the IO page.
**Covered by:** FR: FR-CBM-TEXT-001; TEST: TEST-CBM-043, TEST-CBM-044, TEST-CBM-045, TEST-CBM-046, TEST-CBM-047, TEST-CBM-048, TEST-CBM-049
**Status:** pending
Scope: layer-1+

## TR-CBM-VIC-001

**Pure VIC register math + IO writers** — Compute D011/D016/D018 + CIA2 DD00 bank bits from bank/bitmapBase/screenBase; setters route via IMemoryService.WriteIo with DD00 read-modify-write.
**Covered by:** FR: FR-CBM-VIC-001; TEST: TEST-CBM-007, TEST-CBM-008, TEST-CBM-009, TEST-CBM-010, TEST-CBM-011, TEST-CBM-012
**Status:** pending
Scope: layer-1+

