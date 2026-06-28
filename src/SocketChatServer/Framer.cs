namespace SocketChatServer;

//[CMD]-1  [ADDRESS_LEN]-4 [ADDRESS_ID]-N -- Sipher
public abstract class Framer
{
    protected byte[] _buffer;
    
    protected const int CmdOffset = 0;
    protected const int AddressLenOffset = 1;
    
}