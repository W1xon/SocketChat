using System.Buffers.Binary;

namespace SocketChatClient;

//[CMD]-1  [ADDRESS_LEN]-4 [ADDRESS_ID]-N [KEY]- 12 [TEXT_LEN]-4 [TEXT]-N [NAME_LEN]-4 [NAME]-N
public abstract class Framer
{
    protected byte[] _buffer;
    protected const int CmdOffset = 0;
    protected const int AddressOffset = 1;
    protected const int KeyLength = 12;
    

    public Span<byte> GetMessage()
    {
        return _buffer.AsSpan(GetMessageOffset());
    }
    public ReadOnlySpan<byte> GetKey()
    {
        return _buffer.AsSpan(GetKeyOffset(), KeyLength);
    }
    public int GetTextLength()
    {
        return BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(GetTextLenOffset(), 4));
    }
    
    public int GetNameLength()
    {
        var textLength = GetTextLength();
        int messageOffset = GetMessageOffset();
        var nameLenPos = messageOffset + textLength;
        return BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(nameLenPos, 4));
    }

    public int GetAddressLength()
    {
        return BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan().Slice(AddressOffset, 4));
    }
    public int GetKeyOffset()
    {
        return AddressOffset + 4 + GetAddressLength();
    }

    public int GetTextLenOffset()
    {
        return GetKeyOffset() + KeyLength;
    }

    public int GetMessageOffset()
    {
        return GetTextLenOffset() + 4;
    }
}