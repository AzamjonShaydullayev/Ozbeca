using Ozbeca.Bot.Api;
using Ozbeca.Bot.Data;

namespace Ozbeca.Bot;

/// <summary>Xabarni (copyMessage) ro'yxatdagi hammaga ketma-ket yuboradi. 429 kutadi, 403 da bloklangan deb belgilaydi.</summary>
public sealed class Broadcaster(BotApi api, Store store, TimeSpan delay)
{
    /// <summary>Testlarda almashtiriladi.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Wait { get; set; } = Task.Delay;

    public async Task<(int Sent, int Failed)> Run(long fromChatId, int messageId, IReadOnlyList<long> ids,
        IProgress<(int Sent, int Failed, int Left)> progress, CancellationToken ct)
    {
        int sent = 0, failed = 0;
        try
        {
            foreach (var id in ids)
            {
                ct.ThrowIfCancellationRequested();
                if (await CopyTo(id, fromChatId, messageId, ct)) sent++; else failed++;
                progress.Report((sent, failed, ids.Count - sent - failed));
                if (delay > TimeSpan.Zero) await Wait(delay, ct);
            }
        }
        catch (OperationCanceledException) { }   // to'xtatildi — hozirgacha sanalganini qaytaramiz
        return (sent, failed);
    }

    private async Task<bool> CopyTo(long id, long fromChatId, int messageId, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await api.Call("copyMessage", new { chat_id = id, from_chat_id = fromChatId, message_id = messageId }, ct);
                return true;
            }
            catch (BotApiException e) when (e.Code == 429 && attempt < 5)
            {
                await Wait(TimeSpan.FromSeconds(Math.Clamp(e.RetryAfter ?? 1, 1, 60)), ct);
            }
            catch (BotApiException e)
            {
                if (e.Code == 403) store.SetBlocked(id);
                return false;
            }
            catch (HttpRequestException) { return false; }
        }
    }
}
