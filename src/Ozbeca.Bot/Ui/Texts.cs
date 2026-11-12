namespace Ozbeca.Bot.Ui;

/// <summary>Botning barcha matnlari — yangi alifboda (ö ğ ş ç, ʼ U+02BC).</summary>
public static class Texts
{
    public const string CmdStart = "Boşlaş";
    public const string CmdSettings = "Sozlamalar";
    public const string CmdHelp = "Yordam";
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
    public const string FileDoneOld = "Tayyor! Fayl eski alifboda.";

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
    public const string BroadcastAsk = "Tarqatiladigan xabarga javob qilib /send deb yozing.";
    public const string BroadcastConfirm = "foydalanuvçiga yuboriladi.";
    public const string BroadcastBusy = "Boşqa tarqatiş davom etyapti.";
    public const string BroadcastGo = "Yuboriş";
    public const string BroadcastNo = "Bekor";
    public const string BroadcastStop = "Töxtatiş";
    public const string BroadcastCancelled = "Bekor qilindi.";
    public const string BroadcastRunning = "Yuborilmoqda.";
    public const string BroadcastSent = "Yuborildi";
    public const string BroadcastFailed = "Xato";
    public const string BroadcastLeft = "Qoldi";
    public const string StatUsers = "Jami";
    public const string StatToday = "Bugun";
    public const string StatWeek = "7 kun";
    public const string StatBlocked = "Bloklagan";
    public const string StatText = "Matn";
    public const string StatFile = "Fayl";
    public const string StatInline = "Inline";
    public const string StatChars = "belgi";
    public const string BroadcastDone = "Tarqatiş tugadi.";
    public const string BroadcastStopped = "Tarqatiş töxtatildi.";
    public const string NotAdmin = "Bu buyruq faqat admin uçun.";
    public const string Flood = "Juda tez yozyapsiz. Biroz kuting.";

    // /start menyusi va widget'lar
    public const string MenuSettings = "Sozlama";
    public const string MenuLetters = "Harflar";
    public const string MenuGame = "Öyin";
    public const string MenuStats = "Statistika";
    public const string MenuFile = "Fayl";
    public const string MenuHelp = "Yordam";
    public const string CmdGame = "Harfni top öyini";
    public const string CmdStats = "Mening statistikam";
    public const string CmdLetters = "Yangi alifbo harflari";
    public const string FileHelp =
        "Faylni şu çatga yuboring — formatini saqlab ögiraman: Word (.docx), Excel (.xlsx), PowerPoint (.pptx) yoki matn (.txt, .csv, .srt, .md). 20 MB gaça.\n" +
        "Yönaliş sozlamadan olinadi: eskidan yangiga yoki yangidan eskiga.";
    public const string LettersHint = "Harfni bosing — nusxa olinadi.";

    // salomlaşuv
    public const string GreetHello = "Assalomu alaykum";
    public const string GreetIntro = "Men matnni yangi özbek alifbosiga ögiraman — tez, aniq va bepul.";
    public const string GreetText = "Matn yuboring — darhol yangi alifboda qaytaraman.";
    public const string GreetFile = "Fayl yuboring (Word, Excel, PowerPoint, matn) — formatini saqlab ögiraman.";
    public const string GreetReverse = "«Teskari» tugmasi yangi alifbodan eskisiga qaytaradi.";
    public const string GreetInline = "Boşqa çatlarda: @ozbecauzbot va matn.";
    public const string GreetGo = "Boşlaş uçun biror matn yuboring";
    public const string BackButton = "Orqaga";
    public const string SetDirection = "Yönaliş";

    // öyin
    public const string GameTitle = "Harfni top";
    public const string GameAsk = "Qaysi harf tuşib qolgan?";
    public const string GameAsk2 = "Ikki harfni tartib bilan tanlang.";
    public const string GameTimer = "10 soniya içida javob bering!";
    public const string GameStreak = "Seriya";
    public const string GameBest = "Rekord";
    public const string GameRight = "Töğri!";
    public const string GameWrong = "Javob";
    public const string GameTimeout = "Vaqt tugadi";
}
