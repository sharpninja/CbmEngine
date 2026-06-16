# Functional Requirements (MCP Server)

## FR-CBM-BITMAPMODE-001 Host-driven bitmap mode without CC65 (CBMFR-002)

machine.EnterBitmapMode(bank,bitmapBase,screenBase,multicolor) asserts VIC regs once and returns a re-asserting steady-state LineProgram, no CC65.
**Acceptance Criteria:**
- [ ] After EnterBitmapMode(bank1,0x6000,0x4400,multicolor), machine IO reads D011=0x3B, D016=0xD8, D018=0x18.
- [ ] Returned SteadyState LineProgram contains D011/D016/D018 and DD00 writes at assertRasterLine.
- [ ] Running SteadyState via LineDirector for one frame re-asserts registers after a simulated clobber.
- [ ] multicolor=false selects D016=0xC8; true selects 0xD8.
- [ ] SteadyState asserts the VIC bank via DD00 so a non-default bank survives.
- [ ] Invalid bank or misaligned base throws ArgumentException (delegates to Vic validation).
- [ ] No CC65/external process invoked; code path has no Ca65Assembler dependency.

## FR-CBM-BOOT-001 Boot C64 PAL and produce READY screen framebuffer

The engine boots a C64 PAL machine from a profile selector and produces a framebuffer matching the post-warmup READY screen.
AC: After 120 frames of warmup against C64MachineProfiles.C64Pal with real ROMs, the BGRA framebuffer contains a non-zero count of palette-index-14 (light-blue) pixels in vertical band Y in [60, 80]; PNG written to artifacts/phase0/ready.png exists and is a valid PNG.

## FR-CBM-BOOT-002 Missing ROMs fail fast with precise error

Missing or invalid ROMs fail the build with a clear error.
AC: With IRomProvider returning false for every name, ArchitectureBuilder.Build(C64Descriptor(C64Pal)) throws InvalidOperationException whose Message contains the first missing ROM filename.

## FR-CBM-BOOT-003 Boot framebuffer is deterministic across runs

Boot is deterministic across runs on the same machine.
AC: SHA-256 of the framebuffer at frame 120 is stable across three consecutive runs and written to artifacts/phase0/ready.sha256.

## FR-CBM-CANVAS-001 Multicolor drawing canvas + bundled font (CBMFR-004)

MulticolorCanvas (160x200 logical) with Clear/FillRect/SetPixel/DrawGlyph/DrawText/Encode plus bundled CbmFont8x8.
**Acceptance Criteria:**
- [ ] New canvas pixels default to 0; Clear(c) sets all pixels to c.
- [ ] FillRect(x,y,w,h,c) sets that rectangle to c, clipped to canvas bounds.
- [ ] SetPixel out of bounds is ignored (no throw).
- [ ] DrawGlyph renders set bits as c, leaves clear bits untouched.
- [ ] DrawText renders each char via CbmFont8x8 advancing 8 logical px; unknown char blank.
- [ ] Encode() returns valid EncodedSplashBitmap (8000/1000/1000) via the encoder.
- [ ] CbmFont8x8.GetGlyph covers A-Z, 0-9, space; returns 8 bytes; missing glyph all-zero.
- [ ] Drawing more than 3 non-background colors in one 4x8 cell still encodes without throwing.

## FR-CBM-ENCODE-001 Raw-span multicolor encode overload (CBMFR-001 follow-up)

Add Encode(ReadOnlySpan<Rgba32> pixels,int width,int height,byte? forceBackgroundColor) so callers encode frames without an ImageSharp Image.
**Acceptance Criteria:**
- [ ] Encode(span,320,200) returns EncodedSplashBitmap with Bitmap=8000, ScreenRam=1000, ColorRam=1000.
- [ ] Output byte-identical to Encode(Image) for identical pixels.
- [ ] width/height not 320x200 throws ArgumentException naming the param.
- [ ] pixels.Length less than width*height throws ArgumentException.
- [ ] forceBackgroundColor honored identically to the Image overload.
- [ ] Image overload delegates to span overload; existing encoder tests stay green.

## FR-CBM-HOST-001 Host renders live framebuffer at PAL refresh

AC: After running HeadlessHost.Run for 5 simulated seconds with a fake clock advancing at 50 Hz, the host has called IBlitTarget.Upload at least 240 times (250 - 10 slack).

## FR-CBM-HOST-002 Host forwards SID samples to audio backend

AC: After driving the machine through a POKE 54296,15 etc sequence for 1s emulated time, a recording IAudioBackend has captured at least 1000 samples with abs(value) > 0.001.

