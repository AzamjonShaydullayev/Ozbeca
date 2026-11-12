using Ozbeca.Core;

namespace Ozbeca.Core.Tests;

public class UpdateInfoTests
{
    private const string Sha = "0407e73eb3907c5f100cdaa0f2250271e3012eb51fbedaee1852544ae40cc8c5";

    // GitHub javobi qisqartirilgan (haqiqiy maydon nomlari, ortiqcha maydonlar e'tiborsiz qolishi kerak).
    private static string Json(string tag = "v1.7.0", string name = "Ozbeca.exe", string? url = null, long size = 328704,
        string? digest = "sha256:" + Sha) =>
        "{\"url\":\"x\",\"tag_name\":\"" + tag + "\",\"draft\":false,\"assets\":[{\"name\":\"" + name + "\",\"size\":" + size +
        (digest == null ? "" : ",\"digest\":\"" + digest + "\"") + ",\"browser_download_url\":\"" +
        (url ?? "https://github.com/AzamjonShaydullayev/Ozbeca/releases/download/" + tag + "/" + name) + "\"}]}";

    private static readonly Version Current = new(1, 6, 4, 0);

    [Fact]
    public void NewerRelease()
    {
        var u = UpdateInfo.Parse(Json(), Current);
        Assert.NotNull(u);
        Assert.Equal(new Version(1, 7, 0), u!.Version);
        Assert.Equal(328704, u.Size);
        Assert.Equal(Sha, u.Sha256);
        Assert.Equal("https://github.com/AzamjonShaydullayev/Ozbeca/releases/download/v1.7.0/Ozbeca.exe", u.Url);
    }

    [Theory]
    [InlineData("v1.6.4")]   // shu versiya
    [InlineData("v1.6.3")]   // eskisi
    [InlineData("1.6")]
    [InlineData("v2.0-beta")]
    [InlineData("latest")]
    public void NotNewerOrBadTag(string tag) => Assert.Null(UpdateInfo.Parse(Json(tag: tag), Current));

    [Fact]
    public void ShortTagIsNewer() => Assert.NotNull(UpdateInfo.Parse(Json(tag: "v1.7"), Current));

    [Fact] public void OtherAssetName() => Assert.Null(UpdateInfo.Parse(Json(name: "Setup.exe"), Current));
    [Fact] public void ForeignUrl() => Assert.Null(UpdateInfo.Parse(Json(url: "https://evil.example/Ozbeca.exe"), Current));
    [Fact] public void OtherRepoUrl() => Assert.Null(UpdateInfo.Parse(Json(url: "https://github.com/someone/Ozbeca/releases/download/v1.7.0/Ozbeca.exe"), Current));
    [Fact] public void UrlOfOtherTag() => Assert.Null(UpdateInfo.Parse(Json(url: "https://github.com/AzamjonShaydullayev/Ozbeca/releases/download/v1.0.0/Ozbeca.exe"), Current));
    [Fact] public void NoDigest() => Assert.Null(UpdateInfo.Parse(Json(digest: null), Current));
    [Fact] public void BadDigest() => Assert.Null(UpdateInfo.Parse(Json(digest: "md5:abc"), Current));
    [Fact] public void TooLarge() => Assert.Null(UpdateInfo.Parse(Json(size: 50_000_000), Current));
    [Fact] public void EmptySize() => Assert.Null(UpdateInfo.Parse(Json(size: 0), Current));
    [Fact] public void NotJson() => Assert.Null(UpdateInfo.Parse("<html>rate limit</html>", Current));
    [Fact] public void NoAssets() => Assert.Null(UpdateInfo.Parse("{\"tag_name\":\"v1.7.0\"}", Current));
}
