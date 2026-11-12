namespace Ozbeca.Bot;

public sealed record BotConfig(string Token, IReadOnlySet<long> Admins, string DbPath, string WorkDir, bool PremiumEmoji)
{
    public static BotConfig FromEnv() => FromEnv(Environment.GetEnvironmentVariable);

    // Xato matnlariga qiymatlar (token, ID'lar) kiritilmaydi.
    internal static BotConfig FromEnv(Func<string, string?> get)
    {
        var token = get("BOT_TOKEN")?.Trim();
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("BOT_TOKEN environment variable is missing or empty.");

        var admins = new HashSet<long>();
        foreach (var part in (get("ADMIN_IDS") ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!long.TryParse(part, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id))
                throw new InvalidOperationException("ADMIN_IDS must be a comma-separated list of numeric Telegram user ids.");
            admins.Add(id);
        }

        static string Or(string? v, string d) => string.IsNullOrWhiteSpace(v) ? d : v.Trim();
        return new BotConfig(token, admins, Or(get("DB_PATH"), "data/bot.db"), Or(get("WORK_DIR"), "data/work"),
            Or(get("PREMIUM_EMOJI"), "1") != "0");
    }
}
