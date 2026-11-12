namespace Ozbeca.Bot.Ui;

/// <summary>Botning barcha matnlari — yangi alifboda (ö ğ ş ç, ʼ U+02BC).</summary>
public static class Texts
{
    public const string Greeting = "Salom! Matn yuboring — yangi alifboğa öğiraman.";
    public const string Help =
        "Matn yuboring — yangi alifboğa öğiraman. Faylni (Word, Excel, PowerPoint yoki matn) yuborsangiz, formatini saqlab öğiraman.\n" +
        "Teskari tugma yangi alifbodan eski alifboğa qaytaradi. Boşqa çatda @ozbecauzbot deb yozsangiz ham işlayman.";

    public const string NewHeader = "Yangi alifboda:";
    public const string OldHeader = "Eski alifboda:";
    public const string NoChange = "Bu matnda öğiradiğan narsa yöq.";
    public const string SendText = "Matn yuboring.";

    public const string FileBusy = "Avval oldingi faylni tugatay, biroz kuting.";
    public const string FileTooBig = "Fayl juda katta. 20 MB gaça fayl yuboring.";
    public const string FileUnsupported = "Bu fayl turini öğirolmayman. Word, Excel, PowerPoint yoki matn fayl yuboring.";
    public const string FileBroken = "Faylni öqiy olmadim. Fayl buzilğan bölişi mumkin.";
    public const string FileNoChange = "Faylda öğiradiğan narsa yöq.";
    public const string FileDone = "Tayyor! Fayl yangi alifboda.";

    public const string CardCaption = "Özbeça — yangi alifbo";
    public const string CopyButton = "Nusxa";
    public const string ReverseButton = "Teskari";

    public const string InlineNewTitle = "Yangi alifbo";
    public const string InlineOldTitle = "Eski alifbo";
    public const string InlineHint = "Matn yozing";
    public const string InlineLongButton = "Uzun matn — botda öğiriş";

    public const string StatTitle = "Statistika";
    public const string BroadcastAsk = "Xabarni yuboring. Barça foydalanuvçilarğa tarqataman.";
    public const string BroadcastDone = "Tarqatiş tugadi.";
    public const string BroadcastStopped = "Tarqatiş töxtatildi.";
    public const string NotAdmin = "Bu buyruq faqat admin uçun.";
    public const string Flood = "Juda tez yozyapsiz. Biroz kuting.";
}
