namespace Ozbeca.Bot.Tests;

public class DotEnvTests
{
    private static Dictionary<string, string> Apply(string[] lines, Dictionary<string, string>? existing = null)
    {
        var env = existing ?? new Dictionary<string, string>();
        DotEnv.Apply(lines, k => env.GetValueOrDefault(k), (k, v) => env[k] = v);
        return env;
    }

    [Fact] public void Reads_pairs_trims_and_skips_comments_and_blanks()
    {
        var env = Apply(["# izoh", "", "  BOT_TOKEN = abc  ", "ADMIN_IDS=1,2", "PREMIUM_EMOJI=", "yaroqsiz qator", "=x"]);
        Assert.Equal("abc", env["BOT_TOKEN"]);
        Assert.Equal("1,2", env["ADMIN_IDS"]);
        Assert.Equal("", env["PREMIUM_EMOJI"]);
        Assert.Equal(3, env.Count);
    }

    [Fact] public void Value_may_contain_equals_sign() =>
        Assert.Equal("a=b", Apply(["K=a=b"])["K"]);

    [Fact] public void Does_not_override_existing_variables()
    {
        var env = Apply(["BOT_TOKEN=fayldan", "WORK_DIR=w"], new() { ["BOT_TOKEN"] = "muhitdan" });
        Assert.Equal("muhitdan", env["BOT_TOKEN"]);
        Assert.Equal("w", env["WORK_DIR"]);
    }
}
