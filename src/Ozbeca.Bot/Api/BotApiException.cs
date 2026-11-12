namespace Ozbeca.Bot.Api;

public sealed class BotApiException(int code, string description, int? retryAfter)
    : Exception($"{code}: {description}")
{
    public int Code { get; } = code;
    public string Description { get; } = description;
    public int? RetryAfter { get; } = retryAfter;
}
