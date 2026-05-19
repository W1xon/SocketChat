using System.Text;

namespace SocketChatClient;

public class ChatState : IState
{
    private ChatService _chat;
    private string _name;
    private string _address;
    private int _messageCount;

    public ChatState(ChatService chat, string name, string address = null)
    {
        _chat = chat;
        _name = name;
        _chat.UpdateChats += PrintMessage;
        _address = address;
    }

    public void Enter()
    {
        Console.Clear();
        if (!string.IsNullOrEmpty(_address))
        {
            DrawHeader();

            if (_chat.History.ContainsKey(_address))
            {
                foreach (var msg in _chat.History[_address])
                {
                    FormatMessage(msg);
                }
                _messageCount = _chat.History[_address].Count;
            }
            return;
        }

        Console.Write("Chat address: ");
        _address = Console.ReadLine() ?? "";
        Enter();
    }

    public async Task Update()
    {
        DrawInputPrompt();

        var inputText = Console.ReadLine() ?? "";
        
        if (inputText.ToLower() == "-q" || inputText.ToLower() == "--quit")
        {
            StateMashine.SwitchTo(new MenuState(_chat, _name));
            return;
        }
        
        if (inputText == "-h" || inputText == "--help")
        {
            ShowHelp();
            return;
        }
        
        if (string.IsNullOrWhiteSpace(inputText)) return;
        _chat.History.GetOrAdd(_address, _ => new List<string>()).Add($"You: {inputText}");
        await _chat.Send(inputText, Encoding.UTF8.GetBytes(_name), Encoding.UTF8.GetBytes(_address));
    }

    private void PrintMessage(string address, string? sender = null)
    {
        if (address != _address) return;
        if (_chat.History[_address].Count <= _messageCount) return;

        Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");

        var lastMsg = _chat.History[_address].Last();
        FormatMessage(lastMsg, sender);

        DrawInputPrompt();
        _messageCount++;
    }

    private void FormatMessage(string message, string? sender = null)
    {
        bool isMine = message.StartsWith("You");

        if (isMine)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            string senderName = sender ?? _address;
            Console.WriteLine($"{senderName}: {message.Replace(_address + ":", "").Trim()}");
        }
        Console.ResetColor();
    }

    private void DrawHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"chat: {_address}");
        Console.ResetColor();
        Console.WriteLine();
    }

    private void DrawInputPrompt()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("> ");
        Console.ResetColor();
    }

    private void ShowHelp()
    {
        Console.Clear();
        Console.WriteLine("\ncommands:");
        Console.WriteLine();
        Console.WriteLine(" -q, --quit         back to menu");
        Console.WriteLine(" -h, --help         show this help");
        Console.WriteLine();
        Console.WriteLine("press any key...");
        Console.ReadKey(true);
        
        Console.Clear();
        DrawHeader();
        if (_chat.History.ContainsKey(_address))
        {
            foreach (var msg in _chat.History[_address])
            {
                FormatMessage(msg);
            }
        }
        DrawInputPrompt();
    }

    public void Exit() => _chat.UpdateChats -= PrintMessage;
}