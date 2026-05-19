namespace SocketChatClient;

public class Crypter
{
    public static void Execute(Span<byte> msg, ReadOnlySpan<byte> key)
    {
        for (int i = 0; i < msg.Length; i++)
        {
            msg[i] = (byte)(msg[i] ^ key[i % key.Length]);
        }
    }
}