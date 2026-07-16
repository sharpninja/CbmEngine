# Testing Requirements (MCP Server)

## TEST-CBM

### TEST-CBM-001

EncoderSpanTests: Encode(span,320,200) returns Bitmap=8000/Screen=1000/Color=1000.


### TEST-CBM-002

EncoderSpanTests: span output byte-identical to Image overload for same pixels.


### TEST-CBM-003

EncoderSpanTests: non 320x200 dims throw ArgumentException naming param.


### TEST-CBM-004

EncoderSpanTests: span shorter than width*height throws ArgumentException.


### TEST-CBM-005

EncoderSpanTests: forceBackgroundColor honored same as Image overload.


### TEST-CBM-006

EncoderSpanTests: Image overload delegates to span; existing encoder tests green.


### TEST-CBM-007

VicRegisterTests: MulticolorBitmap(bank1,0x6000,0x4400) gives D011=0x3B D016=0xD8 D018=0x18.


### TEST-CBM-008

VicRegisterTests: HiResBitmap gives D016=0xC8 D011=0x3B same D018.


### TEST-CBM-009

VicRegisterTests: BankBits(0-3) correct DD00 value; bank1 is binary 10.


### TEST-CBM-010

VicRegisterTests: SetMulticolorBitmap writes D011/D016/D018/DD00 via WriteIo (ReadIo verifies).


### TEST-CBM-011

VicRegisterTests: SetBorder writes D020, SetBackground writes D021 masked 0x0F.


### TEST-CBM-012

VicRegisterTests: invalid bank/misaligned base throws ArgumentException.


### TEST-CBM-013

RomDiscoveryTests: CBMENGINE_ROM_BASE env var wins.


### TEST-CBM-014

RomDiscoveryTests (TEST_CBM_014): with no CBMENGINE_ROM_BASE, DiscoverRomBase returns the per-user cache base.


### TEST-CBM-015

RomAcquisitionTests (TEST_ROM_001/002): EnsureC64RomsAsync downloads each missing C64 ROM via the acquirer and skips present ones.


### TEST-CBM-016

RomDiscoveryTests (TEST_CBM_016): CBMENGINE_ROM_BASE pointing at a missing directory falls back to the cache base (no throw).


### TEST-CBM-017

RomAcquisitionIntegrationTests (TEST_ROM_INT_001): download-on-demand caches the three C64 ROMs (IsAvailable true) then the emulator boots.


### TEST-CBM-018

RomDiscoveryIntegrationTests: Build(c64) builds machine equivalent to Build(c64,Discover()).


### TEST-CBM-019

RomDiscoveryTests: EncodeCbmVid/Game.Sample/CbmVidStudio entry points resolve ROMs via RomDiscovery/StudioRoms download-on-demand.


### TEST-CBM-020

BitmapFramePumpTests: writes Bitmap 8000 to BitmapBase via WriteRange.


### TEST-CBM-021

BitmapFramePumpTests: writes ScreenRam 1000 to ScreenBase via WriteRange.


### TEST-CBM-022

BitmapFramePumpTests: writes ColorRam 1000 to ColorBase via WriteIo.


### TEST-CBM-023

BitmapFramePumpTests: multicolor writes D016=0xD8, hires writes 0xC8 via WriteIo.


### TEST-CBM-024

BitmapFramePumpTests: D016 written only on mode change; first pump always writes.


### TEST-CBM-025

BitmapFramePumpTests: custom config addresses honored (BitmapBase 0xA000).


### TEST-CBM-026

BitmapFramePumpTests: null memory/frame throws ArgumentNullException.


### TEST-CBM-027

VideoPlayer regression: PumpFrame delegates to BitmapFramePump, existing tests green.


### TEST-CBM-028

EnterBitmapModeTests: after call IO reads D011=0x3B D016=0xD8 D018=0x18.


### TEST-CBM-029

EnterBitmapModeTests: SteadyState LineProgram contains D011/D016/D018/DD00 at assertRasterLine.


### TEST-CBM-030

EnterBitmapModeTests: LineDirector RunFrame re-asserts regs after simulated clobber.


### TEST-CBM-031

EnterBitmapModeTests: multicolor=false selects D016=0xC8, true selects 0xD8.


### TEST-CBM-032

EnterBitmapModeTests: SteadyState asserts bank via DD00 for non-default bank.


