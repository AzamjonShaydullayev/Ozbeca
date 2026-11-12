using Ozbeca.Core;

namespace Ozbeca.Bot.Data;

public sealed record Settings(bool Reverse = false, bool Cyrillic = true, bool Dictionary = true, bool LongAsFile = false)
{
    public ConvertOptions ToOptions() => new(Reverse, Cyrillic, Dictionary);
}

public sealed record UserRow(long Id, Settings Settings, int CharsToday, int Streak, int Best, string? GameAnswer, bool Blocked);

public sealed record Stats(int Users, int ActiveToday, int Active7d, int Blocked, IReadOnlyDictionary<string, (int Count, long Chars)> Today);
