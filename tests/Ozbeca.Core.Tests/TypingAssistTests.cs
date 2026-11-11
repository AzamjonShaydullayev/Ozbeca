using Ozbeca.Core;

namespace Ozbeca.Core.Tests;

/// <summary>Yozish paytidagi yordam: qaytarilgan so'zni istisnoga taklif qilish, inglizcha gap, istisnolar.</summary>
public class TypingAssistTests
{
    private static (bool Ok, int Del, string Text) Type(TokenBuffer b, string word)
    {
        foreach (char c in word) b.Add(c);
        bool ok = b.TryComplete(out int del, out string text);
        return (ok, del, text);
    }

    [Fact]
    public void RejectedWhenUndoneWordIsLeftAndTypingContinues()
    {
        var b = new TokenBuffer();
        Assert.Equal("çoy", Type(b, "choy").Text);
        Assert.True(b.TryUndo(out _, out string restore));
        Assert.Equal("choy ", restore);
        Assert.Null(b.TakeRejected());          // hali davom etmadi
        b.Add('v');                             // keyingi so'z boshlandi — "choy" shu holicha qoldi
        Assert.Equal("choy", b.TakeRejected());
        Assert.Null(b.TakeRejected());          // bir marta
    }

    [Fact]
    public void RejectedWhenUndoneWordIsPulledBackAndFinishedUnchanged()
    {
        var b = new TokenBuffer();
        Type(b, "choy");
        b.TryUndo(out _, out _);
        b.Backspace();                          // probel o'chdi — so'z yana qo'lda
        Assert.False(b.TryComplete(out _, out _));
        Assert.Equal("choy", b.TakeRejected());
    }

    [Fact]
    public void NotRejectedWhenUndoneWordIsEdited()
    {
        var b = new TokenBuffer();
        Type(b, "osh");
        b.TryUndo(out _, out _);
        b.Backspace();
        foreach (char c in "xona") b.Add(c);   // "oshxona" — davom ettirdi, rad etish emas
        Assert.True(b.TryComplete(out _, out string text));
        Assert.Equal("oşxona", text);
        Assert.Null(b.TakeRejected());
    }

    [Fact]
    public void NotRejectedWithoutUndo()
    {
        var b = new TokenBuffer();
        Type(b, "choy");
        b.Add('v');
        Assert.Null(b.TakeRejected());
    }

    [Fact]
    public void ForgetUndoWhenReplacementSkipped()
    {
        var b = new TokenBuffer();
        Type(b, "choy");
        b.ForgetUndo(); // parol maydoni — almashtirish bo'lmadi
        Assert.False(b.TryUndo(out _, out _));
    }

    [Theory]
    [InlineData("check")]
    [InlineData("Cache,")]
    [InlineData("(school)")]
    public void EnglishWords(string token) => Assert.True(CodeGuard.IsEnglishWord(token), token);

    [Theory]
    [InlineData("choy")]
    [InlineData("shahar")]
    [InlineData("O'zbekiston")]
    [InlineData("qishloq")]
    [InlineData("much")]   // o'zbekcha "much" (muchal) — ataylab ro'yxatda yo'q
    public void UzbekWords(string token) => Assert.False(CodeGuard.IsEnglishWord(token), token);

    [Fact]
    public void EnglishGuardOffKeepsWordList()
    {
        try
        {
            CodeGuard.EnglishGuard = false;
            Assert.True(CodeGuard.IsEnglishWord("check"));     // ro'yxat — har doim
            Assert.False(CodeGuard.StartsEnglishContext("the"));
            Assert.Equal("the şah", CodeGuard.ConvertProse("the shah"));
        }
        finally { CodeGuard.EnglishGuard = true; }
    }

    [Fact]
    public void EnglishContextWhileTyping()
    {
        var b = new TokenBuffer();
        Assert.False(Type(b, "the").Ok);
        Assert.False(Type(b, "shah").Ok);      // "the shah" — inglizcha gap davomi
        var b2 = new TokenBuffer();
        Type(b2, "please");
        Assert.True(Type(b2, "qo'shiq").Ok);   // aniq o'zbekcha — baribir o'giriladi
    }

    [Fact]
    public void EnglishContextBreaksAtPunctuation()
    {
        var b = new TokenBuffer();
        Type(b, "Thanks,");
        Assert.Equal("çoy", Type(b, "choy").Text);
    }

    [Theory]
    [InlineData("How are you shah", "How are you shah")]
    [InlineData("Thanks! Bugun choy ichdik", "Thanks! Bugun çoy içdik")]
    [InlineData("the toga", "the toga")]
    [InlineData("please qo'shiq ayt", "please qöşiq ayt")]
    public void EnglishContextOnPaste(string input, string expected) => Assert.Equal(expected, CodeGuard.ConvertProse(input));

    [Fact]
    public void UserExceptions()
    {
        try
        {
            Transliterator.SetExceptions(new[] { "Shohjahon", " chorsu " });
            Assert.Equal("Shohjahon bilan Chorsu'da çoy", Transliterator.ToNewAlphabet("Shohjahon bilan Chorsu'da choy"));
            Assert.Equal("Şohjahonov", Transliterator.ToNewAlphabet("Shohjahonov")); // butun so'z emas — o'giriladi
        }
        finally { Transliterator.SetExceptions(null); }
        Assert.Equal("Şohjahon", Transliterator.ToNewAlphabet("Shohjahon"));
    }
}
