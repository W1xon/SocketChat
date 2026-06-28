using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace SocketChatServer.ChatServices;

public class ConnectionStore
{
    private ConcurrentDictionary<Guid, (WebSocket, DateTime)> _connections = new();
    
    public bool TryGetWebSocket(Guid id, out WebSocket webSocket)
    {
        if (_connections.ContainsKey(id))
        {
            webSocket = _connections[id].Item1;
            return true;
        }
        webSocket = null;
        return false;
    }
    
    
    public async Task CheckConnection()
    {
        while (true)
        {
            foreach( var id in _connections.Keys.ToArray())
            {
                var time = _connections[id].Item2;
                if (DateTime.Now - time > TimeSpan.FromSeconds(30))
                {
                    await RemoveUnusedConnection(id);
                }
            }
            await Task.Delay(5000);
        }
    }
    

    public void UpdateConnection(WebSocket webSocket, Guid id)
    {
        _connections[id] = (webSocket, DateTime.Now);
    }
    
    public async Task RemoveUnusedConnection(Guid id)
    {
        if(_connections.TryRemove(id, out var wsData))
        {
            var ws = wsData.Item1;
            if(ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
            {
                try
                {
                    await ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error closing connection: {ex.Message}");
                }
            }
        
            try
            {
                ws?.Dispose();
            }
            catch { }
        }
    }
}