## FR-CBM-HOST-003 Host forwards keyboard input into C64 matrix within 3 frames

AC: With scripted press-A before frame N, RAM[0x0277] (KEYD buffer) contains 0x41 at or before frame N+3 once BASIC has scanned the keyboard.

## FR-CBM-HOST-004 Full host pipeline runs headless

AC: HeadlessHost.Run executes the full present + audio + input pipeline against test doubles for GraphicsDevice/SoundEffect/Keyboard layers; MonoGame Game class is not instantiated.

## FR-CBM-HOST-005 Reusable Game-agnostic embeddable emulator viewport

CbmEngine.Host.MonoGame exposes a public, Game-agnostic component (CbmViewport) that composes the emulator pump, BGRA->RGBA blit target, keyboard input bridge, and SID audio backend from an IMachine and a GraphicsDevice. It MUST NOT derive from or reference MonoGame Game, so an external MonoGame Game subclass (e.g. a Myra-driven host) can embed a live emulated framebuffer by calling Update/Tick, Draw(SpriteBatch, Rectangle), accessing the latest frame texture, forwarding input, and disposing it. AC: CbmViewport can be constructed and driven headlessly via its DI seam against fakes; one Tick advances exactly one emulated frame (machine.RunFrame once), uploads one frame to the blit target, drains input into the C64 matrix, and pumps SID audio when present; it is IDisposable and does not inherit Game.

## FR-CBM-HOST-006 MonoGameHost composes the viewport on a single code path

MonoGameHost is refactored to compose CbmViewport internally so the emulator pump + blit + input + audio wiring exists in exactly one place. MonoGameHost retains only Game/window concerns (window sizing, Escape-to-exit, FPS overlay, SpriteBatch Begin/End) and delegates all emulator composition to CbmViewport. No parallel or duplicated pump/blit/input/audio wiring remains. AC: MonoGameHost declares a CbmViewport field and declares no EmulatorPump, MonoGameBlitTarget, SidPump, or MonoGameAudioBackend fields of its own; window behavior (size, demo run) is unchanged.

## FR-CBM-LAYOUT-001 Mixed-mode raster-split layout helper (CBMFR-007)

ScreenLayout.Builder composing CharBand/BitmapBand into a CompiledScreenLayout with per-region addresses and a raster-split LineProgram.
**Acceptance Criteria:**
- [ ] Band rows must sum to 25 else Build throws ArgumentException.
- [ ] Build allocates non-overlapping screen/color/bitmap addresses per region within the bank, reported in RegionPlacement.
- [ ] SteadyState has D011/D016/D018 writes at each band start raster line (51 + topRow*8), flipping bitmap/char.
- [ ] Char bands D018 points at char screen+charset; bitmap band D016 sets multicolor/hires per split entry.
- [ ] Running SteadyState via LineDirector.RunFrameAndRecordRasterLines yields expected split lines.
- [ ] RegionPlacements usable by TextService (char bands) and BitmapFramePump (bitmap band) with no address collision.
- [ ] Invalid layout (rows not 25, oversized/overlapping bitmap, too many bands) throws ArgumentException.

## FR-CBM-MIDI-001 SmfReader parses Type 0 SMF

SmfReader.Load(stream) returns SmfFile{Format=0, TrackCount=1}; Tracks[0] contains the fixture's expected NoteOn/NoteOff sequence in tick order.

## FR-CBM-MIDI-002 SmfReader parses Type 1 SMF

Multi-track fixture loads; TrackCount matches header; per-track event lists ordered by tick.

## FR-CBM-MIDI-003 Bridge drives SID registers via WriteIo

After bridge.Load + Play + Tick(0) for a first-tick NoteOn(note=69), IMemoryService.WriteIo was called with $D404 bit 0 (gate) set; $D400/$D401 hold the value of Sid6581Strategy.HzToRegister(440.0).

## FR-CBM-MIDI-004 NoteOff releases the matching voice

After NoteOn at frame N then NoteOff at frame N+10, voice 0's gate bit is clear at frame N+10 (bus.Read($D404) and 0x01 == 0).

## FR-CBM-MIDI-005 Three simultaneous notes use all three voices

Three NoteOn at the same tick on distinct notes - $D404, $D40B, $D412 all gate=1; voice frequencies differ.

## FR-CBM-MIDI-006 Fourth simultaneous note triggers voice stealing

A 4th NoteOn at the same tick while 3 voices active - oldest-sustained voice's gate clears then re-arms at the new note's frequency. bridge.VoicesActive == 3.

## FR-CBM-MIDI-007 Tempo events change playback rate

