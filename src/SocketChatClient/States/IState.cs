namespace SocketChatClient;

public interface IState
{
    public void Enter();
    public Task Update();
    public void Exit();
}