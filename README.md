<div align="center">

<img src="docs/icon.png" width="96" alt="Özbeça ikonkasi">

# Özbeça

**Belgilangan matnni bitta tugma bilan yangi o'zbek alifbosiga o'giradi.**<br>
Notepad, Word, Telegram, Chrome, VS Code va boshqa ilovalarda — matn o'sha joyning o'zida almashadi.

[![Yuklab olish](https://img.shields.io/badge/⬇_Yuklab_olish-Ozbeca.exe-0099B5?style=for-the-badge)](https://github.com/AzamjonShaydullayev/Ozbeca/releases/latest/download/Ozbeca.exe)

![Windows 10/11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows)
![Versiya](https://img.shields.io/github/v/release/AzamjonShaydullayev/Ozbeca?label=versiya&color=0099B5)
![O'rnatish shart emas](https://img.shields.io/badge/o'rnatish-shart_emas-2ea44f)

</div>

<p align="center"><img src="docs/demo.png" width="760" alt="Oldin va keyin: O'zbekiston → Özbekiston, Bog'da choy → Boğda çoy"></p>

---

## Bu nima?

2026-yil 21-oktabrda lotin yozuviga asoslangan o'zbek alifbosi yangilandi. Yangi alifboda 4 ta harf boshqacha yoziladi:

| Eski | Yangi | Misol |
|:---:|:---:|---|
| **Sh** sh | **Ş** ş | shahar → **ş**ahar |
| **Ch** ch | **Ç** ç | choy → **ç**oy |
| **Oʻ** oʻ | **Ö** ö | oʻzbek → **ö**zbek |
| **Gʻ** gʻ | **Ğ** ğ | gʻisht → **ğ**işt |

Bu harflarni klaviaturada yozish noqulay. **Özbeça** shu muammoni hal qiladi:
1. Odatdagidek yozasiz.
2. Matnni belgilaysiz.
3. <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>O</kbd> ni bosasiz.

Matn o'sha joyning o'zida yangi alifboga o'tadi.

---

## ⬇️ Yuklab olish va ishga tushirish (1 daqiqa)

### 1. Yuklab oling

👉 **[Ozbeca.exe ni yuklab olish](https://github.com/AzamjonShaydullayev/Ozbeca/releases/latest/download/Ozbeca.exe)** (atigi ~0.1 MB)

Bitta kichik fayl, qo'shimcha hech narsa o'rnatish shart emas. Windows 10 va 11 da kerakli hamma narsa allaqachon bor.

### 2. Ishga tushiring

Yuklab olingan `Ozbeca.exe` ni ikki marta bosing.

> [!NOTE]
> **Ko'k "Windows protected your PC" ("Windows kompyuteringizni himoya qildi") oynasi chiqadi — bu normal holat.**
>
> Ochish uchun **«More info» / «Подробнее»** → **«Run anyway» / «Выполнить в любом случае»** ni bosing. Bu faqat bir marta so'raladi.
>
> <details>
> <summary>Bu oyna nega chiqadi? Xavfsizmi?</summary>
>
> Bu Windows'ning **SmartScreen** himoyasi. U internetdan yuklangan va ikkita belgisi bo'lgan har qanday dasturda chiqadi:
> - **raqamli imzo yo'q** — imzo uchun har yili pullik sertifikat kerak bo'ladi;
> - **hali kam odam yuklab olgan.**
>
> Oyna dasturda virus borligini anglatmaydi. U faqat Windows bu dasturni hali "tanimasligini" bildiradi.
>
> Özbeça xavfsiz:
> - **kodi to'liq ochiq**, bu sahifada istalgan kishi ko'rib chiqishi mumkin;
> - **internetga ulanmaydi**, hech qanday ma'lumot yubormaydi;
> - **administrator huquqi so'ramaydi**;
> - **klaviaturani kuzatmaydi**: Windows'ning oddiy `RegisterHotKey` funksiyasi ishlatiladi, keylogger usuli emas.
>
> Xohlasangiz, faylni [VirusTotal](https://www.virustotal.com/) saytida 70 dan ortiq antivirus bilan tekshirib ko'rishingiz mumkin.
> </details>

### 3. O'rnatish oynasida «Tayyor» ni bosing

Birinchi ochilganda dastur nimalarni qilish kerakligini so'raydi. Hammasi oldindan belgilangan, shunchaki **Tayyor** ni bossangiz bo'ldi:

<p align="center"><img src="docs/welcome.png" width="440" alt="O'rnatish oynasi"></p>

| Belgi | Nima qiladi |
|---|---|
| **Kompyuterga o'rnatish** | Dastur `%LocalAppData%\Programs\Ozbeca` ga ko'chiriladi. Yuklab olingan faylni keyin o'chirib tashlasangiz bo'ladi |
| **Ish stolida yorliq** | Ish stolida **Özbeça** belgisi paydo bo'ladi |
| **Start menyusiga qo'shish** | Start menyusida "Özbeça" deb qidirsangiz topiladi |
| **Avtomatik ishga tushirish** | Kompyuter yonganda dastur o'zi ochiladi va jim kutib turadi |

Administrator huquqi so'ralmaydi.

Shundan keyin dastur oyna ochmaydi. U ekranning pastki o'ng burchagida, soat yonida <img src="docs/icon.png" width="16"> **Ö** belgisi bo'lib turadi. Belgi ko'rinmasa, **^** (yashirin belgilar) tugmasini bosing.

---

## ✍️ Qanday foydalaniladi

1. Istalgan joyda matn yozing: Word, Telegram, brauzer, Notepad...
2. O'girmoqchi bo'lgan qismni **belgilang**. Hammasini belgilash uchun <kbd>Ctrl</kbd>+<kbd>A</kbd>.
3. <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>O</kbd> ni bosing.
4. Matn o'girildi, kursor yonida **"Özbeça ✓"** chiqadi.

**Bekor qilmoqchimisiz?** Oddiy <kbd>Ctrl</kbd>+<kbd>Z</kbd> ni bosing, eski matn qaytadi.

> 💡 Eslab qolish oson: **O** → **Ö**.

---

## ✨ Imkoniyatlar

- **Deyarli hamma joyda ishlaydi.** Notepad, Word, Excel, Chrome, Telegram, VS Code, terminal, hatto Explorer'da fayl nomini o'zgartirishda ham.
- **Telegram'da stillar saqlanadi.** Qalin, chiziqli matn va premium emoji o'girilgandan keyin ham joyida qoladi.
- **Aralash matnni tushunadi.** Eski yozuv, yangi yozuv yoki ikkalasi aralash bo'lsa ham to'g'ri o'giradi. Allaqachon yangi alifboda yozilgan qism o'zgarmaydi.
- **Tutuq belgisining hamma xilini taniydi:** `'` `` ` `` `’` `‘` `ʻ` `ʼ` `´` `′`. Kim qanday yozgan bo'lsa ham, `o'`, `o‘`, `o`` va boshqalar `ö` ga aylanadi.
- **Kerakmas joyga tegmaydi.** Havolalar, email, `@username` va `#hashtag` o'zgarmaydi. Tirnoqni tutuq deb adashtirmaydi: `'kino'` va `'tong'` shundayligicha qoladi.
- **Clipboard'ingiz buzilmaydi.** Oldin nima nusxalagan bo'lsangiz, o'girishdan keyin o'sha joyida qoladi. Win+V tarixiga ortiqcha narsa tushmaydi.
- **Yengil va xavfsiz.** Internetga ulanmaydi, administrator huquqi so'ramaydi, klaviaturani kuzatmaydi (keylogger emas).

---

## 🔤 Qanday o'giradi

| Yozilgan | Natija | Qoida |
|---|---|---|
| `O'zbekiston`, `o‘zbek`, `o`rta` | `Özbekiston`, `özbek`, `örta` | o + tutuq → **ö** |
| `G'ALABA`, `bog'`, `tog'da` | `ĞALABA`, `boğ`, `toğda` | g + tutuq → **ğ** (so'z oxirida ham) |
| `shahar`, `Shahar`, `SHAHAR` | `şahar`, `Şahar`, `ŞAHAR` | sh → **ş** |
| `choy`, `Chorshanba` | `çoy`, `Çorşanba` | ch → **ç** |
| `ma'no`, `san'at`, `she'r` | `maʼno`, `sanʼat`, `şeʼr` | harflar orasidagi tutuq → rasmiy **ʼ** |
| `Is'hoq`, `as'hob` | `Isʼhoq`, `asʼhob` | s va h alohida tovush bo'lsa, ş bo'lmaydi |
| `Ishoq`, `ashob` | `Isʼhoq`, `asʼhob` | tutuqsiz yozilgani ham tuzatiladi |
| `tong`, `keng`, `dengiz` | o'zgarmaydi | **ng** o'z holicha qoladi |
| `Bu 'kino' edi`, `'tong'` | o'zgarmaydi | tirnoq tutuq deb olinmaydi |
| `https://shop.uz/choy`, `info@shahar.uz` | o'zgarmaydi | havola va email |

Qoidalar quyidagi rasmiy manbalarga asoslangan:
- [OʻRQ-1184-son Qonun (21.10.2026)](https://lex.uz/uz/docs/8548754) — 28 harf va tutuq belgisi **ʼ** (U+02BC);
- [Oʻzbek tilining asosiy imlo qoidalari (1995)](https://lex.uz/docs/1625271).

---

## ⚙️ Sozlamalar

Sozlamalarni ochishning uchta yo'li bor:
- soat yonidagi **Ö** belgisini bosish;
- shu belgi ustida o'ng tugmani bosib **Sozlamalar…** ni tanlash;
- ish stolidagi **Özbeça** yorlig'ini bosish.

Oynani **−** bilan kichraytirsangiz, u yana Ö belgisiga yashirinadi. Dastur esa ishlashda davom etadi.

<p align="center"><img src="docs/settings.png" width="420" alt="Sozlamalar oynasi"></p>

- **Hotkey.** Maydonni bosing va yangi kombinatsiyani bosing. U darhol tekshiriladi: boshqa dastur egallagan bo'lsa, ogohlantirish chiqadi.
- **O'girilganda xabar ko'rsatish.** "Özbeça ✓" xabarini yoqadi yoki o'chiradi.
- **Windows bilan birga ishga tushirish.** Kompyuter yonganda dastur o'zi ochiladi.
- **Kengaytirilgan → kechikish.** Biror ilovada matn qo'yilmay qolsa, qiymatni oshiring (masalan, 400 → 800).

**Tray menyusi** (Ö belgisi ustida o'ng tugma):
- **Yoqilgan** — vaqtincha o'chirib qo'yish uchun.
- **Sozlamalar…**
- **Windows bilan birga ishga tushirish**
- **Chiqish**

---

## ✅ Qayerlarda sinalgan

| Ilova | Natija |
|---|---|
| Notepad, Word, Excel (katak va tahrirlash rejimi) | ✅ |
| Chrome (input, textarea), VS Code | ✅ |
| Telegram Desktop (stillar bilan) | ✅ |
| Windows Terminal, cmd | ✅ |
| Explorer — fayl nomini o'zgartirish | ✅ |

Batafsil natijalar: [docs/M4_SINOV.md](docs/M4_SINOV.md).

---

## ❓ Savollar va muammolar

<details>
<summary><b>Hech narsa bo'lmadi / "Matn belgilanmagan" chiqdi</b></summary>

Avval matnni belgilang (sichqoncha bilan yoki <kbd>Ctrl</kbd>+<kbd>A</kbd>), keyin tugmalarni bosing. Shu xabar chiqmasa, tray'dagi Ö belgisida **Yoqilgan** belgisi turganini tekshiring.
</details>

<details>
<summary><b>"Tahrirlashga ruxsat yo'q" chiqdi (Word)</b></summary>

Hujjat **himoyalangan ko'rinishda** (Protected View) ochilgan. Bu internetdan yoki Telegram'dan yuklangan fayllarda bo'ladi. Yuqoridagi sariq qatordagi **«Tahrirlashni yoqish» / «Разрешить редактирование»** tugmasini bosing va qayta urinib ko'ring.
</details>

<details>
<summary><b>"… boshqa dasturda band" degan xabar chiqdi</b></summary>

Tanlangan tugmalar kombinatsiyasini boshqa dastur egallab olgan. Sozlamalar oynasi o'zi ochiladi: hotkey maydonini bosing va boshqa kombinatsiyani tanlang, masalan <kbd>Win</kbd>+<kbd>Alt</kbd>+<kbd>Space</kbd>.
</details>

<details>
<summary><b>Dasturni qayta ochsam faqat sozlamalar chiqyapti</b></summary>

Bu to'g'ri: dastur allaqachon ishlab turibdi, shuning uchun ikkinchi nusxa ochilmaydi, faqat sozlamalar ko'rsatiladi. Matnni o'girish uchun hech narsa ochish shart emas: matnni belgilab <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>O</kbd> ni bosing.
</details>

<details>
<summary><b>"Administrator huquqidagi oynada ishlamaydi"</b></summary>

Administrator sifatida ochilgan dasturlarga Windows oddiy dasturlarning tugma yuborishini xavfsizlik uchun taqiqlaydi. Bu Windows'ning himoyasi, uni chetlab o'tmaymiz.
</details>

<details>
<summary><b>Word'da qalin yoki rangli harflar oddiy bo'lib qoldi</b></summary>

Word'da o'girilgan qism oddiy matn sifatida qo'yiladi, shuning uchun belgilangan joydagi aralash formatlash (qalin, kursiv, rang) bitta formatga tushadi. Telegram'da bu muammo yo'q, u yerda stillar saqlanadi.
</details>

<details>
<summary><b>Terminalda matn almashmadi, oxiriga qo'shildi</b></summary>

Terminal kiritilgan matnni almashtirishga imkon bermaydi. O'girilgan matn kursor turgan joyga qo'yiladi. Terminalda dastur xavfsiz tugmalarni ishlatadi (Ctrl+Insert / Shift+Insert), shuning uchun ishlab turgan buyruq to'xtab qolmaydi.
</details>

<details>
<summary><b>Inglizcha so'zlar ham o'zgarib ketdi</b></summary>

Dastur so'zning qaysi tilda ekanini bilmaydi: `shop` → `şop`, `don't` → `donʼt`. Faqat o'zbekcha qismni belgilab o'giring.
</details>

---

## 🗑️ O'chirish

1. Ö belgisi ustida o'ng tugma → **Windows bilan birga ishga tushirish** belgisini olib tashlang.
2. Shu menyuda **Chiqish** ni bosing.
3. Ish stolidagi va Start menyusidagi **Özbeça** yorliqlarini o'chiring.
4. <kbd>Win</kbd>+<kbd>R</kbd> → `%LocalAppData%\Programs\Ozbeca` → papkani o'chiring (dasturning o'zi).
5. <kbd>Win</kbd>+<kbd>R</kbd> → `%AppData%\Ozbeca` → papkani o'chiring (sozlamalar).

---

## 🛠️ Dasturchilar uchun

C#, WPF, **.NET Framework 4.8**. U Windows 10/11 tarkibida bor, shuning uchun exe ~0.1 MB va foydalanuvchi hech narsa o'rnatmaydi. O'girish mantig'i alohida, Windows'ga bog'liq bo'lmagan `netstandard2.0` kutubxonada joylashgan va 300 dan ortiq test bilan himoyalangan. Bu kod exe ichiga kompilyatsiya qilinadi, natijada bitta fayl chiqadi.

```
src/Ozbeca.Core/         o'girish qoidalari (Transliterator), Telegram stillari (TelegramTags)
src/Ozbeca.App/          tray, hotkey (RegisterHotKey), clipboard, sozlamalar, toast, o'rnatish oynasi
tests/Ozbeca.Core.Tests/ xUnit testlar (.NET 8)
tools/                   ikonka va README rasmlarini yasash
```

```bash
dotnet test
```

Exe'ni yasash (natija `src/Ozbeca.App/bin/Release/net48/Ozbeca.exe` da chiqadi):

```bash
dotnet build src/Ozbeca.App -c Release
```

Qanday ishlaydi: hotkey bosilganda dastur clipboard'ni saqlab qo'yadi. Keyin belgilangan matnni nusxalaydi (Ctrl+C), uni o'giradi va qayta qo'yadi (Ctrl+V). Oxirida asl clipboard'ni tiklaydi. Klaviatura hook'i ishlatilmaydi, faqat `RegisterHotKey`.

Takliflar va xatolar uchun **[Issues](https://github.com/AzamjonShaydullayev/Ozbeca/issues)** bo'limiga yozing.
