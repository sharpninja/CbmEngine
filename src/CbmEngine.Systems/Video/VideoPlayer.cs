using System.Buffers.Binary;
using CbmEngine.Abstractions;
using CbmEngine.Pipeline;
using CbmEngine.Pipeline.CbmVid;

namespace CbmEngine.Systems.Video;

public sealed class VideoPlayer : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly long _frameDataStart;
    private readonly byte[] _scratch = new byte[CbmVidFormat.FrameRecordSize];
    private readonly BitmapFramePump _pump = new();
    private CbmVidHeader _header;
    private int _currentFrame;

    public VideoPlayer(Stream stream, bool leaveOpen = false)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(stream));
        _stream = stream;
        _leaveOpen = leaveOpen;
        _header = ReadHeader();
        _frameDataStart = stream.CanSeek ? stream.Position : CbmVidFormat.HeaderSize;
    }

    public CbmVidHeader Header => _header;
    public int CurrentFrame => _currentFrame;
    public bool Loop { get; set; }
    public bool IsFinished => _currentFrame >= (int)_header.FrameCount;

    public void Reset()
    {
        if (!_stream.CanSeek) throw new InvalidOperationException("Underlying stream does not support seeking.");
        _stream.Position = _frameDataStart;
        _currentFrame = 0;
        _pump.Reset();
    }

    public void Seek(int frameIndex)
    {
        if (!_stream.CanSeek) throw new InvalidOperationException("Underlying stream does not support seeking.");
        if (frameIndex < 0 || frameIndex >= (int)_header.FrameCount) throw new ArgumentOutOfRangeException(nameof(frameIndex));
        _stream.Position = _frameDataStart + (long)frameIndex * CbmVidFormat.FrameRecordSize;
        _currentFrame = frameIndex;
    }

    public EncodedSplashBitmap PeekFrame(int frameIndex)
    {
        if (!_stream.CanSeek) throw new InvalidOperationException("PeekFrame requires a seekable stream.");
        var saved = _stream.Position;
        try
        {
            _stream.Position = _frameDataStart + (long)frameIndex * CbmVidFormat.FrameRecordSize;
            ReadRecordExact(frameIndex);
            return RecordToEncodedSplash(_scratch);
        }
        finally { _stream.Position = saved; }
    }

    public EncodedSplashBitmap PeekFrame0AsSplash()
    {
        var frame0 = PeekFrame(0);
        return frame0;
    }

    public bool PumpFrame(IMemoryService memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        if (IsFinished)
        {
            if (!Loop) return false;
            Reset();
        }

        ReadRecordExact(_currentFrame);
        var mode = _scratch[0] == (byte)CbmVidFrameMode.HiRes ? SplashBitmapMode.HiRes : SplashBitmapMode.Multicolor;
        _pump.Pump(
            memory,
            mode,
            _scratch.AsSpan(CbmVidFormat.FrameBitmapOffset, CbmVidFormat.FrameBitmapSize),
            _scratch.AsSpan(CbmVidFormat.FrameScreenOffset, CbmVidFormat.FrameScreenSize),
            _scratch.AsSpan(CbmVidFormat.FrameColorOffset, CbmVidFormat.FrameColorSize));
        _currentFrame++;
        return true;
    }

    public void Dispose()
    {
        if (!_leaveOpen) _stream.Dispose();
    }

    public static CbmVidHeader Validate(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var player = new VideoPlayer(stream, leaveOpen: true);
        var hdr = player.Header;
        if (!stream.CanSeek) return hdr;
        long expectedLength = CbmVidFormat.HeaderSize + (long)hdr.FrameCount * CbmVidFormat.FrameRecordSize;
        if (stream.Length != expectedLength)
            throw new InvalidDataException(
                $".cbmvid size {stream.Length} does not match header (frameCount={hdr.FrameCount}, expected length={expectedLength}).");
        return hdr;
    }

    public static CbmVidHeader ValidateFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var fs = File.OpenRead(path);
        try { return Validate(fs); }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            throw new InvalidDataException($"{path}: {ex.Message}", ex);
        }
    }

    private CbmVidHeader ReadHeader()
    {
        Span<byte> buf = stackalloc byte[CbmVidFormat.HeaderSize];
        _stream.ReadExactly(buf);
        for (int i = 0; i < CbmVidFormat.Magic.Length; i++)
            if (buf[i] != CbmVidFormat.Magic[i]) throw new InvalidDataException("Not a .cbmvid stream: magic mismatch.");
        byte version = buf[7];
        if (version > CbmVidFormat.Version) throw new NotSupportedException($"Unsupported .cbmvid version {version}; max supported is {CbmVidFormat.Version}.");
        ushort width = BinaryPrimitives.ReadUInt16LittleEndian(buf[8..]);
        ushort height = BinaryPrimitives.ReadUInt16LittleEndian(buf[10..]);
        ushort frameRate = BinaryPrimitives.ReadUInt16LittleEndian(buf[12..]);
        uint frameCount = BinaryPrimitives.ReadUInt32LittleEndian(buf[16..]);
        byte defaultMode = buf[20];
        byte flags = buf[21];
        ushort recordSize = BinaryPrimitives.ReadUInt16LittleEndian(buf[22..]);
        if (recordSize != CbmVidFormat.FrameRecordSize)
            throw new InvalidDataException($"Unexpected FrameRecordSize {recordSize}; expected {CbmVidFormat.FrameRecordSize}.");
        return new CbmVidHeader(width, height, frameRate, frameCount, (CbmVidFrameMode)defaultMode, flags);
    }

    private void ReadRecordExact(int frameIndexForError)
    {
        int total = 0;
        while (total < CbmVidFormat.FrameRecordSize)
        {
            int n = _stream.Read(_scratch.AsSpan(total));
            if (n <= 0)
                throw new InvalidDataException(
                    $"frame {frameIndexForError} truncated: got {total} of {CbmVidFormat.FrameRecordSize} bytes");
            total += n;
        }
    }

    private static EncodedSplashBitmap RecordToEncodedSplash(byte[] record)
    {
        var mode = record[0] == (byte)CbmVidFrameMode.HiRes ? SplashBitmapMode.HiRes : SplashBitmapMode.Multicolor;
        byte bg = record[1];
        var bitmap = new byte[EncodedSplashBitmap.BitmapByteSize];
        var screen = new byte[EncodedSplashBitmap.ScreenRamSize];
        var color = new byte[EncodedSplashBitmap.ColorRamSize];
        record.AsSpan(CbmVidFormat.FrameBitmapOffset, CbmVidFormat.FrameBitmapSize).CopyTo(bitmap);
        record.AsSpan(CbmVidFormat.FrameScreenOffset, CbmVidFormat.FrameScreenSize).CopyTo(screen);
        record.AsSpan(CbmVidFormat.FrameColorOffset, CbmVidFormat.FrameColorSize).CopyTo(color);
        return new EncodedSplashBitmap(mode, bg, bitmap, screen, color);
    }
}