SMF with one quarter at 120 BPM, MetaTempo to 60 BPM, another quarter - total frames-to-completion is 60 + 120 PAL frames within +/- 2 frames.

## FR-CBM-MIDI-008 ProgramChange swaps the channel's patch

After PC swapping channel 0 to GM bass (sawtooth + low ADSR), next NoteOn writes $D404 with bit 5 set (sawtooth) and bit 4 clear.

## FR-CBM-MIDI-009 Direct game-side NoteOn/NoteOff bypasses the schedule

With no SMF loaded, bridge.NoteOn(voice=0, note=60, velocity=100) writes $D404 gate=1; bridge.NoteOff(voice=0) clears gate.

## FR-CBM-MIDI-010 Sample exposes --midi=<file> flag

dotnet run --project src/CbmEngine.Game.Sample -- --midi=fixtures/midi/test.mid loads and plays MIDI through the C64 emulator; RecordingAudioBackend captures >=1000 samples with abs>0.001 in the first second.

## FR-CBM-PUMP-001 General per-frame bitmap pump (CBMFR-003)

BitmapFramePump plus BitmapFramePumpConfig writing an EncodedSplashBitmap to VIC memory independent of any stream.
**Acceptance Criteria:**
- [ ] Pump writes frame.Bitmap (8000) to BitmapBase via WriteRange.
- [ ] Pump writes frame.ScreenRam (1000) to ScreenBase via WriteRange.
- [ ] Pump writes frame.ColorRam (1000) to ColorBase via WriteIo.
- [ ] Multicolor frame writes D016=0xD8; HiRes frame writes D016=0xC8 via WriteIo.
- [ ] D016 written only on mode change vs previous pump; first pump always writes.
- [ ] Custom config addresses honored (e.g. BitmapBase 0xA000).
- [ ] null memory or frame throws ArgumentNullException.
- [ ] VideoPlayer.PumpFrame refactored to delegate to BitmapFramePump; existing tests stay green.

## FR-CBM-ROM-001 ROM base resolution helper (CBMFR-006)

RomDiscovery static plus CommodoreSystem.Build(profileId) overload locating bundled VICE ROMs.
**Acceptance Criteria:**
- [ ] CBMENGINE_ROM_BASE env var pointing at a valid dir wins (highest precedence).
- [ ] From startDir under a tree with CbmEngine.slnx and external vice data dir, returns that data dir.
- [ ] Fallback without slnx that finds the external vice data dir returns it.
- [ ] Nothing found throws InvalidOperationException with actionable message naming ROM base/env var.
- [ ] RomDiscovery.Discover() returns IRomProvider whose IsAvailable is true for required C64 ROMs.
- [ ] CommodoreSystem.Build(c64) builds ICommodoreMachine equivalent to Build(c64, Discover()).
- [ ] Program.FindRomBase refactored to call RomDiscovery; sample still runs.

## FR-CBM-TEXT-001 Char-mode text rendering helper (CBMFR-008)

TextService plus ScreenCode mapping: ASCII to C64 screen codes poked into screen RAM and color RAM.
**Acceptance Criteria:**
- [ ] ScreenCode.FromAscii maps A-Z to 1..26, 0-9 to 0x30..0x39, space to 0x20.
- [ ] TextService.Write places screen codes at screenBase + row*40 + col via WriteRange.
- [ ] Write places color and 0x0F at colorBase + same offset; WriteIo when colorBase is IO range else WriteRange.
- [ ] col + text.Length greater than 40 throws ArgumentException.
- [ ] col outside 0-39 or row outside 0-24 throws ArgumentOutOfRangeException.
- [ ] Unknown chars map to space (0x20).
- [ ] TextService.Clear fills the 1000-cell screen and color region.

## FR-CBM-VIC-001 Typed VIC mode/register helpers (CBMFR-005)

Static Vic class plus VicBitmapRegisters struct computing D011/D016/D018/bank and writing D020/D021.
**Acceptance Criteria:**
- [ ] Vic.MulticolorBitmap(bank1,0x6000,0x4400) returns D011=0x3B, D016=0xD8, D018=0x18.
- [ ] Vic.HiResBitmap returns D016=0xC8, D011=0x3B, same D018 math.
- [ ] Vic.BankBits(bank) returns correct CIA2 DD00 2-bit value for banks 0-3 (bank1 gives binary 10).
- [ ] Vic.SetMulticolorBitmap writes D011/D016/D018 + DD00 via WriteIo, verifiable via ReadIo.
- [ ] Vic.SetBorder writes D020 and SetBackground writes D021 masked to color and 0x0F.
- [ ] Invalid bank or misaligned base throws ArgumentException.

