namespace CbmEngine.Pipeline;

public static class CompiledMagic
{
    public const uint Charset = 0x53524843;  // 'CHRS'
    public const ushort CharsetVersion = 1;
    public const uint Tilemap = 0x504D5443;  // 'CTMP'
    public const ushort TilemapVersion = 1;
}

public sealed class CompiledCharset
{
    public required byte[] GlyphBytes { get; init; }
    public required CharMode[] ModeBySlot { get; init; }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(CompiledMagic.Charset);
        w.Write(CompiledMagic.CharsetVersion);
        w.Write((ushort)GlyphBytes.Length);
        w.Write((ushort)ModeBySlot.Length);
        w.Write(GlyphBytes);
        for (int i = 0; i < ModeBySlot.Length; i++) w.Write((byte)ModeBySlot[i]);
        return ms.ToArray();
    }

    public static CompiledCharset Deserialize(ReadOnlySpan<byte> bytes)
    {
        using var ms = new MemoryStream(bytes.ToArray());
        using var r = new BinaryReader(ms);
        uint magic = r.ReadUInt32();
        if (magic != CompiledMagic.Charset) throw new InvalidDataException($"Bad magic 0x{magic:X8}; expected charset.");
        ushort ver = r.ReadUInt16();
        if (ver != CompiledMagic.CharsetVersion) throw new InvalidDataException($"Charset version {ver} unsupported.");
        ushort glyphLen = r.ReadUInt16();
        ushort modeLen = r.ReadUInt16();
        var glyphs = r.ReadBytes(glyphLen);
        var modes = new CharMode[modeLen];
        for (int i = 0; i < modeLen; i++) modes[i] = (CharMode)r.ReadByte();
        return new CompiledCharset { GlyphBytes = glyphs, ModeBySlot = modes };
    }
}

public sealed class CompiledTilemap
{
    public required byte[] ScreenRam { get; init; }
    public required byte[] ColorRam { get; init; }
    public required (int RasterLine, byte D018Value)[] SplitTable { get; init; }
    public required byte[][] CharsetsPerBand { get; init; }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(CompiledMagic.Tilemap);
        w.Write(CompiledMagic.TilemapVersion);
        w.Write((ushort)ScreenRam.Length);
        w.Write(ScreenRam);
        w.Write((ushort)ColorRam.Length);
        w.Write(ColorRam);
        w.Write((ushort)SplitTable.Length);
        foreach (var (line, d018) in SplitTable) { w.Write((ushort)line); w.Write(d018); }
        w.Write((ushort)CharsetsPerBand.Length);
        foreach (var cs in CharsetsPerBand) { w.Write((ushort)cs.Length); w.Write(cs); }
        return ms.ToArray();
    }

    public static CompiledTilemap Deserialize(ReadOnlySpan<byte> bytes)
    {
        using var ms = new MemoryStream(bytes.ToArray());
        using var r = new BinaryReader(ms);
        if (r.ReadUInt32() != CompiledMagic.Tilemap) throw new InvalidDataException("Bad magic; expected tilemap.");
        if (r.ReadUInt16() != CompiledMagic.TilemapVersion) throw new InvalidDataException("Tilemap version unsupported.");
        ushort sLen = r.ReadUInt16(); var screen = r.ReadBytes(sLen);
        ushort cLen = r.ReadUInt16(); var color = r.ReadBytes(cLen);
        ushort splitCount = r.ReadUInt16();
        var splits = new (int, byte)[splitCount];
        for (int i = 0; i < splitCount; i++) splits[i] = (r.ReadUInt16(), r.ReadByte());
        ushort bandCount = r.ReadUInt16();
        var bands = new byte[bandCount][];
        for (int i = 0; i < bandCount; i++) { ushort len = r.ReadUInt16(); bands[i] = r.ReadBytes(len); }
        return new CompiledTilemap { ScreenRam = screen, ColorRam = color, SplitTable = splits, CharsetsPerBand = bands };
    }
}
