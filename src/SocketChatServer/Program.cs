using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace SocketChatServer;


public class Program
{
    private static ConcurrentDictionary<Guid, (WebSocket, DateTime)> _connections = new();
    private static ConcurrentDictionary<string, Guid> _usersKeyName = new();
    private static ConcurrentDictionary<Guid, string> _usersKeyGuid = new();
    private static ConcurrentDictionary<string, ConcurrentBag<string>> _groups = new();
    private static ConcurrentDictionary<string, ConcurrentBag<byte[]>> _oldMessage = new();
    private const byte INITCOMMAND = 0;
    private const byte SENDMESSAGE = 1;
    private const byte ADDGROUP = 3;
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();
        WebSocketOptions options = new()
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30),
        };
        app.UseWebSockets(options);
        app.Map("/auth", async context =>
        {
            using StreamReader reader = new StreamReader(context.Request.Body);
            string name = await reader.ReadToEndAsync();
            Guid id = GenerateId();
            if (!TryAddUser(name, id))
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                return;
            }
            context.Response.ContentType = "application/octet-stream";
            context.Response.ContentLength = 16;
            Memory<byte> idMemory = context.Response.BodyWriter.GetMemory(16);
            bool success = id.TryWriteBytes(idMemory.Span);
            if (success)
            {
                context.Response.BodyWriter.Advance(16);
            }
            await context.Response.BodyWriter.FlushAsync();
        });
        app.Map("logout", async context =>
        {
            using StreamReader reader = new StreamReader(context.Request.Body);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(16);
            await reader.BaseStream.ReadAsync(buffer, 0, 16);
            Guid id = new Guid(buffer.AsSpan(0, 16));
            if (ContainsUsersGuid(id))
            {
                string name = GetUserName(id);
                _usersKeyGuid.TryRemove(id, out _);
                _usersKeyName.TryRemove(name, out _);
                await RemoveUnusedConnection(id);
            }
        });
        app.Map("/nf_chat", async context =>
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                using var websocket = await context.WebSockets.AcceptWebSocketAsync();
                await Handle(websocket);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        } );

        _ = Task.Run(async () => await ConnectionChecker());
        app.Run();
    }

    public static bool TryAddUser(string name, Guid id)
    {
        if (ContainsUsersName(name) || ContainsUsersGuid(id))
            return false;
        _usersKeyGuid[id] = name;
        _usersKeyName[name] = id;
        return true;
    }
    public static bool ContainsUsersName(string name)
    {
        return _usersKeyName.ContainsKey(name);
    }
    public static bool ContainsUsersGuid( Guid id)
    {
        return _usersKeyGuid.ContainsKey(id);
    }

    public static Guid GetUserId(string name)
    {
        if (ContainsUsersName(name))
            return _usersKeyName[name];
        return Guid.Empty;
    }
    public static string GetUserName(Guid id)
    {
        if (ContainsUsersGuid(id))
            return _usersKeyGuid[id];
        return string.Empty;
    }
    private static void SaveMessage(string address, byte[] buffer, int count)
    {
        byte[] frame = new byte[count];
        Array.Copy(buffer, frame, count);

        _oldMessage
            .GetOrAdd(address, _ => new ConcurrentBag<byte[]>())
            .Add(frame);
    }
    private static Guid GenerateId() =>  Guid.NewGuid();
    private static async Task Handle(WebSocket webSocket)
    {
        var rentedBytes = ArrayPool<byte>.Shared.Rent(1024*4);
        Guid id = Guid.Empty;
        try
        {
            
            FrameReader frameReader = new FrameReader(rentedBytes);
            while (webSocket.State == WebSocketState.Open)
            {
                var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(rentedBytes),
                    CancellationToken.None);
                if (receiveResult.MessageType == WebSocketMessageType.Close) break;
                if (rentedBytes[0] == INITCOMMAND)
                {
                    ReadOnlySpan<byte> idSpan = frameReader.GetId();
                    id = new Guid(idSpan);
                    if (!ContainsUsersGuid(id))
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.PolicyViolation,
                            "Unauthorized",
                            CancellationToken.None);
                        return;
                    }

                    var name = GetUserName(id);
                    if (name != String.Empty && _oldMessage.ContainsKey(name))
                    {
                        foreach (var message in _oldMessage[name])
                        {
                            await webSocket.SendAsync(
                                new ArraySegment<byte>(message, 0, message.Length),
                                receiveResult.MessageType,
                                receiveResult.EndOfMessage,
                                CancellationToken.None);
                        }
                    }
                }
                else if(rentedBytes[0] == SENDMESSAGE)
                {
                    if (receiveResult.Count < 18) continue;
                    ReadOnlySpan<byte> addressSpan = frameReader.GetAddressSee();
                    var addressLength = BinaryPrimitives.ReadInt32LittleEndian(frameReader.GetAddressLength());
                    string address = Encoding.UTF8.GetString(addressSpan.Slice(0, addressLength));
                    if(ContainsUsersName(address))
                    {
                        Guid addressId = GetUserId(address);
                        if(_connections.ContainsKey(addressId))
                        {
                            var ws = _connections[addressId].Item1;
                            if (ws.State == WebSocketState.Open)
                            {
                                try
                                {
                                    await ws.SendAsync(
                                        new ArraySegment<byte>(rentedBytes, 0, receiveResult.Count),
                                        receiveResult.MessageType,
                                        receiveResult.EndOfMessage,
                                        CancellationToken.None);
                                }
                                catch
                                {
                                    SaveMessage(address, rentedBytes, receiveResult.Count);
                                    await RemoveUnusedConnection(addressId);
                                }
                            }
                            else
                            {
                                SaveMessage(address, rentedBytes, receiveResult.Count);
                            }
                        }
                        else
                        {
                            SaveMessage(address, rentedBytes, receiveResult.Count);
                        }
                    }
                    else if (_groups.ContainsKey(address))
                    {
                        foreach (var userAddress in _groups.GetOrAdd(address, _ => new ConcurrentBag<string>()))
                        {
                            Guid addressId = GetUserId(userAddress);
                            var ws = _connections[addressId].Item1;
                            await ws.SendAsync(
                                new ArraySegment<byte>(rentedBytes, 0, receiveResult.Count),
                                receiveResult.MessageType,
                                receiveResult.EndOfMessage,
                                CancellationToken.None);
                        }
                    }
                }
                else if (rentedBytes[0] == ADDGROUP)
                {
                    Span<byte> groupNameLenSpan = rentedBytes.AsSpan().Slice(1, 4);
                    var groupNameLen = BinaryPrimitives.ReadInt32LittleEndian(groupNameLenSpan);
                    Span<byte> groupNameSpan = rentedBytes.AsSpan().Slice(5, groupNameLen);
                    string groupName = Encoding.UTF8.GetString(groupNameSpan);
                    
                    Span<byte> senderNameLenSpan = rentedBytes.AsSpan().Slice(5 + groupNameLen, 4);
                    var senderNameLen = BinaryPrimitives.ReadInt32LittleEndian(senderNameLenSpan);
                    Span<byte> senderNameSpan = rentedBytes.AsSpan().Slice(5 + groupNameLen + 4, senderNameLen);
                    string senderName = Encoding.UTF8.GetString(senderNameSpan);
                    
                    _groups.GetOrAdd(groupName, _ => new ConcurrentBag<string>())
                        .Add(senderName);
                }
                _connections[id] = (webSocket, DateTime.Now);
            }
        }
        catch (WebSocketException)
        {
            await RemoveUnusedConnection(id);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBytes);
        }
    }


    private static async Task ConnectionChecker()
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

    private static async Task RemoveUnusedConnection(Guid id)
    {
        if(_connections.TryGetValue(id, out var wsData))
        {
            var ws = wsData.Item1;
            if(ws.State != WebSocketState.Aborted)
            {
                await ws.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closing",
                    CancellationToken.None);
            }
            
            _connections.Remove(id, out wsData);
        }
    }
}