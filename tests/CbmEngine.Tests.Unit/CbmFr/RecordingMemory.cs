using CbmEngine.Abstractions;

namespace CbmEngine.Tests.Unit.CbmFr;

/// <summary>
/// Hand-written <see cref="IMemoryService"/> test double for the CBMFR feature work.
/// Moq cannot mock the Span-based members, so this records RAM ranges and IO byte writes
/// and supports <see cref="ReadIo"/> read-back for register assertions.
/// </summary>
internal sealed class RecordingMemory : IMemoryService
{
    private readonly byte[] _ram = new byte[0x10000];
    private readonly Dictionary<ushort, byte> _io = new();

    public List<(ushort Address, byte[] Payload)> Ranges { get; } = new();
    public List<(ushort Address, byte Value)> IoWrites { get; } = new();

    public IReadOnlyList<(ushort Address, int Length)> RangeWrites =>
        Ranges.Select(r => (r.Address, r.Payload.Length)).ToList();

    public byte[] Ram => _ram;

    public void WriteRange(ushort address, ReadOnlySpan<byte> source)
    {
        Ranges.Add((address, source.ToArray()));
        source.CopyTo(_ram.AsSpan(address));
    }

    public void WriteIo(ushort address, ReadOnlySpan<byte> source)
    {
        for (int i = 0; i < source.Length; i++)
        {
            var a = (ushort)(address + i);
            _io[a] = source[i];
            IoWrites.Add((a, source[i]));
        }
    }

    public byte ReadIo(ushort address) => _io.TryGetValue(address, out var v) ? v : (byte)0;
    public bool IsIoAddress(ushort address) => address is >= 0xD000 and < 0xE000;
    public ReadOnlySpan<byte> Snapshot() => _ram;
    public ReadOnlySpan<byte> SnapshotRange(ushort address, int length) => _ram.AsSpan(address, length);
    public Span<byte> View(ushort address, int length) => _ram.AsSpan(address, length);
    public IReadOnlyList<MemoryRangeHandle> ViewMany(IReadOnlyList<(ushort address, int length)> ranges) =>
        ranges.Select(r => new MemoryRangeHandle(r.address, r.length)).ToList();
    public Span<byte> Materialize(MemoryRangeHandle handle) => _ram.AsSpan(handle.Address, handle.Length);
}
