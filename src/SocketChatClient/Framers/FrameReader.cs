namespace SocketChatClient;

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

    public ReadOnlySpan<byte> GetAddressSee()
    {
        var addressLen = GetAddressLength();
        return _buffer.AsSpan().Slice(AddressOffset + 4, addressLen);
    }
    public ReadOnlySpan<byte> GetKey()
    {
        return  _buffer.AsSpan().Slice(GetKeyOffset(), 12);
    }
    public ReadOnlySpan<byte> GetText()
    {
        int textLength = GetTextLength() ;
        return _buffer.AsSpan(GetMessageOffset(), textLength);
    }
    
    public ReadOnlySpan<byte> GetSenderName()
    {
        int textLength = GetTextLength();
        int nameLength = GetNameLength();
        
        var senderPos = GetMessageOffset() + textLength + 4;
        return _buffer.AsSpan(senderPos, nameLength);
    }
}
