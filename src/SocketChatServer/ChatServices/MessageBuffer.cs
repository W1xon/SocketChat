using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace SocketChatServer.ChatServices;

public class MessageBuffer
{
    
    private ConcurrentDictionary<Guid, ConcurrentBag<byte[]>> _oldMessage = new();
    
    public async Task SendOldMessage(WebSocket webSocket, Guid id)
    {
        if (id != null && _oldMessage.TryRemove(id, out var messages))
        {
            foreach (var message in messages)
            {
                try
                {
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(message, 0, message.Length),
                        WebSocketMessageType.Binary,
                        true,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending old message: {ex.Message}");
                }
            }
        }
    }
    
    
    public void SaveMessage(Guid id, byte[] buffer, int count)
    {
        byte[] frame = new byte[count];
        Array.Copy(buffer, frame, count);

        _oldMessage
            .GetOrAdd(id, _ => new ConcurrentBag<byte[]>())
            .Add(frame);
    }
}