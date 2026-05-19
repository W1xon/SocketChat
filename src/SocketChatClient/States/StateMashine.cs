namespace SocketChatClient;

public class StateMashine
{
    
    private static readonly Stack<IState> _stateStack = new();

    public static IState ActiveScene => _stateStack.Any() ? _stateStack.Peek() : null;
    
    public static void SwitchTo(IState scene)
    {
        if (_stateStack.Any()) Deactivate();
        Activate(scene);
    }

    public static void Activate(IState scene)
    {
        if (scene == null) throw new ArgumentNullException(nameof(scene));
        scene.Enter();
        _stateStack.Push(scene);
    }

    private static void Deactivate()
    {
        if (!_stateStack.Any()) throw new InvalidOperationException("Нет сцен для деактивации.");
        var scene = _stateStack.Pop();
        scene.Exit();
    }
}