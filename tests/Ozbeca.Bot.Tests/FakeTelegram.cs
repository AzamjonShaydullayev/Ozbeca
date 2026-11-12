using System.Net;
using System.Text;
using System.Text.Json;

namespace Ozbeca.Bot.Tests;

/// <summary>Telegram Bot API soxta serveri. Oqim-xavfsiz (Calls lock bilan).</summary>
internal sealed class FakeTelegram : HttpMessageHandler
{
    public const string DefaultResponse = "{\"ok\":true,\"result\":{\"message_id\":1}}";

    private readonly object _lock = new();
    private readonly List<(string Method, JsonElement Body)> _calls = new();

    /// <summary>Qo'ng'iroqlar nusxasi (har o'qishda yangi ro'yxat).</summary>
    public List<(string Method, JsonElement Body)> Calls { get { lock (_lock) return _calls.ToList(); } }

    /// <summary>(method, body) -> javob JSON matni. Null bo'lsa standart javob.</summary>
    public Func<string, JsonElement, string>? Respond { get; set; }

    /// <summary>HTTP darajasidagi javob: (status, xom tana). Respond'dan ustun.</summary>
    public Func<string, JsonElement, (int Status, string Body)>? RespondRaw { get; set; }

    /// <summary>So'rov URL yo'llari (Calls bilan bir xil tartibda), masalan "/bot123:X/sendMessage".</summary>
    public List<string> Urls { get { lock (_lock) return _urls.ToList(); } }
    private readonly List<string> _urls = new();

    /// <summary>Fayl yuklab olish: "/file/bot&lt;token&gt;/" dan keyingi yo'l -> baytlar.</summary>
    public Dictionary<string, byte[]> Files { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path.StartsWith("/file/bot", StringComparison.Ordinal))
        {
            var rest = path.Substring("/file/bot".Length);
            var filePath = rest.Substring(rest.IndexOf('/') + 1);
            lock (_lock) { _calls.Add(("download:" + filePath, JsonDocument.Parse("{}").RootElement.Clone())); _urls.Add(path); }
            return Files.TryGetValue(filePath, out var bytes)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var method = path.Substring(path.LastIndexOf('/') + 1);
        JsonElement body;
        if (request.Content is MultipartFormDataContent multipart)
        {
            var fields = new Dictionary<string, string>();
            foreach (var part in multipart)
            {
                var name = part.Headers.ContentDisposition!.Name!.Trim('"');
                var file = part.Headers.ContentDisposition.FileName;
                fields[name] = file != null ? "file:" + file.Trim('"') : await part.ReadAsStringAsync(ct);
            }
            body = JsonSerializer.SerializeToElement(fields);
        }
        else
        {
            var text = request.Content == null ? "{}" : await request.Content.ReadAsStringAsync(ct);
            body = JsonDocument.Parse(text).RootElement.Clone();
        }

        lock (_lock) { _calls.Add((method, body)); _urls.Add(path); }
        if (RespondRaw != null)
        {
            var (status, raw) = RespondRaw(method, body);
            return new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(raw, Encoding.UTF8) };
        }
        var json = Respond?.Invoke(method, body) ?? DefaultResponse;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}
