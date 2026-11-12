namespace Ozbeca.Bot;

public static class Clock
{
    public static Func<DateTime> UtcNow = () => DateTime.UtcNow;

    /// <summary>Toshkent sanasi (UTC+5, yozgi vaqt yo'q).</summary>
    public static DateOnly Today() => DateOnly.FromDateTime(UtcNow().AddHours(5));

    /// <summary>Hozirgi vaqt, millisekund (o'yin taymeri uchun).</summary>
    public static long Ms() => UtcNow().Ticks / TimeSpan.TicksPerMillisecond;
}
