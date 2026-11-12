namespace Ozbeca.Bot.Ui;

/// <summary>Botning barcha matnlari — yangi alifboda (ö ğ ş ç, ʼ U+02BC).</summary>
public static class Texts
{
    public const string Greeting = "Salom! Matn yuboring — yangi alifboga ögiraman.";
    public const string Help =
        "Matn yuboring — yangi alifboga ögiraman. Faylni (Word, Excel, PowerPoint yoki matn) yuborsangiz, formatini saqlab ögiraman.\n" +
        "Teskari tugma yangi alifbodan eski alifboga qaytaradi. Boşqa çatda @ozbecauzbot deb yozsangiz ham işlayman.";

    public const string NewHeader = "Yangi alifboda:";
    public const string OldHeader = "Eski alifboda:";
    public const string NoChange = "Bu matnda ögiradigan narsa yöq.";
    public const string SendText = "Matn yuboring.";

    public const string FileBusy = "Avval oldingi faylni tugatay, biroz kuting.";
    public const string FileTooBig = "Fayl juda katta. 20 MB gaça fayl yuboring.";
    public const string FileUnsupported = "Bu fayl turini ögirolmayman. Word, Excel, PowerPoint yoki matn fayl yuboring.";
    public const string FileBroken = "Faylni öqiy olmadim. Fayl buzilgan bölişi mumkin.";
    public const string FileNoChange = "Faylda ögiradigan narsa yöq.";
    public const string FileDone = "Tayyor! Fayl yangi alifboda.";

    public const string CardCaption = "Özbeça — yangi alifbo";
    public const string CopyButton = "Nusxa";
    public const string ReverseButton = "Teskari";
    public const string SettingsTitle = "Sozlamalar. Tugmani bosib özgartiring.";
    public const string SetOldToNew = "Eski → Yangi";
    public const string SetNewToOld = "Yangi → Eski";
    public const string SetCyrillic = "Kirillni ham ögiriş";
    public const string SetWords = "Tutuqsiz sözlar";
    public const string SetFile = "Katta matnni fayl qilib beriş";

    public const string InlineNewTitle = "Yangi alifbo";
    public const string InlineOldTitle = "Eski alifbo";
    public const string InlineHint = "Matn yozing";
    public const string InlineLongButton = "Uzun matn — botda ögiriş";

    public const string StatTitle = "Statistika";
    public const string BroadcastAsk = "Xabarni yuboring. Barça foydalanuvçilarga tarqataman.";
    public const string BroadcastDone = "Tarqatiş tugadi.";
    public const string BroadcastStopped = "Tarqatiş töxtatildi.";
    public const string NotAdmin = "Bu buyruq faqat admin uçun.";
    public const string Flood = "Juda tez yozyapsiz. Biroz kuting.";
}
