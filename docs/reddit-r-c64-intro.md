# Reddit r/c64 Introduction Post

Copy-paste ready. Title options up top, body below. Replace the repo URL if you
prefer the Azure DevOps mirror. Attaching a GIF of the Frost Point cart booting
in VICE plus a short MIDI-to-SID clip will massively outperform a text-only post.

---

## Title (pick one)

1. I built CbmEngine: a .NET C64 engine that drives a real emulated VIC-II/SID and spits out bootable .CRT carts
2. Made an open-source C64 engine: real SID/VIC emulation, MIDI-to-SID, and a full-motion video format
3. [OC] CbmEngine: build real C64 cartridges (and play MIDI on the SID) from C#

---

## Body

Hey r/c64,

I've been building **CbmEngine**, an open-source engine for the C64, and figured this is the right crowd to show it to (and to get torn apart by, constructively).

**What it actually is:** a .NET 10 engine that runs a real, cycle-level C64 emulation underneath (the [ViceSharp](https://github.com/sharpninja/vice-sharp) core, i.e. VICE's guts). It is not a "C64-style" framework that fakes the look. You're talking to a real VIC-II, a real SID, and a real 6510: screen RAM at `$0400`, sprite registers at `$D000`, SID at `$D400`. If you know the hardware, you already know the engine.

**The part you'll care about most: it builds real `.CRT` cartridges.** Standard 16K carts, assembled with `ca65`/`ld65`, with a proper CBM80 autostart header, wrapped in the actual `.CRT` container. They boot in VICE and on real hardware via a cart flasher. Three cart types so far:

- **PSID/RSID music-player carts** - drop in a `.sid`, get a cart that plays it under a CIA-timer IRQ (with an optional bitmap splash and a border-color heartbeat).
- **Bitmap splash carts** - hi-res or multicolor full-screen image.
- **Captured-screen carts** - freeze a live text screen (custom charset + screen + color + up to 8 sprites) into a bootable demo.

**MIDI-to-SID:** load a standard `.mid` and hear it played live on the 3 SID voices. General MIDI program changes map to SID patches, velocity drives the envelope sustain (since the SID has no per-voice volume), pitch bend works, and there's a free/release/steal-oldest voice allocator because, well, 3 voices and 16 MIDI channels. There's a Fur Elise fixture if you want to hear the voice-stealing in action.

**CbmVid (experimental):** a full-motion video format. Encode any video, animated GIF, or PNG sequence into a stream of VIC-II bitmap frames and play it back through the real emulated VIC. No magic compression, every frame is a genuine 320x200 bitmap + screen + color snapshot. There's a CLI and a little GUI studio that previews frames through the actual chip before you commit.

**Honest about what runs where:** interactive game logic is currently written in C# driving the emulated machine on the PC side, so a "game" you write today runs against the emulator, not as native 6502 on a breadbin. The cartridge outputs above *are* real 6502 and *do* run on hardware. Closing that gap (compiling more of the runtime to native carts) is where I want to take it.

It's early and a one-person hobby project, open source. Repo, docs, and a runnable sample (the "Frost Point" SID-cart demo) are here: **https://github.com/sharpninja/CbmEngine**

Would genuinely love feedback from this sub: which cart types would actually be useful to you, whether the SID/MIDI mapping sounds right to your ears, and what "real hardware" support should mean first. Roast away.
