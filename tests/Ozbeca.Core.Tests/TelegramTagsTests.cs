using System.Text;
using Ozbeca.Core;

namespace Ozbeca.Core.Tests;

public class TelegramTagsTests
{
    private static readonly TelegramTags.Tag[] NoTags = Array.Empty<TelegramTags.Tag>();

    [Fact]
    public void RoundTripSerialize()
    {
        var tags = new[] { new TelegramTags.Tag(0, 5, "**"), new TelegramTags.Tag(6, 2, "custom-emoji://5368324170671202286"), new TelegramTags.Tag(1, 1, null) };
        Assert.True(TelegramTags.TryParse(TelegramTags.Serialize(tags), 10, out var back));
        Assert.Equal(tags, back);
    }

    [Fact]
    public void BoldAndEmojiMoveWithText()
    {
        // "Choy 😀 shirin" — "Choy" qalin, emoji premium, "shirin" chiziqli
        string text = "Choy \U0001F600 shirin";
        var tags = new[]
        {
            new TelegramTags.Tag(0, 4, "**"),
            new TelegramTags.Tag(5, 2, "custom-emoji://123"),
            new TelegramTags.Tag(8, 6, "~~"),
        };
        Assert.True(TelegramTags.TryConvert(Encoding.UTF8.GetBytes(text), TelegramTags.Serialize(tags), out var newText, out var newTags));
        Assert.Equal("Çoy \U0001F600 şirin", newText);
        Assert.True(TelegramTags.TryParse(newTags, newText.Length, out var moved));
        Assert.Equal("Çoy", newText.Substring(moved[0].Offset, moved[0].Length));
        Assert.Equal("\U0001F600", newText.Substring(moved[1].Offset, moved[1].Length));
        Assert.Equal("custom-emoji://123", moved[1].Id);
        Assert.Equal("şirin", newText.Substring(moved[2].Offset, moved[2].Length));
    }

    [Fact]
    public void TagInsideWordWithApostrophe()
    {
        string text = "Salom o'zbek do'stlar";
        var tags = new[] { new TelegramTags.Tag(6, 6, "__"), new TelegramTags.Tag(13, 8, "**") };
        Assert.True(TelegramTags.TryConvert(Encoding.UTF8.GetBytes(text), TelegramTags.Serialize(tags), out var newText, out var newTags));
        Assert.True(TelegramTags.TryParse(newTags, newText.Length, out var moved));
        Assert.Equal("özbek", newText.Substring(moved[0].Offset, moved[0].Length));
        Assert.Equal("döstlar", newText.Substring(moved[1].Offset, moved[1].Length));
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0, 0, 0, 1 })]                              // soni 1, lekin ma'lumot yo'q
    [InlineData(new byte[] { 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 50, 0, 0, 0, 0 })] // matndan uzun
    public void RejectsBrokenData(byte[] data) => Assert.False(TelegramTags.TryParse(data, 10, out _));
}
