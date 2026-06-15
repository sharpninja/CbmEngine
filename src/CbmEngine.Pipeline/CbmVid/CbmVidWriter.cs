using System.Buffers.Binary;

namespace CbmEngine.Pipeline.CbmVid;

public sealed class CbmVidWriter : IDisposable
{
    private readonly Stream _output;
    private readonly bool _leaveOpen;
    private readonly long _headerOffset;
    private CbmVidHeader _header;
    private uint _written;

    public CbmVidWriter(Stream output, CbmVidHeader header, bool leaveOpen = false)
    {
        if (output is null) throw new ArgumentNullException(nameof(output));
        if (!output.CanWrite) throw new ArgumentException("Stream must be writable.", nameof(output));
        if (header.Width != 320 || header.Height != 200) throw new ArgumentException("Only 320x200 is supported.", nameof(header));
        _output = output;
        _leaveOpen = leaveOpen;
        _headerOffset = output.CanSeek ? output.Position : 0;
        _header = header;
        WriteHeader(header);
    }

    public void WriteFrame(EncodedSplashBitmap frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Bitmap.Length != EncodedSplashBitmap.BitmapByteSize) throw new ArgumentException("Bitmap size mismatch.", nameof(frame));
        if (frame.ScreenRam.Length != EncodedSplashBitmap.ScreenRamSize) throw new ArgumentException("ScreenRam size mismatch.", nameof(frame));
        if (frame.ColorRam.Length != EncodedSplashBitmap.ColorRamSize) throw new ArgumentException("ColorRam size mismatch.", nameof(frame));

        Span<byte> record = stackalloc byte[CbmVidFormat.FrameRecordSize];
        record[0] = (byte)(frame.Mode == SplashBitmapMode.HiRes ? CbmVidFrameMode.HiRes : CbmVidFrameMode.Multicolor);
        record[1] = frame.BackgroundColorIndex;
        frame.Bitmap.AsSpan().CopyTo(record[CbmVidFormat.FrameBitmapOffset..]);
        frame.ScreenRam.AsSpan().CopyTo(record[CbmVidFormat.FrameScreenOffset..]);
        frame.ColorRam.AsSpan().CopyTo(record[CbmVidFormat.FrameColorOffset..]);
        _output.Write(record);
        _written++;
    }

    public void FinalizeFrameCount()
    {
        if (!_output.CanSeek) return;
        if (_written == _header.FrameCount) return;
        var snapshot = _output.Position;
        _output.Position = _headerOffset + 16;
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, _written);
        _output.Write(buf);
        _output.Position = snapshot;
        _header = _header with { FrameCount = _written };
    }

    private void WriteHeader(CbmVidHeader header)
    {
        Span<byte> buf = stackalloc byte[CbmVidFormat.HeaderSize];
        CbmVidFormat.Magic.CopyTo(buf);
        buf[7] = CbmVidFormat.Version;
        BinaryPrimitives.WriteUInt16LittleEndian(buf[8..], header.Width);
        BinaryPrimitives.WriteUInt16LittleEndian(buf[10..], header.Height);
        BinaryPrimitives.WriteUInt16LittleEndian(buf[12..], header.FrameRate);
        BinaryPrimitives.WriteUInt16LittleEndian(buf[14..], 0);
        BinaryPrimitives.WriteUInt32LittleEndian(buf[16..], header.FrameCount);
        buf[20] = (byte)header.DefaultMode;
        buf[21] = header.Flags;
        BinaryPrimitives.WriteUInt16LittleEndian(buf[22..], (ushort)CbmVidFormat.FrameRecordSize);
        _output.Write(buf);
    }

    public void Dispose()
    {
        FinalizeFrameCount();
        if (!_leaveOpen) _output.Dispose();
    }
}
