using CbmEngine.Abstractions;
using ViceSharp.Abstractions;

namespace CbmEngine.Systems.Memory;

public sealed class MemoryService : IMemoryService
{
    private static readonly (ushort lo, ushort hi)[] IoRanges =
    {
        (0xD000, 0xD3FF),
        (0xD400, 0xD7FF),
        (0xD800, 0xDBFF),
        (0xDC00, 0xDCFF),
        (0xDD00, 0xDDFF),
    };

    private readonly IMemory _ram;
    private readonly IBus _bus;

    public MemoryService(IMemory systemRam, IBus bus)
    {
        _ram = systemRam ?? throw new ArgumentNullException(nameof(systemRam));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        if (_ram.Span.Length != 65536)
            throw new ArgumentException($"System RAM expected 65536 bytes, got {_ram.Span.Length}.", nameof(systemRam));
    }

    public bool IsIoAddress(ushort address)
    {
        for (int i = 0; i < IoRanges.Length; i++)
            if (address >= IoRanges[i].lo && address <= IoRanges[i].hi) return true;
        return false;
    }

    public ReadOnlySpan<byte> Snapshot() => _ram.Span;

    public ReadOnlySpan<byte> SnapshotRange(ushort address, int length)
    {
        ValidateRange(address, length, requireNonIo: false);
        return _ram.Span.Slice(address, length);
    }

    public Span<byte> View(ushort address, int length)
    {
        ValidateRange(address, length, requireNonIo: true);
        return _ram.Span.Slice(address, length);
    }

    public IReadOnlyList<MemoryRangeHandle> ViewMany(IReadOnlyList<(ushort address, int length)> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var handles = new MemoryRangeHandle[ranges.Count];
        for (int i = 0; i < ranges.Count; i++)
        {
            ValidateRange(ranges[i].address, ranges[i].length, requireNonIo: true);
            handles[i] = new MemoryRangeHandle(ranges[i].address, ranges[i].length);
        }
        return handles;
    }

    public Span<byte> Materialize(MemoryRangeHandle handle)
    {
        ValidateRange(handle.Address, handle.Length, requireNonIo: true);
        return _ram.Span.Slice(handle.Address, handle.Length);
    }

    public void WriteRange(ushort address, ReadOnlySpan<byte> source)
    {
        ValidateRange(address, source.Length, requireNonIo: true);
        source.CopyTo(_ram.Span.Slice(address, source.Length));
    }

    public void WriteIo(ushort address, ReadOnlySpan<byte> source)
    {
        if (source.Length == 0) return;
        int end = address + source.Length - 1;
        if (end > 0xFFFF) throw new ArgumentOutOfRangeException(nameof(source), "Write extends past end of address space.");
        for (int i = 0; i < source.Length; i++)
            _bus.Write((ushort)(address + i), source[i]);
    }

    public byte ReadIo(ushort address) => _bus.Read(address);

    private void ValidateRange(ushort address, int length, bool requireNonIo)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (length == 0) return;
        long end = (long)address + length - 1;
        if (end > 0xFFFF)
            throw new ArgumentOutOfRangeException(nameof(length), $"Range [${address:X4}..${end:X4}] extends past $FFFF.");
        if (requireNonIo)
        {
            for (int i = 0; i < IoRanges.Length; i++)
            {
                var (lo, hi) = IoRanges[i];
                if (address <= hi && end >= lo)
                    throw new InvalidOperationException($"Range [${address:X4}..${end:X4}] overlaps IO range [${lo:X4}..${hi:X4}]. Use WriteIo or ReadIo instead.");
            }
        }
    }
}
