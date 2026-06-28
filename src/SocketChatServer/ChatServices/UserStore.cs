using System.Collections.Concurrent;

namespace SocketChatServer.ChatServices;

public class UserStore
{
    
    private ConcurrentDictionary<string, Guid> _usersByName = new();
    private ConcurrentDictionary<Guid, string> _usersById = new();
    private ConcurrentDictionary<string, string> _passwordHashes = new();
    
    
    public  bool TryAddUser(string name, Guid id, string passHash)
    {
        if (ContainsUserName(name) || ContainsUserId(id))
        {
            if(_passwordHashes.TryGetValue(name, out var hash))            
            {
                return hash == passHash;
            }
        }
        _usersById[id] = name;
        _usersByName[name] = id;
        _passwordHashes[name] = passHash;
        return true;
    }
    public bool IsValidHash(string name, string passHash)
    {
        if (_passwordHashes.TryGetValue(name, out var hash))
        {
            return hash == passHash;
        }
        return false;
    }
    public bool TryRemoveUser(string name, Guid id)
    {
        if (!ContainsUserName(name) || !ContainsUserId(id))
            return false;
        _usersById.TryRemove(id, out _);
        _usersByName.TryRemove(name, out _);
        return true;
    }
    public  bool ContainsUserName(string name)
    {
        return _usersByName.ContainsKey(name);
    }
    public  bool ContainsUserId(Guid id)
    {
        return _usersById.ContainsKey(id);
    }

    public  Guid GetUserId(string name)
    {
        if (ContainsUserName(name))
            return _usersByName[name];
        return Guid.Empty;
    }
    public string GetUserName(Guid id)
    {
        if (ContainsUserId(id))
            return _usersById[id];
        return string.Empty;
    }
}