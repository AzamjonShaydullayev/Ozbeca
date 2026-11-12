using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Ozbeca.Bot.Api;

/// <summary>Telegram Bot API ustidagi yupqa HTTP qatlam. Token xato matnlariga tushmaydi.</summary>
public sealed class BotApi
{
    private readonly HttpClient _http;
    private readonly string _token;
    private readonly string _baseUrl;

    public BotApi(HttpMessageHandler handler, string token, string baseUrl = "https://api.telegram.org")
    {
        // Long polling 50 s, shuning uchun timeout undan katta.
        _http = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(70) };
        _token = token;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<JsonElement> Call(string method, object? payload = null, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(payload ?? new { }, Json.Options);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await Send(method, content, ct);
    }

    public async Task<JsonElement> Upload(string method, IReadOnlyDictionary<string, string> fields,
        string fileField, string fileName, Stream fileContent, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        foreach (var (k, v) in fields) form.Add(new StringContent(v, Encoding.UTF8), k);
        var file = new StreamContent(fileContent);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, fileField, fileName);
        return await Send(method, form, ct);
    }

    public async Task Download(string filePath, Stream destination, long maxBytes, CancellationToken ct = default)
    {
        using var resp = await Guard(() => _http.GetAsync($"{_baseUrl}/file/bot{_token}/{filePath}",
            HttpCompletionOption.ResponseHeadersRead, ct));
        if (!resp.IsSuccessStatusCode)
            throw new BotApiException((int)resp.StatusCode, "file download failed", null);
        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        var buf = new byte[81920];
        long total = 0;
        int n;
        while ((n = await src.ReadAsync(buf, ct)) > 0)
        {
            total += n;
            if (total > maxBytes) throw new InvalidDataException($"File exceeds {maxBytes} bytes");
            await destination.WriteAsync(buf.AsMemory(0, n), ct);
        }
    }

    private async Task<JsonElement> Send(string method, HttpContent content, CancellationToken ct)
    {
        using var resp = await Guard(() => _http.PostAsync($"{_baseUrl}/bot{_token}/{method}", content, ct));
        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        if (root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True)
            return root.TryGetProperty("result", out var r) ? r.Clone() : default;
        throw new BotApiException(root.Int("error_code") ?? (int)resp.StatusCode,
            root.Str("description") ?? "unknown error",
            root.Obj("parameters")?.Int("retry_after"));
    }

    // URL tokenni o'z ichiga oladi: transport xatosi matnidan tokenni olib tashlaymiz.
    private async Task<HttpResponseMessage> Guard(Func<Task<HttpResponseMessage>> send)
    {
        try { return await send(); }
        catch (HttpRequestException e)
        {
            throw new HttpRequestException(e.Message.Replace(_token, "<token>"), null, e.StatusCode);
        }
    }
}
