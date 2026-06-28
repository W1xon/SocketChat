using System.Buffers.Binary;
using System.Net.WebSockets;
using System.Text;
using SocketChatServer.ChatServices;

namespace SocketChatServer;

public class MessageProcessor
{
    
    private const byte INITCOMMAND = 0;
    private const byte SENDMESSAGE = 1;
    private const byte PING = 2;
    private const byte ADDGROUP = 3;
    
    private readonly ConnectionStore _connectionStore;
    private readonly UserStore _userStore;
    private readonly GroupStore _groupStore;
    private readonly MessageBuffer _messageBuffer;
    
    
    public MessageProcessor(ConnectionStore connectionStore, GroupStore groupStore, UserStore userStore, MessageBuffer messageBuffer)
    {
        _connectionStore = connectionStore;
        _groupStore = groupStore;
        _userStore = userStore;
        _messageBuffer = messageBuffer;
    }
    
    public async Task ProcessMessage(WebSocket socket, byte[] buffer, WebSocketReceiveResult receiveResult , Guid id)
    {
        var frameReader = new FrameReader(buffer);
        byte commandType = frameReader.GetCommandType()[0];
        switch (commandType)
        {
            case INITCOMMAND:
                await ProcessInitCommand(socket, id);
                break;
            case SENDMESSAGE:
                await ProcessSendMessage(frameReader, receiveResult, buffer);
                break;
            case PING:
                break;
            case ADDGROUP:
                await ProcessAddGroup(socket, buffer);
                break;
            default:
                Console.WriteLine("Unknown command type");
                break;
        }
        
        _connectionStore.UpdateConnection(socket, id);
    }
    private async Task ProcessInitCommand(WebSocket socket, Guid id)
    {
        if (!_userStore.ContainsUserId(id))
        {
            await socket.CloseAsync(
                WebSocketCloseStatus.PolicyViolation,
                "Unauthorized",
                CancellationToken.None);
            return;
        }

        await _messageBuffer.SendOldMessage(socket, id);

    }
    private async Task ProcessSendMessage(FrameReader frameReader, WebSocketReceiveResult receiveResult, byte[] rentedBytes)
    {
        if (receiveResult.Count < 18) return;
        ReadOnlySpan<byte> addressSpan = frameReader.GetAddressSpan();
        var addressLength = BinaryPrimitives.ReadInt32LittleEndian(frameReader.GetAddressLength());
        string address = Encoding.UTF8.GetString(addressSpan.Slice(0, addressLength));
        
        if(_userStore.ContainsUserName(address))
            await SendMessage(address, rentedBytes, receiveResult);
        else 
            await BroadcastMessage(address, rentedBytes, receiveResult);
    }
    private async Task SendMessage(string address, byte[] message, WebSocketReceiveResult receiveResult)
    {
        if (!_userStore.ContainsUserName(address)) return;
        
        Guid addressId = _userStore.GetUserId(address);
        if (!_userStore.ContainsUserId(addressId) || !_connectionStore.TryGetWebSocket(addressId, out var ws) || ws.State != WebSocketState.Open)
        { 
            _messageBuffer.SaveMessage(addressId, message, receiveResult.Count);
            return;
        }
        try
        {
            await ws.SendAsync(
                new ArraySegment<byte>(message, 0, receiveResult.Count),
                receiveResult.MessageType,
                receiveResult.EndOfMessage,
                CancellationToken.None);
        }
        catch
        {
            _messageBuffer.SaveMessage(addressId, message, receiveResult.Count);
            await _connectionStore.RemoveUnusedConnection(addressId);
        }
    }
    
    private async Task ProcessAddGroup(WebSocket socket, byte[] rentedBytes)
    {
        Span<byte> groupNameLenSpan = rentedBytes.AsSpan().Slice(1, 4);
        var groupNameLen = BinaryPrimitives.ReadInt32LittleEndian(groupNameLenSpan);
        Span<byte> groupNameSpan = rentedBytes.AsSpan().Slice(5, groupNameLen);
        string groupName = Encoding.UTF8.GetString(groupNameSpan);
                    
        Span<byte> senderNameLenSpan = rentedBytes.AsSpan().Slice(5 + groupNameLen, 4);
        var senderNameLen = BinaryPrimitives.ReadInt32LittleEndian(senderNameLenSpan);
        Span<byte> senderNameSpan = rentedBytes.AsSpan().Slice(5 + groupNameLen + 4, senderNameLen);
        var senderName = Encoding.UTF8.GetString(senderNameSpan);
        _groupStore.AddGroup(groupName, senderName);
    }
    private async Task BroadcastMessage(string groupName, byte[] message, WebSocketReceiveResult receiveResult)
    {
        foreach (var userAddress in _groupStore.GetGroupMembers(groupName))
        {
            await SendMessage(userAddress, message, receiveResult);
        }
    }
}