### TEST-CBM-033

EnterBitmapModeTests: invalid bank/misaligned base throws ArgumentException.


### TEST-CBM-034

EnterBitmapModeTests: no Ca65Assembler/external process in code path.


### TEST-CBM-035

MulticolorCanvasTests: defaults to 0; Clear(c) sets all pixels.


### TEST-CBM-036

MulticolorCanvasTests: FillRect sets rect to c, clipped to bounds.


### TEST-CBM-037

MulticolorCanvasTests: SetPixel out of bounds ignored.


### TEST-CBM-038

MulticolorCanvasTests: DrawGlyph sets bits as c, clear bits untouched.


### TEST-CBM-039

MulticolorCanvasTests: DrawText renders chars via font; unknown blank.


### TEST-CBM-040

MulticolorCanvasTests: Encode returns valid EncodedSplashBitmap 8000/1000/1000.


### TEST-CBM-041

CbmFont8x8Tests: GetGlyph covers A-Z 0-9 space, 8 bytes, missing all-zero.


### TEST-CBM-042

MulticolorCanvasTests: more than 3 colors in a cell still encodes without throwing.


### TEST-CBM-043

ScreenCodeTests: A-Z to 1..26, 0-9 to 0x30..0x39, space 0x20.


### TEST-CBM-044

TextServiceTests: Write places codes at screenBase+row*40+col via WriteRange.


### TEST-CBM-045

TextServiceTests: color and 0x0F at colorBase; WriteIo when IO range else WriteRange.


### TEST-CBM-046

TextServiceTests: col+len greater than 40 throws ArgumentException.


### TEST-CBM-047

TextServiceTests: col/row out of range throws ArgumentOutOfRangeException.


### TEST-CBM-048

ScreenCodeTests: unknown chars map to space 0x20.


### TEST-CBM-049

TextServiceTests: Clear fills 1000-cell screen and color region.


### TEST-CBM-050

ScreenLayoutTests: band rows not summing 25 throws ArgumentException.


### TEST-CBM-051

ScreenLayoutTests: non-overlapping addresses per region in RegionPlacement.


### TEST-CBM-052

ScreenLayoutTests: SteadyState has D011/D016/D018 at band start raster lines.


### TEST-CBM-053

ScreenLayoutTests: char band D018 and bitmap band D016 correct per split entry.


### TEST-CBM-054

ScreenLayoutIntegrationTests: RunFrameAndRecordRasterLines yields expected split lines.


### TEST-CBM-055

ScreenLayoutIntegrationTests: TextService + BitmapFramePump use regions without collision.


### TEST-CBM-056

ScreenLayoutTests: invalid layout (oversized/overlap/too many bands) throws ArgumentException.



## TEST-CBM-BOOT

### TEST-CBM-BOOT-001

Given C64Pal profile and real ROMs, when BootRunner.Run warms 120 frames, then framebuffer has >50 light-blue pixels in Y=[60,80] and ready.png is a valid PNG.


### TEST-CBM-BOOT-002

Given an IRomProvider returning false for every name, when Build is invoked, then InvalidOperationException is thrown with ROM filename in the message.


### TEST-CBM-BOOT-003

Given identical inputs, when BootRunner.Run is invoked three times, then all three SHA-256 hashes of the warmup framebuffer are identical.



## TEST-CBM-DEPS

### TEST-CBM-DEPS-001

After each slice and at completion, dotnet test (Unit + Integration) passes 0 failed / 0 skipped, on xUnit v3, with zero Moq references.



## TEST-CBM-HOST

### TEST-CBM-HOST-001

Given a fake clock advancing 50Hz over 5 seconds, when HeadlessHost.Run is invoked, then IBlitTarget.Upload is called at least 240 times.


### TEST-CBM-HOST-002

Given a real C64 machine playing a POKE 54296,15 + voice sequence, when 1s of emulated time passes, then >=1000 samples with abs>0.001 are captured.


### TEST-CBM-HOST-003

Given a booted READY machine, when A is pressed and 3 frames advance, then RAM[0x0277] contains 0x41.


### TEST-CBM-HOST-004

Given fakes for blit/audio/input/clock, when HeadlessHost.Run completes, then all fakes show non-zero call counts and no MonoGame Game class is touched.


### TEST-CBM-HOST-005

