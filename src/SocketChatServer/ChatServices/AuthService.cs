using System.Buffers;
using System.Text;
using SocketChatServer.Extensions;

namespace SocketChatServer.ChatServices;

public class AuthService
{
    private readonly ConnectionStore _connectionStore;
    private readonly UserStore _userStore;

    public AuthService(ConnectionStore connectionStore, UserStore userStore)
    {
        _connectionStore = connectionStore;
        _userStore = userStore;
    }

    public async Task RegisterUser(HttpContext context)
    {
        bool isSuccess = await context.Request.BodyReader.TryReadExactLengthAsync(
            32,
            async buffer =>
            {
                int nameLength = (int)(buffer.Length - 32);
                string name = ParseName(buffer, nameLength);
                ReadOnlySequence<byte> hashSequence = buffer.Slice(nameLength, 32);

                byte[] rentedHash = ArrayPool<byte>.Shared.Rent(32);
                try
                {
                    Span<byte> hashSpan = rentedHash.AsSpan(0, 32);
                    hashSequence.CopyTo(hashSpan);
                    string hexHash = Convert.ToHexString(hashSpan);

                    Guid id;
                    if (_userStore.ContainsUserName(name))
                    {
                        if (!_userStore.IsValidHash(name, hexHash))
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return;
                        }

                        id = _userStore.GetUserId(name);
                    }
                    else
                    {
                        id = Guid.NewGuid();
                        if (!_userStore.TryAddUser(name, id, hexHash))
                        {
                            context.Response.StatusCode = StatusCodes.Status409Conflict;
                            return;
                        }
                    }

                    context.Response.StatusCode = StatusCodes.Status200OK;
                    context.Response.ContentType = "application/octet-stream";
                    context.Response.ContentLength = 16;

                    Memory<byte> idMemory = context.Response.BodyWriter.GetMemory(16);
                    if (id.TryWriteBytes(idMemory.Span))
                    {
                        context.Response.BodyWriter.Advance(16);
                    }

                    await context.Response.BodyWriter.FlushAsync();
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentedHash);
                }
            });

        if (!isSuccess)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }

    public async Task LogoutUser(HttpContext context)
    {
        bool isSuccess = await context.Request.BodyReader.TryReadExactLengthAsync(
            16,
            async buffer =>
            {
                ReadOnlySequence<byte> idSequence = buffer.Slice(0, 16);

                byte[] rentedId = ArrayPool<byte>.Shared.Rent(16);
                try
                {
                    Span<byte> idSpan = rentedId.AsSpan(0, 16);
                    idSequence.CopyTo(idSpan);

                    Guid id = new(idSpan);

                    if (_userStore.ContainsUserId(id))
                    {
                        await _connectionStore.RemoveUnusedConnection(id);
                    }

                    context.Response.StatusCode = StatusCodes.Status200OK;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentedId);
                }
            });

        if (!isSuccess)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }

    private static string ParseName(ReadOnlySequence<byte> buffer, int nameLength)
    {
        ReadOnlySequence<byte> nameSequence = buffer.Slice(0, nameLength);
        StringBuilder sb = new();

        foreach (var segment in nameSequence)
        {
            ReadOnlySpan<byte> span = segment.Span;
            if (!span.IsEmpty)
            {
                sb.Append(Encoding.UTF8.GetString(span));
            }
        }


        return sb.ToString();
    }
}
