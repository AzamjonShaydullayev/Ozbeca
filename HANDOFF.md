# Özbeça — HANDOFF (Claude Code uchun)

> Bu hujjat loyihaning tahlili va oldingi chatda qabul qilingan qarorlarni o'z ichiga oladi.
> Kod yozishdan oldin uni **to'liq o'qib chiq**. Ishni bosqichma-bosqich, quyidagi M1 → M5 tartibida bajar.
> Har bir bosqich oxirida to'xtab, nima qilinganini qisqa hisobot qil.

---

## 0. Loyiha nima?

**Özbeça** — Windows uchun kichik desktop dastur. U fonda (tray'da) ishlaydi.

Foydalanuvchi kompyuterdagi **istalgan input oynada** (Notepad, Word, Chrome, Telegram, VS Code va boshqalar) matn yozadi. Matn eski alifboda, yangi alifboda yoki ikkalasi aralash bo'lishi mumkin. Keyin matnni **belgilaydi (select)** va **Ctrl+Alt+Space** ni bosadi. Belgilangan matn **o'sha joyning o'zida** yangi o'zbek lotin alifbosiga o'girilib, eski matn o'rniga qo'yiladi.

**Hozircha faqat shu funksiya.** Kirill, avtomatik o'girish, lug'at va boshqa qo'shimchalar yo'q.

- Ko'rinadigan nom: **Özbeça**
- Texnik identifikator (namespace, exe, papka nomlari): **Ozbeca**. Unicode harflarni yo'llar va identifikatorlarda ishlatmaslik uchun ASCII nom olinadi.

---

## 1. Kontekst: yangi alifbo (2026)

- Prezident lotin yozuviga asoslangan o'zbek alifbosini yangilash haqidagi qonunni **2026-yil 21-oktabrda** imzolagan. Undan oldin deputatlar 2026-yil iyulda qabul qilgan, Senat sentyabrda ma'qullagan.
- Eski alifbo: 26 harf, 3 ta harf birikmasi (sh, ch, ng) va tutuq belgisi.
- Yangi alifbo: **28 harf va 1 ta tutuq belgisi**.
- O'zgarishlar:
  - `Sh` → **Ş**
  - `Ch` → **Ç**
  - `Oʻ` → **Ö**
  - `Gʻ` → **Ğ**
- `Ng` alohida birlik sifatida alifbodan chiqarilgan, lekin so'zlarda ishlatilaveradi. Uni yozish qoidalari imlo qoidalarida belgilanadi.
- Qolgan harflar o'zgarmagan. Darsliklar 2027-yildan yangilanadi. Eski hujjatlar o'z kuchida qoladi.
- Islohot sababi: `oʻ` va `gʻ` dagi tutuq belgisini klaviaturada yozish qiyin, shu sabab amalda 10 dan ortiq xil yozilish ishlatiladi.

Manbalar:
- https://www.gazeta.uz/en/2026/09/10/uzb-alphabet/
- https://www.uzdaily.uz/en/mirziyoyev-signs-law-revising-uzbekistans-latin-alphabet/
- https://daryo.uz/2026/07/07/lotin-yozuviga-asoslangan-ozbek-alifbosi-ozgaradi-unda-harf-birikmalari-bolmaydi/

⚠️ **Ochiq savol:** yangi alifbodagi rasmiy tutuq belgisi aynan qaysi Unicode belgi ekani hali aniqlanmagan. Ehtimol `ʼ` (U+02BC). Buni qonun matnidan (lex.uz) tekshirish kerak. Kodda bu belgi **bitta konstanta** bo'lsin, keyin oson almashtirish uchun.

---

## 2. Qabul qilingan qarorlar

| # | Qaror | Sabab |
|---|---|---|
| D1 | Asosiy mexanizm: **clipboard orqali almashtirish** (nusxa olish → o'girish → qo'yish). | Notepad, Word, Chrome, Telegram, Electron va Qt ilovalari, VS Code — deyarli hamma joyda ishlaydi. UI Automation esa ko'p dasturlarda (brauzerlar, Telegram) ishonchsiz. |
| D2 | Texnologiya: **C# / .NET 8 + WPF**. | Win32 funksiyalariga (RegisterHotKey, SendInput, clipboard) to'liq kirish beradi, barqaror va chiroyli UI qilish mumkin. Electron ortiqcha og'ir. Python va AutoHotkey'ni antivirus ko'p bloklaydi va ular havaskorona ko'rinadi. |
| D3 | Hotkey: **Ctrl+Alt+Space**. Sozlamada o'zgartirish mumkin bo'lsin. | Bosish oson. Alt+Shift va Ctrl+Shift Windows'da til yoki klaviatura tartibini almashtiradi. Ctrl+Alt+harf esa ba'zi klaviatura tartiblarida AltGr bo'lib, harf yozib yuboradi. |
| D4 | Hotkey **`RegisterHotKey`** orqali ro'yxatdan o'tkaziladi, **low-level keyboard hook ishlatilmaydi**. | Hook bo'lmasa antivirus dasturni keylogger deb gumon qilmaydi. Kombinatsiya boshqa dasturda band bo'lsa, tizim o'zi xabar beradi. Hotkey'ni dastur egallaydi, shuning uchun u boshqa dasturdagi amalni ishga tushirmaydi. |
| D5 | UI minimal: tray ikonkasi, kichik sozlamalar oynasi va "O'girildi ✓" toast. | Foydalanuvchi talabi: ortiqcha narsa kerak emas. |
| D6 | O'girish mantig'i UI'dan **alohida, Windows'ga bog'liq bo'lmagan kutubxonada** yoziladi va unit testlar bilan himoyalanadi. | Dasturning to'g'ri ishlashi asosan shu qismga bog'liq. Uni Linux'da ham test qilish mumkin bo'ladi. |

> **2026-11-10 yangilanish (D3):** Ctrl+Alt+Space ishlab chiquvchi kompyuterida boshqa dasturda band chiqdi. Standart hotkey **Ctrl+Alt+O** ga o'zgartirildi (O → Ö, bir qo'l bilan bosiladi). Sozlamada istalganini tanlash mumkin.

