namespace CbmEngine.Systems.Audio;

public sealed record PsidHeader(
    string Magic,
    ushort Version,
    ushort DataOffset,
    ushort LoadAddress,
    ushort InitAddress,
    ushort PlayAddress,
    ushort SongCount,
    ushort StartSong,
    uint SpeedFlags,
    string Name,
    string Author,
    string Released);

public sealed record PsidProgram(PsidHeader Header, ReadOnlyMemory<byte> Payload);