Given a fake machine, FakeBlitTarget, and FakeInputScript with a press at frame 0 and release at frame 2, when CbmViewport (useHybridPump=false) Tick runs 5 times, then machine.RunFrame is called 5 times, blit Upload count is 5, input is drained 5 times, and keyboard.SetKey(code,true/false) each fire once.


### TEST-CBM-HOST-006

Given a fake machine, when CbmViewport (useHybridPump=true) starts its threaded pump and is allowed to run briefly, then FramesCompleted increases and RefreshTexture uploads the latest pump frame to the blit target (Upload count and last dimensions match the framebuffer).


### TEST-CBM-HOST-007

CbmViewport throws ArgumentNullException for a null machine and for a null GraphicsDevice, and InvalidOperationException when the machine has no IVideoChip. FrameWidth/FrameHeight equal the video chip dimensions.


### TEST-CBM-HOST-008

Given a machine exposing an IAudioChip and a RecordingAudioBackend injected via the DI seam (non-hybrid), when Tick runs, then SID samples are submitted to the backend each frame; and EnqueueKey(code,true) forwards to IKeyboardMatrix.SetKey(code,true).


### TEST-CBM-HOST-009

Reflection over MonoGameHost shows a private field of type CbmViewport and no fields of type EmulatorPump, MonoGameBlitTarget, SidPump, or MonoGameAudioBackend, proving emulator composition is not duplicated.


### TEST-CBM-HOST-020

Unit tests for internal FrameLease. using var releases on exception. Non-hybrid unaffected. Uses fakes. Internal visibility.



## TEST-CBM-MIDI

### TEST-CBM-MIDI-001

Fixture type0.mid (1 track, 4 NoteOn/NoteOff pairs) loads; events match expected list.


### TEST-CBM-MIDI-002

Fixture type1.mid (3 tracks) loads; TrackCount == 3; per-track events match.


### TEST-CBM-MIDI-003

Truncated SMF throws InvalidDataException containing the truncation offset.


### TEST-CBM-MIDI-004

Real CommodoreSystem; bridge.NoteOn(0, 69, 100) writes $D400/$D401 matching 440 Hz (via HzToRegister) and $D404 gate=1.


### TEST-CBM-MIDI-005

Gate clears within 1 frame of NoteOff.


### TEST-CBM-MIDI-006

After 3 NoteOn (notes 60, 64, 67), a 4th NoteOn (note 72) returns voice index of the oldest sustained note.


### TEST-CBM-MIDI-007

With fake clock - 120 BPM segment in expected frames; tempo change to 60 BPM doubles the next segment's frame count.


### TEST-CBM-MIDI-008

After PC -> sawtooth patch, next NoteOn writes $D404 bit 5 set, bit 4 clear.


### TEST-CBM-MIDI-009

bridge.NoteOn(0, 60, 100) without SMF loaded writes correct registers.


### TEST-CBM-MIDI-010

BenchmarkDotNet MemoryDiagnoser - Gen0/Gen1/Gen2 == 0 after warm-up.


### TEST-CBM-MIDI-011

After 1s emulated playback, RecordingAudioBackend.Samples.Count(s => Math.Abs(s) > 0.001) >= 1000.



## TEST-CBM-PUBLISH

### TEST-CBM-PUBLISH-001

Automated tests cover API key masking for supported command forms, Nuke parameter secrecy, non-secret argument preservation, and repository gates.

**Acceptance Criteria:**
- [x] A unit test proves a separate --api-key value is absent from formatted output and replaced by the redaction token. (evidence: CommandLogFormatterTests.Format_RedactsSeparateApiKeyValue passed.)
- [x] A unit test proves an inline --api-key=value secret is absent from formatted output. (evidence: CommandLogFormatterTests.Format_RedactsInlineApiKeyValue passed.)
- [x] A source contract test proves NuGetApiKey remains annotated with Secret. (evidence: CommandLogFormatterTests.BuildNuGetApiKeyParameter_RemainsSecret passed.)
- [x] The build project compiles and all executed Unit and Integration tests pass with zero failed and zero skipped. (evidence: _build and solution Release builds succeeded with 0 warnings/errors; Unit 201/201 and Integration 81/81 passed, zero failed/skipped.)


## TEST-CBM-SAMPLE

### TEST-CBM-SAMPLE-001

Tests for extracted helpers, ILogger calls instead of Console, engine harness used in SampleGameTests and others.
