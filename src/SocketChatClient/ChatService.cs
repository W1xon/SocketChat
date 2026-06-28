using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace SocketChatClient;

public class ChatService
{
    public WebSocketState State => _webSocket.State;
    private const byte INITCOMMAND = 0;
    private const byte SENDMESSAGE = 1;
    private const byte PING = 2;
    private const byte ADDGROUP = 3;

    private static DateTime _lastPing;
    private readonly Uri ChatUri = new Uri("ws://127.0.0.1:5000/nf_chat");
    private ClientWebSocket _webSocket;
    public ConcurrentDictionary<string, List<string>> History;
    public event Action<string, string?> UpdateChats;
    public ChatService()
    {
        History = new();
        _webSocket = new ClientWebSocket();
    }
    public async Task ConnectAsync()
    {
        await _webSocket.ConnectAsync(ChatUri, default);
    }
    public  async Task SendAuth(Guid id)
    {
        var totalSize = 1 + 16;
        byte[] rented = ArrayPool<byte>.Shared.Rent(totalSize);
        try
        {
            rented[0] = INITCOMMAND;
            Span<byte> idSpan = rented.AsSpan().Slice(1, 16);
            id.TryWriteBytes(idSpan);
            await _webSocket.SendAsync(new ArraySegment<byte>(rented),WebSocketMessageType.Binary, true, CancellationToken.None);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
    public async Task Send(string text, byte[] name, byte[] address)
    {
        var textSize = Encoding.UTF8.GetByteCount(text);
        var nameSize = name.Length;

        var totalSize =
            1 + 16 + 4 + 12
            + 4 + textSize
            + 4 + nameSize;
        byte[] rented = ArrayPool<byte>.Shared.Rent(totalSize);
        try
        {
            FrameWriter writer = new FrameWriter(rented);
            writer.AddCommandType(SENDMESSAGE)
                .AddInt32(address.Length)
                .AddData(address)
                .AddKey()
                .AddInt32(textSize)
                .AddString(text)
                .AddInt32(nameSize)
                .AddData(name);
            
            Crypter.Execute(writer.GetMessage(), writer.GetKey());
            await _webSocket.SendAsync(new ArraySegment<byte>(rented, 0, totalSize), WebSocketMessageType.Binary, true, default);
        }
        finally { ArrayPool<byte>.Shared.Return(rented); }
    }

    public async Task AddOrJoinToGroup(string groupName, byte[] name)
    {
        int nameLen = Encoding.UTF8.GetByteCount(groupName);
        var totalSize = 1 + 4 + nameLen + 4 + name.Length;
        byte[] rented = ArrayPool<byte>.Shared.Rent(totalSize);
        try
        {
            rented[0] = ADDGROUP;
            FrameWriter frameWriter = new FrameWriter(rented);
            frameWriter.AddCommandType(ADDGROUP)
                .AddInt32(nameLen)
                .AddString(groupName)
                .AddInt32(name.Length)
                .AddData(name);
            await _webSocket.SendAsync(new ArraySegment<byte>(rented, 0, totalSize),WebSocketMessageType.Binary, true, CancellationToken.None);
        }
        finally
        {
            History.GetOrAdd(groupName, _ => new List<string>()).Add("Вы присоединились");
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
    public async Task ReceiveLoop()
    {
        var bytes = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            while (_webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(bytes), default);
                byte command = bytes[0];

                if (command != SENDMESSAGE
                    || result.Count < 13)
                    continue;
                FrameReader frameReader = new FrameReader(bytes[..result.Count]);

                Crypter.Execute(frameReader.GetMessage(), frameReader.GetKey());
                AddToHistory(frameReader);
            }
        }
        finally { ArrayPool<byte>.Shared.Return(bytes); }
    }

    private void AddToHistory(FrameReader frameReader)
    {
        string sender = Encoding.UTF8.GetString(frameReader.GetSenderName());
        string address = Encoding.UTF8.GetString(frameReader.GetAddressSee());
        string text = Encoding.UTF8.GetString(frameReader.GetText());
        if (History.ContainsKey(address))
        {
            //собщение из группы, поэтмоу отправитель это адрес группы
            History.GetOrAdd(address, _ => new List<string>()).Add(text);
            UpdateChats.Invoke(address, sender);
        }
        else
        {
            History.GetOrAdd(sender, _ => new List<string>()).Add(text);
            UpdateChats.Invoke(sender, null);
        }
    }
    public  async Task Ping()
    {
        _lastPing = DateTime.Now;
        while (_webSocket.State == WebSocketState.Open)
        {
            await Task.Delay(5000);
            if (DateTime.Now - _lastPing > TimeSpan.FromSeconds(20))
            {
                await _webSocket.SendAsync(new ArraySegment<byte>([PING]), WebSocketMessageType.Binary, true, CancellationToken.None);
                _lastPing = DateTime.Now;
            }
        }
    }

    public async Task Close()
    {
        _webSocket.Abort();
    }
}