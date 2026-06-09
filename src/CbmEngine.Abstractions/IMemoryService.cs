namespace CbmEngine.Abstractions;

public readonly record struct MemoryRangeHandle(ushort Address, int Length);

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
