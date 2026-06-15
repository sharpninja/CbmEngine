# Memory and I/O

All memory access in CbmEngine goes through `IMemoryService` (reachable as
`machine.Memory`). It enforces one rule that defines the whole engine:

> **RAM and I/O are different worlds. RAM accessors hit the 64 KB backing store
> directly. I/O accessors go through the system bus so registers behave like
> hardware. Using a RAM accessor on an I/O range throws.**

This mirrors real C64 behavior: a write to `$D020` is not the same as a write to
`$8000`. The first changes the border color through the VIC; the second drops a
byte into RAM. The engine refuses to let you confuse the two.

## The I/O ranges

These five ranges are "I/O" to the engine. Any RAM-style access overlapping them
throws `InvalidOperationException`:

| Range | Chip / function |
|-------|-----------------|
| `$D000`-`$D3FF` | VIC-II (video) |
| `$D400`-`$D7FF` | SID (audio) |
| `$D800`-`$DBFF` | Color RAM |
| `$DC00`-`$DCFF` | CIA #1 (keyboard, joystick, Timer A) |
| `$DD00`-`$DDFF` | CIA #2 (serial, NMI, VIC bank select) |

Everything else (`$0000`-`$CFFF` and `$E000`-`$FFFF`) is plain RAM, including
screen RAM at `$0400` and the sprite pointers at `$07F8`.

> **Color RAM is a special case.** `$D800`-`$DBFF` is I/O, so even though it
> "feels" like RAM you must write it with `WriteIo`. The `TilemapService` does
> this for you.

## The API

### RAM access (the backing store)

```csharp
// Read-only views (no copy):
ReadOnlySpan<byte> Snapshot();                          // the whole 64 KB
ReadOnlySpan<byte> SnapshotRange(ushort addr, int len); // a slice (I/O allowed for reads)

// Writable views (throw if the range overlaps I/O):
Span<byte> View(ushort addr, int len);                  // a writable RAM slice
void WriteRange(ushort addr, ReadOnlySpan<byte> source);// bulk copy into RAM

// Batch validation for hot paths:
IReadOnlyList<MemoryRangeHandle> ViewMany(IReadOnlyList<(ushort address, int length)> ranges);
Span<byte> Materialize(MemoryRangeHandle handle);
```

`Snapshot`/`SnapshotRange` return live read-only views: cheap, no allocation.
`View` and `WriteRange` write straight into the RAM array, so they are fast and
have no bus side effects. They are the right tools for screen RAM, bitmap data,
character sets, and sprite data.

`ViewMany`/`Materialize` let you validate a set of ranges once (returning
`MemoryRangeHandle` tokens) and resolve them to writable spans later, avoiding
repeated bounds checks in tight loops.

### I/O access (through the bus)

```csharp
void WriteIo(ushort addr, ReadOnlySpan<byte> source);   // write registers via the bus
byte ReadIo(ushort addr);                               // read a register via the bus
bool IsIoAddress(ushort addr);                          // is this an I/O address?
```

`WriteIo` writes each byte through the bus so the target chip sees a proper
register write. `ReadIo` reads a single register back. Use these for VIC, SID,
CIA, and color RAM.

```csharp
// Set the border to red ($D020) and background to black ($D021):
machine.Memory.WriteIo(0xD020, new byte[] { 0x02 });
machine.Memory.WriteIo(0xD021, new byte[] { 0x00 });

// Read the current border color back:
byte border = machine.Memory.ReadIo(0xD020) & 0x0F;
```

> `WriteIo` takes a `ReadOnlySpan<byte>`, so a `byte[]` works directly. Writing
> a multi-byte span pokes consecutive registers. A write that would run past
> `$FFFF` throws `ArgumentOutOfRangeException`.

## A worked memory map

A typical bitmap-mode game using VIC bank 1 (the layout the engine's carts and
CbmVid player use) looks like this:

| Address | Size | Contents | Access |
|---------|------|----------|--------|
| `$0400` | 1000 | Text screen RAM (40x25) | `View` / `WriteRange` (RAM) |
| `$07F8` | 8 | Sprite pointers | `View` (RAM) |
| `$4400` | 1000 | Bitmap-mode screen RAM (color pairs) | `View` / `WriteRange` (RAM) |
| `$6000` | 8000 | Bitmap data (320x200) | `View` / `WriteRange` (RAM) |
| `$D000`-`$D02E` | - | VIC registers (sprites, control, colors) | `WriteIo` / `ReadIo` |
| `$D400`-`$D418` | - | SID registers | use `ISoundChipStrategy` |
| `$D800` | 1000 | Color RAM | `WriteIo` |

## What happens if you get it wrong

```csharp
// WRONG: $D020 is in the VIC I/O range. This throws InvalidOperationException
// with a message telling you to use WriteIo/ReadIo instead.
machine.Memory.WriteRange(0xD020, new byte[] { 0x02 });

// RIGHT:
machine.Memory.WriteIo(0xD020, new byte[] { 0x02 });
```

The error names the overlapping range, so misdirected writes fail loudly at the
call site rather than silently corrupting state.

## Reading the whole machine

For debugging, capture tools, or snapshot tests, `Snapshot()` gives you a
read-only view of all 64 KB:

```csharp
ReadOnlySpan<byte> ram = machine.Memory.Snapshot();
byte spriteEnable = machine.Memory.ReadIo(0xD015);   // I/O still goes through ReadIo
```

The diagnostic tools under `tools/` (for example `AnalyzeFrostCapture` and
`DumpSprites`) work from exactly these kinds of RAM/VIC snapshots. See
[Tools](tools.md).

## Notes and caveats

- `MemoryService` requires the backing RAM to be exactly 65536 bytes; it is
  constructed for you by `CommodoreMachine`.
- `LineDirector` (the raster-effect driver) writes registers via `Bus.Write`
  directly rather than through `WriteIo`. This is intentional: raster splits
  target I/O registers by design. If you build raster effects, follow that
  pattern through `LineProgram` (see the [API Reference](api-reference.md)).
