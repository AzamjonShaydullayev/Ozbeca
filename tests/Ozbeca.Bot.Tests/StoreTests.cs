using Microsoft.Data.Sqlite;
using Ozbeca.Bot;
using Ozbeca.Bot.Data;

namespace Ozbeca.Bot.Tests;

[Collection("Clock")]
public sealed class StoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"ozbeca-{Guid.NewGuid():N}.db");
    private readonly Func<DateTime> _clock = Clock.UtcNow;
    private DateTime _now = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

    public StoreTests() => Clock.UtcNow = () => _now;

    public void Dispose()
    {
        Clock.UtcNow = _clock;
        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _path, _path + "-wal", _path + "-shm" }) File.Delete(f);
    }

    [Fact]
    public void New_user_has_default_settings()
    {
        var u = new Store(_path).Touch(1);
        Assert.Equal(new Settings(false, true, true, false), u.Settings);
        Assert.False(u.Blocked);
    }

    [Fact]
    public void Settings_roundtrip()
    {
        var s = new Store(_path);
        s.Touch(1);
        var set = new Settings(true, false, false, true);
        s.SaveSettings(1, set);
        Assert.Equal(set, s.Get(1)!.Settings);
    }

    [Fact]
    public void Chars_reset_on_new_day()
    {
        var s = new Store(_path);
        s.Touch(1);
        Assert.Equal(100, s.AddChars(1, 100));
        _now = _now.AddDays(1);
        Assert.Equal(5, s.AddChars(1, 5));
    }

    [Fact]
    public void Stats_counts_active_and_blocked()
    {
        var s = new Store(_path);
        _now = _now.AddDays(-1);
        s.Touch(3);
        _now = _now.AddDays(1);
        s.Touch(1); s.Touch(2);
        s.SetBlocked(2);
        var st = s.GetStats();
        Assert.Equal(3, st.Users);
        Assert.Equal(2, st.ActiveToday);
        Assert.Equal(1, st.Blocked);
        Assert.Equal(new long[] { 1, 3 }, s.ActiveUserIds().OrderBy(x => x));
    }

    [Fact]
    public void Touch_unblocks_returning_user()
    {
        var s = new Store(_path);
        s.Touch(1);
        s.SetBlocked(1);
        Assert.True(s.Get(1)!.Blocked);
        Assert.False(s.Touch(1).Blocked);
    }

    [Fact]
    public void Usage_aggregates_by_kind()
    {
        var s = new Store(_path);
        s.AddUsage("text", 10); s.AddUsage("text", 5); s.AddUsage("file", 7);
        var t = s.GetStats().Today;
        Assert.Equal((2, 15L), t["text"]);
        Assert.Equal((1, 7L), t["file"]);
    }

    [Fact]
    public void Concurrent_AddChars_loses_nothing()
    {
        var s = new Store(_path);
        s.Touch(1);
        Parallel.For(0, 200, _ => s.AddChars(1, 1));
        Assert.Equal(200, s.Get(1)!.CharsToday);
    }

    [Fact]
    public void User_week_and_totals_follow_days()
    {
        var s = new Store(_path);
        s.Touch(1);
        s.AddChars(1, 10);
        s.AddFile(1);
        _now = _now.AddDays(2);
        s.AddChars(1, 5);
        Assert.Equal(10, s.AddChars(1, 5));
        s.AddFile(1);
        s.AddFile(1);
        Assert.Equal(new[] { 0, 0, 0, 0, 10, 0, 10 }, s.UserWeek(1));
        Assert.Equal((20L, 3), s.UserTotals(1));
        Assert.Equal(10, s.Get(1)!.CharsToday);

        _now = _now.Date.AddHours(19).AddSeconds(1);   // Toshkentda yangi kun (UTC+5)
        Assert.Equal(new[] { 0, 0, 0, 10, 0, 10, 0 }, s.UserWeek(1));
        Assert.Equal(0, s.Get(1)!.CharsToday);
        _now = _now.AddDays(6);
        Assert.Equal(new int[7], s.UserWeek(1));
        Assert.Equal((20L, 3), s.UserTotals(1));
    }

    [Fact]
    public void Daily_stats_need_existing_user()
    {
        var s = new Store(_path);
        Assert.Equal(0, s.AddChars(5, 10));
        s.AddFile(5);
        Assert.Null(s.Get(5));
        Assert.Equal((0L, 0), s.UserTotals(5));
        Assert.Equal(new int[7], s.UserWeek(5));
    }
}
