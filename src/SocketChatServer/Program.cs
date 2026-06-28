using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using SocketChatServer.ChatServices;

namespace SocketChatServer;

public class Program
{
    
    private static readonly ConnectionStore _connectionStore = new();
    private static readonly UserStore _userStore = new();
    private static readonly GroupStore _groupStore = new();
    private static readonly MessageBuffer _messageBuffer = new();
    
    private static MessageProcessor MessageProcessor;
    private static AuthService _authService;
    
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();
        _authService = new AuthService(_connectionStore, _userStore);
        MessageProcessor = new MessageProcessor(_connectionStore, _groupStore, _userStore, _messageBuffer);
        WebSocketOptions options = new()
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30),
        };
        app.UseWebSockets(options);
        app.Map("/auth", async context =>
        {
            await _authService.RegisterUser(context);
        });
        app.Map("logout", async context =>
        {
            await _authService.LogoutUser(context);
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

        _ = Task.Run(async () => await _connectionStore.CheckConnection());
        app.Run();
    }
    private static async Task Handle(WebSocket webSocket)
    {
        var rentedBytes = ArrayPool<byte>.Shared.Rent(1024*4);
        
        Guid id = Guid.Empty;
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {

                var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(rentedBytes),
                    CancellationToken.None);

                if (receiveResult.MessageType == WebSocketMessageType.Close) return;
                
                
                byte commandType = rentedBytes[0];
            
                if (id == Guid.Empty && commandType == 0)
                {
                    try
                    {
                        Span<byte> idSpan = rentedBytes.AsSpan(1, 16);
                        id = new Guid(idSpan);
                    }
                    catch (Exception ex)
                    {
                        continue;
                    }
                }
                if (id != Guid.Empty)
                {
                    await MessageProcessor.ProcessMessage(webSocket, rentedBytes, receiveResult, id);
                }
            }
        }
        catch (WebSocketException ex)
        {
            await _connectionStore.RemoveUnusedConnection(id);
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
}