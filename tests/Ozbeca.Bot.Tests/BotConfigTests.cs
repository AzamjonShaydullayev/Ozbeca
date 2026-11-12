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
}
