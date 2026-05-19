using System.Text;

namespace SocketChatClient;

public class MenuState : IState
{
    private ChatService _chat;
    private string _name;

    public MenuState(ChatService chat, string name)
    {
        _chat = chat;
        _name = name;
        _chat.UpdateChats += PrintChats;
    }

    public void Enter() => PrintChats();

    public async Task Update()
    {
        DrawPrompt();
        string input = Console.ReadLine() ?? "";

        switch (input.ToLower())
        {
            case "-g":
            case "--group":
                await CreateGroup();
                return;

            case "-h":
            case "--help":
                ShowHelp();
                return;

            case "-q":
            case "--quit":
                _chat.Close();
                return;
        }

        if (int.TryParse(input, out int index) && index > 0 && index <= _chat.History.Count)
        {
            var address = _chat.History.ElementAt(index - 1).Key;
            StateMashine.SwitchTo(new ChatState(_chat, _name, address));
        }
        else if (!string.IsNullOrWhiteSpace(input))
        {
            StateMashine.SwitchTo(new ChatState(_chat, _name, input));
        }
    }

    private async Task CreateGroup()
    {
        Console.Write("\nGroup address: ");
        string address = Console.ReadLine() ?? "default";

        await _chat.AddOrJoinToGroup(address, Encoding.UTF8.GetBytes(_name));

        StateMashine.SwitchTo(new ChatState(_chat, _name, address));
    }

    private void ShowHelp()
    {
        Console.Clear();
        Console.WriteLine("\ncommands:");
        Console.WriteLine();
        Console.WriteLine(" -g, --group       create or join group");
        Console.WriteLine(" -h, --help        show this help");
        Console.WriteLine(" -q, --quit        exit");
        Console.WriteLine();
        Console.WriteLine("navigation:");
        Console.WriteLine();
        Console.WriteLine(" [number]          select chat by index");
        Console.WriteLine(" [address]         select chat by name");
        Console.WriteLine();
        Console.WriteLine("press any key...");
        Console.ReadKey(true);
        PrintChats();
    }
    
    private void PrintChats(string s = "", string ss = "")
    {
        Console.Clear();
        DrawHeader();
        DrawGroupsList();
        DrawCommands();
    }

    private void DrawHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"user: {_name}");
        Console.ResetColor();
        Console.WriteLine();
    }

    private void DrawGroupsList()
    {
        if (_chat.History.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("no chats");
            Console.ResetColor();
        }
        else
        {
            int i = 0;
            foreach (var chat in _chat.History)
            {
                var lastMsg = chat.Value.LastOrDefault() ?? "";
                string preview = lastMsg.Length > 40 ? lastMsg.Substring(0, 37) + "..." : lastMsg;

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($" {++i:D2} ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{chat.Key,-20}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($" {preview}");
            }
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    private void DrawCommands()
    {
        Console.WriteLine("commands:");
        Console.WriteLine(" -g    create or join group");
        Console.WriteLine(" -h    help");
        Console.WriteLine(" -q    quit");
    }

    private void DrawPrompt()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("\n> ");
        Console.ResetColor();
    }

    public void Exit() => _chat.UpdateChats -= PrintChats;
}