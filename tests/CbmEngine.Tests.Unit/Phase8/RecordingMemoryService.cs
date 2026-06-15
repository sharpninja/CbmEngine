using CbmEngine.Abstractions;

namespace CbmEngine.Tests.Unit.Phase8;

internal sealed class RecordingMemoryService : IMemoryService
{
    public List<(ushort Address, byte[] Payload)> Ranges { get; } = new();
    public List<(ushort Address, byte Value)> IoWrites { get; } = new();

    public IReadOnlyList<(ushort Address, int Length)> RangeWrites =>
        Ranges.Select(r => (r.Address, r.Payload.Length)).ToList();

    public void WriteRange(ushort address, ReadOnlySpan<byte> source) =>
        Ranges.Add((address, source.ToArray()));

    public void WriteIo(ushort address, ReadOnlySpan<byte> source)
    {
        for (int i = 0; i < source.Length; i++) IoWrites.Add(((ushort)(address + i), source[i]));
    }

    public byte ReadIo(ushort address) => 0;
    public bool IsIoAddress(ushort address) => address is >= 0xD000 and < 0xE000 && (address < 0xD800 || address >= 0xDC00);
    public ReadOnlySpan<byte> Snapshot() => ReadOnlySpan<byte>.Empty;
    public ReadOnlySpan<byte> SnapshotRange(ushort address, int length) => ReadOnlySpan<byte>.Empty;
    public Span<byte> View(ushort address, int length) => Span<byte>.Empty;
    public IReadOnlyList<MemoryRangeHandle> ViewMany(IReadOnlyList<(ushort address, int length)> ranges) =>
        ranges.Select(r => new MemoryRangeHandle(r.address, r.length)).ToList();
    public Span<byte> Materialize(MemoryRangeHandle handle) => Span<byte>.Empty;
}
