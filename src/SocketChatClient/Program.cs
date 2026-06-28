using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace SocketChatClient;

class Program
{
    private static string Name = "";
    private static Guid Id;
    private static AuthenticationService AuthenticationService = new();
    
    static async Task Main(string[] args)
    {
        await LogIn();
        Console.ResetColor();

        ChatService chat = new ChatService();
        await chat.ConnectAsync();
        await chat.SendAuth(Id);
        
        _ = Task.Run(() => chat.ReceiveLoop());
        _ = Task.Run(() => chat.Ping());

        StateMashine.Activate(new MenuState(chat, Name));
        while (chat.State == WebSocketState.Open)
        {
            await StateMashine.ActiveScene.Update();
        }
        await AuthenticationService.Logout(Id);
    }

    private static async Task LogIn()
    {
        do
        {
            Console.ResetColor();
            Console.Write("name: ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Name = (Console.ReadLine() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(Name))
            {
                Id = Guid.Empty;
                continue;
            }
            Console.ResetColor();
            Console.Write("password: ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Id = await AuthenticationService.Auth(Encoding.UTF8.GetBytes(Name),
                SHA256.HashData(Encoding.UTF8.GetBytes(Console.ReadLine().Trim())));
        }
        while(Id == Guid.Empty);
    }
}