using System.Text;
using Ozbeca.Bot.Api;

namespace Ozbeca.Bot.Tests;

public class BotApiTests
{
    private const string Token = "123:SECRET";

    [Fact]
    public async Task Call_posts_json_to_method_url()
    {
        var fake = new FakeTelegram();
        var api = new BotApi(fake, Token);
        var r = await api.Call("sendMessage", new { ChatId = 42, Text = "salom" });
        Assert.Equal(1, r.Int("message_id"));
        var (method, body) = Assert.Single(fake.Calls);
        Assert.Equal("sendMessage", method);
        Assert.Equal("/bot123:SECRET/sendMessage", Assert.Single(fake.Urls));
        Assert.Equal(42, body.GetProperty("chat_id").GetInt32());
        Assert.Equal("salom", body.Str("text"));
    }

    [Fact]
    public async Task Error_429_has_retry_after()
    {
        var fake = new FakeTelegram
        {
            Respond = (_, _) => "{\"ok\":false,\"error_code\":429,\"description\":\"Too Many Requests\",\"parameters\":{\"retry_after\":5}}"
        };
        var ex = await Assert.ThrowsAsync<BotApiException>(() => new BotApi(fake, Token).Call("sendMessage"));
        Assert.Equal(429, ex.Code);
        Assert.Equal(5, ex.RetryAfter);
    }

    [Fact]
    public async Task Exception_message_has_no_token()
    {
        var fake = new FakeTelegram
        {
            Respond = (_, _) => "{\"ok\":false,\"error_code\":400,\"description\":\"Bad Request: chat not found\"}"
        };
        var ex = await Assert.ThrowsAsync<BotApiException>(() => new BotApi(fake, Token).Call("sendMessage"));
        Assert.Equal("400: Bad Request: chat not found", ex.Message);
        Assert.DoesNotContain("SECRET", ex.ToString());
    }

    [Fact]
    public async Task Upload_sends_multipart_with_filename()
    {
        var fake = new FakeTelegram();
        var api = new BotApi(fake, Token);
        await api.Upload("sendDocument", new Dictionary<string, string> { ["chat_id"] = "42" },
            "document", "a.docx", new MemoryStream(new byte[] { 1, 2, 3 }));
        var (method, body) = Assert.Single(fake.Calls);
        Assert.Equal("sendDocument", method);
        Assert.Equal("42", body.Str("chat_id"));
        Assert.Equal("file:a.docx", body.Str("document"));
    }

    [Fact]
    public async Task Download_copies_bytes()
    {
        var fake = new FakeTelegram();
        fake.Files["documents/f.txt"] = Encoding.UTF8.GetBytes("abc");
        var ms = new MemoryStream();
        await new BotApi(fake, Token).Download("documents/f.txt", ms, 10);
        Assert.Equal("abc", Encoding.UTF8.GetString(ms.ToArray()));
    }

    [Fact]
    public async Task Download_over_limit_throws()
    {
        var fake = new FakeTelegram();
        fake.Files["documents/f.bin"] = new byte[100];
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new BotApi(fake, Token).Download("documents/f.bin", new MemoryStream(), 50));
    }

    [Fact]
    public async Task Transport_error_does_not_leak_token()
    {
        var api = new BotApi(new ThrowingHandler(), Token);
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => api.Call("getMe"));
        Assert.DoesNotContain("SECRET", ex.ToString());
    }

    [Theory]
    [InlineData(502, "<html>Bad Gateway</html>")]
    [InlineData(503, "")]
    [InlineData(200, "[]")]
    [InlineData(200, "null")]
    [InlineData(200, "{}")]
    public async Task Non_json_or_non_object_response_throws_BotApiException_with_status(int status, string body)
    {
        var fake = new FakeTelegram { RespondRaw = (_, _) => (status, body) };
        var api = new BotApi(fake, Token);
        var ex = await Assert.ThrowsAsync<BotApiException>(() => api.Call("getMe"));
        Assert.Equal(status, ex.Code);
        Assert.Equal($"{status}: non-JSON response", ex.Message);
        var up = await Assert.ThrowsAsync<BotApiException>(() => api.Upload("sendDocument",
            new Dictionary<string, string>(), "document", "a.txt", new MemoryStream()));
        Assert.Equal(status, up.Code);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) =>
            throw new HttpRequestException("failed " + r.RequestUri);
    }
}
