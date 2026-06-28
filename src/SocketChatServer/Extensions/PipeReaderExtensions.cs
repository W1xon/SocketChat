using System.Buffers;
using System.IO.Pipelines;

namespace SocketChatServer.Extensions;

public static class PipeReaderExtensions
{
    public static async Task<bool> TryReadExactLengthAsync(this PipeReader reader, int minLength,
        Func<ReadOnlySequence<byte>, Task> logic)
    {
        
        var result = await reader.ReadAsync();
        var buffer = result.Buffer;
    
        if (buffer.Length < minLength)
        {
            reader.AdvanceTo(buffer.End);
            return false;
        }

        try
        {
            await logic(buffer);
            return true;
        }
        finally
        {
            reader.AdvanceTo(buffer.End);
        }
    }
}