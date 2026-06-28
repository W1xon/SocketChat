using System.Buffers.Binary;

namespace SocketChatServer;
public class FrameReader : Framer
{
    public FrameReader(byte[] buffer)
    {
        _buffer = buffer;
    }
    public ReadOnlySpan<byte> GetCommandType()
    {
        return _buffer.AsSpan().Slice(CmdOffset, 1);
    }

    public ReadOnlySpan<byte> GetId()
    {
        return _buffer.AsSpan().Slice(1,16);
    }
    public ReadOnlySpan<byte> GetAddressSpan()
    {
        var addressLen = BinaryPrimitives.ReadInt32LittleEndian(GetAddressLength());
        
        return _buffer.AsSpan().Slice(AddressLenOffset + 4, addressLen);
    }
    public ReadOnlySpan<byte> GetAddressLength()
    {
        return _buffer.AsSpan().Slice(1, 4);
    }
}
