namespace Ozbeca.Bot.Tests;

public class BotConfigTests
{
    private static BotConfig Cfg(params (string, string)[] env)
    {
        var d = env.ToDictionary(e => e.Item1, e => e.Item2);
        return BotConfig.FromEnv(k => d.GetValueOrDefault(k));
    }

    [Fact]
    public void Defaults()
    {
        var c = Cfg(("BOT_TOKEN", "1:abc"));
        Assert.Equal("1:abc", c.Token);
        Assert.Empty(c.Admins);
        Assert.Equal("data/bot.db", c.DbPath);
        Assert.Equal("data/work", c.WorkDir);
        Assert.True(c.PremiumEmoji);
    }

    [Fact]
    public void Parses_admins_with_spaces_and_flags()
    {
        var c = Cfg(("BOT_TOKEN", "1:abc"), ("ADMIN_IDS", " 5, 6 ,7"), ("PREMIUM_EMOJI", "0"), ("DB_PATH", "x.db"), ("WORK_DIR", "w"));
        Assert.Equal(new long[] { 5, 6, 7 }, c.Admins.OrderBy(x => x));
        Assert.False(c.PremiumEmoji);
        Assert.Equal("x.db", c.DbPath);
        Assert.Equal("w", c.WorkDir);
    }

    [Fact]
    public void Missing_token_or_bad_admin_throws_without_values()
    {
        Assert.Throws<InvalidOperationException>(() => Cfg());
        Assert.Throws<InvalidOperationException>(() => Cfg(("BOT_TOKEN", "  ")));
        var e = Assert.Throws<InvalidOperationException>(() => Cfg(("BOT_TOKEN", "1:secret"), ("ADMIN_IDS", "5,abc")));
        Assert.DoesNotContain("secret", e.Message);
        Assert.DoesNotContain("abc", e.Message);
    }

    [Theory]
    [InlineData("data/work", "data/bot.db", false)]
    [InlineData("work", "data/bot.db", false)]
    [InlineData("dat", "data/bot.db", false)]
    [InlineData("data", "data/bot.db", true)]          // DB katalogining o'zi
    [InlineData("data/", "data/bot.db", true)]
    [InlineData("data/work/..", "data/bot.db", true)]
    [InlineData(".", "data/bot.db", true)]             // DB katalogining otasi
    [InlineData(".", "bot.db", true)]
    public void Unsafe_work_dir(string work, string db, bool unsafeDir) => Assert.Equal(unsafeDir, BotConfig.UnsafeWorkDir(work, db));

    [Fact]
    public void Filesystem_root_work_dir_is_unsafe()
    {
        var root = Path.GetPathRoot(Path.GetFullPath("."))!;
        Assert.True(BotConfig.UnsafeWorkDir(root, Path.Combine(Path.GetTempPath(), "x", "bot.db")));
    }
}
