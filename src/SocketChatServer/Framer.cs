namespace SocketChatServer;

//[CMD]-1 [ADDRESS_ID]-16 [ADDRESS_LEN]-4 [KEY]- 12 [TEXT_LEN]-4 [TEXT]-N [NAME_LEN]-4 [NAME]-16
public abstract class Framer
{
    protected byte[] _buffer;
    
    protected const int CmdOffset = 0;
    protected const int AdddressLenOffset = 1;
    protected const int KeyLength = 12;
    
}