---

## 3. Arxitektura

```
Ozbeca.sln
├─ src/
│  ├─ Ozbeca.Core/          net8.0 classlib — FAQAT o'girish mantig'i, hech qanday Windows API yo'q
│  │   └─ Transliterator.cs  (public static string ToNewAlphabet(string input))
│  └─ Ozbeca.App/           net8.0-windows WPF (UseWPF + UseWindowsForms — NotifyIcon uchun,
│      │                     yoki H.NotifyIcon.Wpf paketi)
│      ├─ App.xaml(.cs)      single-instance (Mutex), tray, hayot sikli
│      ├─ HotkeyService.cs   RegisterHotKey / WM_HOTKEY (yashirin message window, HwndSource)
│      ├─ ClipboardSwap.cs   clipboard'ni saqlash / tiklash, sequence number bilan kutish
│      ├─ InputSender.cs     SendInput (Ctrl+C/V, Ctrl+Ins/Shift+Ins, modifier tozalash)
│      ├─ ConvertEngine.cs   to'liq oqim (5-bo'lim), qayta kirishdan himoya
│      ├─ ToastWindow.xaml   fokusni olmaydigan kichik xabar oynasi
│      ├─ SettingsWindow.xaml
│      └─ Settings.cs        %AppData%\Ozbeca\settings.json
└─ tests/
   └─ Ozbeca.Core.Tests/    xUnit — 4-bo'limdagi barcha holatlar va undan ko'prog'i
```

---

## 4. O'girish qoidalari (Ozbeca.Core)

