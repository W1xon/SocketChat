using System.Buffers;
using System.Net;
using System.Net.Http.Headers;

namespace SocketChatClient;

public class AuthenticationService
{
    private static Uri ServerUri = new Uri("http://127.0.0.1:5000");
    private static HttpClient _httpClient;

    public AuthenticationService()
    {
        _httpClient = new HttpClient()
        {
            BaseAddress = ServerUri,
        };
    }
    public  async Task<Guid> Auth( byte[] name, byte[] hashPassword)
    {
        var totalSize = name.Length + hashPassword.Length;
        byte[] data = ArrayPool<byte>.Shared.Rent(totalSize);
        try
        {
            Span<byte> dataSpan = data.AsSpan().Slice(0, totalSize);
            name.CopyTo(dataSpan);
            hashPassword.CopyTo(dataSpan.Slice(name.Length));
            
            using var content = new ByteArrayContent(data, 0, totalSize);

            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using HttpResponseMessage response = await _httpClient.PostAsync("auth", content);
            if(response.IsSuccessStatusCode)
            {
                byte[] dataGuid = await response.Content.ReadAsByteArrayAsync();
                return new Guid(dataGuid);
            }
            else
                return Guid.Empty;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(data);
        }
    }
    public async Task Logout(Guid id)
    {
        using var content = new ByteArrayContent(id.ToByteArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        await _httpClient.PostAsync("logout", content);
    }
}