using System.Net;
using System.Net.Http.Headers;

namespace SocketChatClient;

public class AuthenticationService
{
    private static Uri ServerUri = new Uri("https://localhost:5000");
    private static HttpClient _httpClient;

    public AuthenticationService()
    {
        _httpClient = new HttpClient()
        {
            BaseAddress = ServerUri,
        };
    }
    public  async Task<Guid> Auth( byte[] name)
    {
        using var content = new ByteArrayContent(name);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using HttpResponseMessage response = await _httpClient.PostAsync("auth", content);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                Console.WriteLine("Данный юзернейм занят");
                return Guid.Empty;
            }
        }

        byte[] data =  await response.Content.ReadAsByteArrayAsync();
        return new Guid(data);
    }
    public async Task Logout(Guid id)
    {
        using var content = new ByteArrayContent(id.ToByteArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        await _httpClient.PostAsync("logout", content);
    }
}