using System.Net.WebSockets;
using System.Text;

namespace SocketChatClient;

class Program
{
    public static string name = "";
    private static Guid id;
    static async Task Main(string[] args)
    {
        AuthenticationService authenticationService = new();
        do
        {
            Console.ResetColor();
            Console.Write("name: ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            name = (Console.ReadLine() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                id = Guid.Empty;
                continue;
            }
            id = await authenticationService.Auth(Encoding.UTF8.GetBytes(name));
        } while ( id == Guid.Empty);
        Console.ResetColor();

        ChatService chat = new ChatService();
        await chat.ConnectAsync();
        await chat.SendAuth(id);


        _ = Task.Run(() => chat.ReceiveLoop());
        _ = Task.Run(() => chat.Ping());

        StateMashine.Activate(new MenuState(chat, name));
        while (chat.State == WebSocketState.Open)
        {
            await StateMashine.ActiveScene.Update();
        }
        await authenticationService.Logout(id);
    }
}