**Kirish:** istalgan matn — eski alifbo, yangi alifbo yoki aralash.
**Chiqish:** yangi alifbodagi matn.

### 4.1 Tutuq belgisi variantlari (A to'plami)
Bularning **hammasi** tutuq yoki harf belgisi deb tanilsin:
`'` U+0027, `` ` `` U+0060, `’` U+2019, `‘` U+2018, `ʻ` U+02BB, `ʼ` U+02BC, `´` U+00B4.

### 4.2 Qoidalar tartibi
0. **NFC normalizatsiya.** `o` + U+0308 kabi ajralgan belgilar `ö` ga birlashtiriladi.
1. **Himoyalangan qismlarga tegilmaydi:** URL (`http://`, `https://`, `www.`), email, `@username`, `#hashtag`. Bu qismlar o'zgarishsiz qoladi.
2. **o + A → ö** (bosh harf bo'lsa `O` + A → `Ö`), **faqat A dan keyin harf kelsa.**
   Sabab: `'kino'` kabi tirnoq ichidagi so'zda `o'` ö ga aylanib ketmasligi kerak. So'z oxirida `oʻ` bilan tugaydigan o'zbek so'zlari juda kam.
3. **g + A → ğ** (`G` → `Ğ`), **so'z oxirida ham.** `bog'`, `tog'`, `sog'` kabi so'zlar ko'p.
4. **Harf + A + harf** (A 2- va 3-qoidada ishlatilmagan bo'lsa) → A rasmiy tutuq belgisiga almashtiriladi: `ma'no` → `maʼno`.
   - `s'h` holati shu qoida bo'yicha **s + ʼ + h** bo'lib qoladi (`Is'hoq` → `Isʼhoq`), ş ga aylanmaydi.
   - Harflar orasida bo'lmagan A (tirnoq) o'zgarmaydi.
5. **sh → ş, ch → ç.** Bosh harf birinchi harf bo'yicha aniqlanadi:
   - `Sh` va `SH` → `Ş` / `Ç`
   - `sh` va `sH` → `ş` / `ç`
6. **ng** o'zgarmaydi.
7. Allaqachon yangi alifboda yozilgan harflarga (Ö Ğ Ş Ç) tegilmaydi. **Idempotentlik:** `f(f(x)) == f(x)`.
8. Qator oxirlari (`\r\n`), bo'shliqlar va boshqa belgilar aynan saqlanadi.

### 4.3 Majburiy test holatlari (kamida shular, imkon bo'lsa yuzlab so'z)
| Kirish | Kutilgan natija |
|---|---|
| `O'zbekiston` | `Özbekiston` |
| `o‘zbek` (U+2018) | `özbek` |
| `o`rta` (backtick) | `örta` |
| `oʻgʻil` | `öğil` |
| `O'G'IL` | `ÖĞIL` |
| `G'ALABA` | `ĞALABA` |
| `bog'` | `boğ` |
| `tog'da` | `toğda` |
| `shahar` / `Shahar` / `SHAHAR` | `şahar` / `Şahar` / `ŞAHAR` |
| `choy` / `CHOY` | `çoy` / `ÇOY` |
| `Chorshanba` | `Çorşanba` |
| `qo'shiq` | `qöşiq` |
| `g'isht` | `ğişt` |
| `ma'no` | `maʼno` |
| `Is'hoq` | `Isʼhoq` |
| `tong` | `tong` |
| `Bu 'kino' edi` | `Bu 'kino' edi` (o'zgarmaydi) |
| `'salom'` | `'salom'` |
| `Özbekiston` | `Özbekiston` (idempotent) |
| `O'zbekiston shaharlari Çirçiq` | `Özbekiston şaharlari Çirçiq` |
| `https://shop.uz/choy` | o'zgarmaydi |
| `info@shahar.uz` | o'zgarmaydi |
| `@shohruh #choyxona` | o'zgarmaydi |
| `maktab` | `maktab` |
| ko'p qatorli `\r\n` matn | qator oxirlari saqlanadi |

### 4.4 Bilib qabul qilingan cheklovlar
- Matn ichidagi inglizcha so'zlar ham o'giriladi (`shop` → `şop`). Hozircha qabul qilinadi.
- `don't` → `donʼt`. Bu ham qabul qilinadi.
- Tutuqsiz yozilgan `Ishoq` ni avtomatik tuzatib bo'lmaydi.

---

## 5. Hotkey bosilgandagi oqim (ConvertEngine)

1. **Qayta kirishdan himoya.** Oldingi o'girish hali tugamagan bo'lsa, yangi bosish e'tiborsiz qoldiriladi.
2. **Faol oynani eslab qolish.** Foreground oyna handle'i (hWnd) va uning class nomi saqlanadi. Jarayon davomida foreground oyna o'zgarsa, ish **bekor qilinadi**. Matn boshqa oynaga qo'yilib ketmasligi kerak.
3. **Alt menyu muammosini oldini olish.** Alt bosib turilgan holatda zararsiz "mask" tugma yuboriladi (masalan, VK 0xE8 — tayinlanmagan kod; AutoHotkey ham shu usuldan foydalanadi). Bu Alt qo'yib yuborilganda klassik oynalarda menyu satri faollashib qolmasligi uchun kerak.
4. **Modifier tugmalar qo'yib yuborilishini kutish.** Ctrl, Alt, Shift, Win va Space qo'yib yuborilguncha `GetAsyncKeyState` bilan ~1 soniyagacha kutiladi. Vaqt tugasa, ular uchun sun'iy key-up yuboriladi.
   **Bu juda muhim.** Aks holda oyna Ctrl+C o'rniga Ctrl+Alt+C ni oladi.
5. **Clipboard'ni saqlash.** Mavjud formatlarning hammasi saqlanadi (`EnumClipboardFormats` + `GlobalLock` orqali nusxa). GDI handle formatlari (CF_BITMAP va boshqalar) saqlanmaydi, ular o'rniga CF_DIB saqlanadi. `OpenClipboard` band bo'lsa, qisqa pauzalar bilan bir necha marta qayta urinadi.
6. **Clipboard'ni tozalash va eslab qolish.** Clipboard bo'shatiladi, `GetClipboardSequenceNumber` qiymati yozib olinadi.
7. **Nusxa olish:**
   - Oddiy oynalarda **Ctrl+C** yuboriladi (SendInput).
   - Terminal class'larida (`ConsoleWindowClass`, `CASCADIA_HOSTING_WINDOW_CLASS`, `mintty`, `PuTTY`) **Ctrl+Insert** yuboriladi. Terminalda Ctrl+C ishlab turgan jarayonni to'xtatib qo'yadi.
8. **Clipboard yangilanishini kutish.** Sequence number o'zgarguncha ~600 ms gacha kutiladi. **Qat'iy `Sleep` ishlatilmaydi.**
   - Hech narsa kelmasa, "Matn belgilanmagan" toast'i ko'rsatiladi, clipboard tiklanadi va ish tugaydi.
   - Kerak bo'lsa zaxira yo'l: Ctrl+C natija bermasa, bir marta Ctrl+Insert bilan urinib ko'riladi.
9. **VS Code holati.** VS Code'da hech narsa belgilanmasa, Ctrl+C butun qatorni nusxalaydi. Clipboard'da `vscode-editor-data` formati bo'lsa va uning ichida `"isFromEmptySelection":true` bo'lsa, bu "matn belgilanmagan" deb hisoblanadi. Aks holda qator dublikat bo'lib qoladi.
10. **O'girish.** CF_UNICODETEXT o'qiladi va `Transliterator.ToNewAlphabet` chaqiriladi. Natija kirish matni bilan bir xil bo'lsa, hech narsa qo'yilmaydi: clipboard tiklanadi va ixtiyoriy ravishda "O'zgarish yo'q" toast'i ko'rsatiladi.
11. **Natijani clipboard'ga qo'yish.** Faqat CF_UNICODETEXT va quyidagi "tarixga yozma" formatlari qo'yiladi:
    - `ExcludeClipboardContentFromMonitorProcessing`
    - `CanIncludeInClipboardHistory` = DWORD 0
    - `CanUploadToCloudClipboard` = DWORD 0

    Shunda oraliq matnlar Win+V tarixiga tushmaydi.
12. **Qo'yish.** Oddiy oynalarda **Ctrl+V**, terminallarda **Shift+Insert** yuboriladi.
13. **Clipboard'ni tiklash.** ~400 ms (sozlanadigan) kutilgandan keyin original clipboard tiklanadi. Office kabi ba'zi ilovalar clipboard'ni kechroq o'qiydi, shuning uchun darhol tiklanmaydi. Agar shu orada clipboard'ni **boshqa dastur** o'zgartirgan bo'lsa (sequence number kutilmagan tarzda o'zgargan bo'lsa), tiklanmaydi va userning yangi ma'lumoti ustidan yozilmaydi.
14. **Toast.** Kursor yonida "O'girildi ✓" chiqadi va ~1 soniyada yo'qoladi.

**Threading:** WM_HOTKEY UI thread'ga keladi. Ish esa alohida **STA** worker thread'da bajariladi (clipboard STA talab qiladi). UI bloklanmasligi kerak.

**Bekor qilish:** user o'girishni oddiy **Ctrl+Z** bilan bekor qila oladi, chunki u aslida oddiy paste amali.

### 5.1 Bilib qabul qilingan cheklovlar
- **Word'da aralash format.** Belgilangan qismda qalin, kursiv yoki rangli harflar aralash bo'lsa, o'girilgandan keyin hammasi bitta formatga tushadi, chunki oddiy matn qo'yiladi. Kelajakda Office uchun alohida yo'l qilish mumkin.
- **Administrator huquqidagi oynalar.** Bunday oynaga oddiy dastur SendInput yubora olmaydi (UIPI himoyasi). Bu holat aniqlansa, tushunarli toast ko'rsatilsin. Himoyani chetlab o'tishga urinilmaydi.

---

## 6. UI (minimal, lekin chiroyli)

- **Tray ikonkasi**, menyusi quyidagicha:
  - Yoqilgan / O'chirilgan
  - Sozlamalar…
  - Windows bilan birga ishga tushirish ☑
  - Chiqish
- **Sozlamalar oynasi** (kichik, zamonaviy, yorug' va qorong'i mavzuni qo'llab-quvvatlaydi):
  - Hotkey tanlash. Tanlangan kombinatsiya darhol `RegisterHotKey` bilan tekshiriladi. Band bo'lsa, xabar chiqadi.
  - Toast ko'rsatish ☑
  - Clipboard'ni tiklashdan oldingi kechikish (ms). "Kengaytirilgan" bo'limida, yashirin holda.
- **Toast oynasi** quyidagi stillarda yaratiladi: `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_TOPMOST`. **U hech qachon fokusni olmasligi kerak.**
- **Ishga tushganda:** hotkey ro'yxatdan o'tkazilmasa, tray'dan bildirishnoma chiqadi va sozlamalar oynasi ochiladi.
- **Autostart:** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- **Bitta nusxa:** dastur faqat bitta nusxada ishlaydi (named Mutex).
- **Ikonka:** Ö yoki Ş harfiga asoslangan sodda, zamonaviy belgi. `.ico` fayli 16, 24, 32, 48 va 256 px o'lchamlarida tayyorlanadi.

---

## 7. Tarqatish

- `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true` buyrug'i bilan **bitta `Ozbeca.exe`** chiqariladi.
  - Agar hajm juda katta bo'lsa (WPF uchun trimming ishlamaydi), muqobil variantlar: framework-dependent build, yoki .NET Framework 4.8 (Windows 10/11'da allaqachon o'rnatilgan, exe juda kichik bo'ladi). Bu tanlovni hajmni o'lchagandan keyin men bilan kelishib qil.
- Kod imzosi yo'q. Birinchi ishga tushirishda SmartScreen ogohlantirishi chiqishi normal holat.
- Installer hozircha shart emas. Kerak bo'lsa keyin Inno Setup bilan qilinadi.

---

## 8. Bosqichlar (milestones)

- **M1 — Core va testlar.** `Ozbeca.Core` + `Ozbeca.Core.Tests`. 4-bo'limdagi barcha qoidalar yoziladi, 4.3 dagi holatlar va qo'shimcha holatlar (100 dan ortiq so'z, idempotentlik, tasodifiy aralash matnlar) test qilinadi. Hammasi yashil bo'lishi shart.
- **M2 — Engine.** HotkeyService, InputSender, ClipboardSwap va ConvertEngine yoziladi. Hotkey ishlaydi va Notepad'da o'girish ishlaydi.
- **M3 — UI.** Tray, sozlamalar, toast, autostart va single-instance qo'shiladi.
- **M4 — Real ilovalarda sinov.** Quyidagi har bir ilovada tekshiriladi:
  - Notepad
  - Word
  - Excel (katakni tahrirlash rejimida va katak belgilangan holatda)
  - Chrome (oddiy input, textarea, Google Docs)
  - Telegram Desktop
  - VS Code (tanlov bor va tanlov yo'q holatda)
  - Windows Terminal
  - cmd
  - Explorer'da faylni qayta nomlash

  Har birida 4 narsa tekshiriladi: o'girish ishlaydimi, clipboard tiklanadimi, Win+V tarixi tozami, Ctrl+Z ishlaydimi. Natijalar jadvalga yoziladi.
- **M5 — Publish.** `Ozbeca.exe`, qisqa README (o'zbekcha) va ikonka tayyorlanadi.

---

## 9. Saboqlar va eslatmalar (tahlil jarayonidan)

- **Qat'iy kutish vaqti (`Sleep(100)`) — beqarorlikning asosiy sababi.** Doim clipboard sequence number yoki holatni kuzatib kutish kerak.
- **Modifier tugmalar.** Ular qo'yib yuborilmasdan yuborilgan Ctrl+C — eng ko'p uchraydigan xato.
- **Userning clipboard'i muqaddas.** U saqlanadi, tiklanadi, Win+V tarixi ifloslanmaydi va boshqa dasturning yangi ma'lumoti ustidan yozilmaydi.
- **Hech narsa belgilanmagan holat** (jumladan, VS Code'ning butun qatorni nusxalashi) alohida ishlanishi kerak.
- **Terminallarda Ctrl+C xavfli.** U yerda Ctrl+Insert va Shift+Insert ishlatiladi.
- **Alt+Shift va Ctrl+Shift** — Windows'ning til almashtirish tugmalari. Ular hotkey sifatida tanlanmaydi.
- **Keyboard hook yo'q, faqat `RegisterHotKey`.** Shunda antivirus dasturni bloklamaydi.
- **O'girish moduli dasturning yuragi.** UI'dan mustaqil bo'lishi va testlar bilan himoyalangan bo'lishi kerak.

---

## 10. Ochiq savollar (kod yozish davomida men bilan kelishiladi)

1. Rasmiy tutuq belgisi qaysi Unicode belgi (ehtimol U+02BC)? Qonun yoki imlo qoidalari matnidan tasdiqlanadi.
2. `ng` uchun imlo qoidalarida biror o'zgarish bormi? Hozircha o'zgartirilmaydi.
3. Tarqatish usuli: self-contained .NET 8 yoki .NET Framework 4.8? Exe hajmini ko'rgandan keyin hal qilinadi.
