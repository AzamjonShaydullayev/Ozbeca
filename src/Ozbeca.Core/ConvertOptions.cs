namespace Ozbeca.Core;

/// <summary>Bir martalik o'girish sozlamalari (Telegram bot: har so'rov o'zinikini beradi; global holatga bog'liq emas).</summary>
/// <param name="Reverse">true — yangi → eski lotin (ö → o', ş → sh); false — eski/kirill → yangi.</param>
/// <param name="Cyrillic">Kirill so'zlar ham o'girilsin (yo'nalishga qarab yangi yoki eski lotinga).</param>
/// <param name="Dictionary">Tutuqsiz so'zlarni lug'at bo'yicha tuzatish (ozbek → özbek). Faqat oldinga yo'nalishda.</param>
public readonly record struct ConvertOptions(bool Reverse, bool Cyrillic, bool Dictionary);
