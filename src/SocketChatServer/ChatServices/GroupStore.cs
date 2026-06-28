using System.Collections.Concurrent;

namespace SocketChatServer.ChatServices;

public class GroupStore
{
    
    private ConcurrentDictionary<string, ConcurrentBag<string>> _groups = new();
    
    public ConcurrentBag<string> GetGroupMembers(string groupName)
    {
        if (_groups.ContainsKey(groupName))
        {
            return _groups[groupName];
        }
        return new ConcurrentBag<string>();
    }

    public void AddGroup(string groupName, string senderName)
    {
        _groups.GetOrAdd(groupName, _ => new ConcurrentBag<string>())
            .Add(senderName);
    }
}