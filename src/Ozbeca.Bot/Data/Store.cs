using Microsoft.Data.Sqlite;

namespace Ozbeca.Bot.Data;

/// <summary>Foydalanuvchi sozlamalari va statistika. Foydalanuvchi matni saqlanmaydi.</summary>
public sealed class Store
{
    private readonly string _cs;

    public Store(string path)
    {
        _cs = new SqliteConnectionStringBuilder { DataSource = path, Pooling = true, DefaultTimeout = 30 }.ToString();
        using var c = Open();
        Exec(c, "PRAGMA journal_mode=WAL");
        Exec(c, @"
            CREATE TABLE IF NOT EXISTS users(
              id INTEGER PRIMARY KEY, first_seen TEXT, last_seen TEXT,
              reverse INT NOT NULL DEFAULT 0, cyrillic INT NOT NULL DEFAULT 1,
              dictionary INT NOT NULL DEFAULT 1, long_file INT NOT NULL DEFAULT 0,
              chars_day TEXT, chars_today INT NOT NULL DEFAULT 0,
              streak INT NOT NULL DEFAULT 0, best INT NOT NULL DEFAULT 0,
              game_answer TEXT, blocked INT NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS usage(
              day TEXT, kind TEXT, count INT NOT NULL DEFAULT 0, chars INT NOT NULL DEFAULT 0,
              PRIMARY KEY(day, kind));");
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_cs);
        c.Open();
        Exec(c, "PRAGMA busy_timeout=30000");
        return c;
    }

    private static void Exec(SqliteConnection c, string sql, params (string, object?)[] p)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in p) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private static string Day() => Clock.Today().ToString("yyyy-MM-dd");

    private const string Cols = "id, reverse, cyrillic, dictionary, long_file, chars_day, chars_today, streak, best, game_answer, blocked";

    public UserRow Touch(long userId)
    {
        var d = Day();
        using var c = Open();
        Exec(c, @"INSERT INTO users(id, first_seen, last_seen) VALUES($id, $d, $d)
                  ON CONFLICT(id) DO UPDATE SET last_seen=$d, blocked=0", ("$id", userId), ("$d", d));
        return Get(c, userId)!;
    }

    public UserRow? Get(long userId)
    {
        using var c = Open();
        return Get(c, userId);
    }

    private static UserRow? Get(SqliteConnection c, long userId)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {Cols} FROM users WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", userId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new UserRow(
            r.GetInt64(0),
            new Settings(r.GetInt32(1) != 0, r.GetInt32(2) != 0, r.GetInt32(3) != 0, r.GetInt32(4) != 0),
            !r.IsDBNull(5) && r.GetString(5) == Day() ? r.GetInt32(6) : 0,
            r.GetInt32(7), r.GetInt32(8),
            r.IsDBNull(9) ? null : r.GetString(9),
            r.GetInt32(10) != 0);
    }

    public void SaveSettings(long userId, Settings s)
    {
        using var c = Open();
        Exec(c, "UPDATE users SET reverse=$r, cyrillic=$c, dictionary=$d, long_file=$l WHERE id=$id",
            ("$r", s.Reverse ? 1 : 0), ("$c", s.Cyrillic ? 1 : 0), ("$d", s.Dictionary ? 1 : 0),
            ("$l", s.LongAsFile ? 1 : 0), ("$id", userId));
    }

    /// <summary>Bugungi jami belgilar (yangi kunda 0 dan). Bitta UPDATE — atomik.</summary>
    public int AddChars(long userId, int chars)
    {
        var d = Day();
        using var c = Open();
        Exec(c, "INSERT OR IGNORE INTO users(id, first_seen, last_seen) VALUES($id, $d, $d)", ("$id", userId), ("$d", d));
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"UPDATE users SET chars_today = CASE WHEN chars_day=$d THEN chars_today+$n ELSE $n END, chars_day=$d
                            WHERE id=$id RETURNING chars_today";
        cmd.Parameters.AddWithValue("$d", d);
        cmd.Parameters.AddWithValue("$n", chars);
        cmd.Parameters.AddWithValue("$id", userId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void AddUsage(string kind, int chars)
    {
        using var c = Open();
        Exec(c, @"INSERT INTO usage(day, kind, count, chars) VALUES($d, $k, 1, $n)
                  ON CONFLICT(day, kind) DO UPDATE SET count=count+1, chars=chars+$n",
            ("$d", Day()), ("$k", kind), ("$n", chars));
    }

    public void SetGame(long userId, string? answer, int streak, int best)
    {
        using var c = Open();
        Exec(c, "UPDATE users SET game_answer=$a, streak=$s, best=$b WHERE id=$id",
            ("$a", answer), ("$s", streak), ("$b", best), ("$id", userId));
    }

    public void SetBlocked(long userId)
    {
        using var c = Open();
        Exec(c, "UPDATE users SET blocked=1 WHERE id=$id", ("$id", userId));
    }

    public IReadOnlyList<long> ActiveUserIds()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id FROM users WHERE blocked=0";
        using var r = cmd.ExecuteReader();
        var list = new List<long>();
        while (r.Read()) list.Add(r.GetInt64(0));
        return list;
    }

    public Stats GetStats()
    {
        var today = Clock.Today();
        using var c = Open();
        int users, blocked, a0, a7;
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = @"SELECT COUNT(*), COALESCE(SUM(blocked),0),
                  COALESCE(SUM(last_seen=$t),0), COALESCE(SUM(last_seen>=$w),0) FROM users";
            cmd.Parameters.AddWithValue("$t", today.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("$w", today.AddDays(-6).ToString("yyyy-MM-dd"));
            using var r = cmd.ExecuteReader();
            r.Read();
            users = r.GetInt32(0); blocked = r.GetInt32(1); a0 = r.GetInt32(2); a7 = r.GetInt32(3);
        }
        var map = new Dictionary<string, (int, long)>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT kind, count, chars FROM usage WHERE day=$d";
            cmd.Parameters.AddWithValue("$d", Day());
            using var r = cmd.ExecuteReader();
            while (r.Read()) map[r.GetString(0)] = (r.GetInt32(1), r.GetInt64(2));
        }
        return new Stats(users, a0, a7, blocked, map.ToDictionary(k => k.Key, k => (k.Value.Item1, k.Value.Item2)));
    }
}
