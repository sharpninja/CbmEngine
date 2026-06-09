using CbmEngine.Abstractions;

namespace CbmEngine.Systems.Services;

public sealed class SpriteService
{
    private readonly IMemoryService _memory;

    public SpriteService(IMemoryService memory)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
    }

    public void SetPosition(int slot, int x, int y)
    {
        EnsureSlot(slot);
        if (x < 0 || x > 511) throw new ArgumentOutOfRangeException(nameof(x));
        if (y < 0 || y > 255) throw new ArgumentOutOfRangeException(nameof(y));

        _memory.WriteIo((ushort)(0xD000 + slot * 2), new[] { (byte)(x & 0xFF) });
        _memory.WriteIo((ushort)(0xD001 + slot * 2), new[] { (byte)y });

        byte msbReg = _memory.ReadIo(0xD010);
        byte mask = (byte)(1 << slot);
        byte updated = x > 255 ? (byte)(msbReg | mask) : (byte)(msbReg & ~mask);
        _memory.WriteIo(0xD010, new[] { updated });
    }

    public void SetEnabled(int slot, bool enabled)
    {
        EnsureSlot(slot);
        byte e = _memory.ReadIo(0xD015);
        byte mask = (byte)(1 << slot);
        byte updated = enabled ? (byte)(e | mask) : (byte)(e & ~mask);
        _memory.WriteIo(0xD015, new[] { updated });
    }

    public void SetColor(int slot, byte color)
    {
        EnsureSlot(slot);
        _memory.WriteIo((ushort)(0xD027 + slot), new[] { (byte)(color & 0x0F) });
    }

    public void SetPointer(int slot, byte spriteBlockIndex)
    {
        EnsureSlot(slot);
        ushort screenRamPointersBase = 0x07F8;
        var span = _memory.View(screenRamPointersBase, 8);
        span[slot] = spriteBlockIndex;
    }

    private static void EnsureSlot(int slot)
    {
        if (slot < 0 || slot > 7) throw new ArgumentOutOfRangeException(nameof(slot), "Sprite slot must be 0..7.");
    }
}
