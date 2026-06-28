using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace SocketChatClient;

public class FrameWriter : Framer
{
    
    private int _offset;
    public FrameWriter(byte[] buffer)
    {
        _buffer = buffer;
        _offset = 0;
    }

    public FrameWriter AddCommandType(byte command)
    {
        _buffer[_offset++] = command;
        return this;
    }
    
    public FrameWriter AddKey()
    {
        Span<byte> keySpan = _buffer.AsSpan().Slice(_offset, 12);
        RandomNumberGenerator.Fill(keySpan);
        _offset += 12;
        return this;
    }

    public FrameWriter AddInt32(int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan().Slice(_offset, 4), value);
        _offset += 4;
        return this;
    }
    public FrameWriter AddString(string value)
    {
        int strLength = Encoding.UTF8.GetByteCount(value);
        Encoding.UTF8.GetBytes(value, _buffer.AsSpan().Slice(_offset, strLength));
        _offset += strLength;
        return this;
    }
    public FrameWriter AddData(ReadOnlySpan<byte> data)
    {
        data.CopyTo(_buffer.AsSpan().Slice(_offset));
        _offset += data.Length;
        return this;
    }
}