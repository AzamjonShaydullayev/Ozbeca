# Özbeça — HANDOFF (Claude Code uchun)

> **Yangi chatda birinchi bo'lib shu faylni to'liq o'qing.** Unda loyihaning joriy holati, qabul qilingan
> qarorlar (va ularning sababi), ish tartibi, sinov usullari va oldingi sessiyalarda o'rganilgan saboqlar bor.
> Oxirgi yangilanish: **2026-11-11**, v1.3.0 release chiqqandan keyin.

---

## 0. Qisqacha holat

| | |
|---|---|
| **Nima** | Windows uchun tray dasturi: o'zbekcha matnni 2026-yilgi yangi lotin alifbosiga (Ö Ğ Ş Ç) o'giradi |
| **Oxirgi versiya** | **v1.3.0** — https://github.com/AzamjonShaydullayev/Ozbeca/releases/tag/v1.3.0 |
| **Repo** | https://github.com/AzamjonShaydullayev/Ozbeca (**ochiq**), branch `main`, lokal: `D:\projects\ozbeca` |
| **Platforma** | C#, WPF, **.NET Framework 4.8** (Windows 10/11 ichida bor) — bitta `Ozbeca.exe`, ~135 KB |
| **Testlar** | 385 unit test (xUnit, .NET 8) + real ilovalarda E2E: 402/408 |
| **Bosqichlar** | M1–M5 (asl reja) tugadi; undan keyin: avto-yozish, Ctrl+V, kod rejimi, premium UI, o'rnatish oynasi; v1.3.0 — pauzada o'girish, o'zini yangilash, UAC tuzatishi |
| **Foydalanuvchining kompyuteri** | **v1.3.0 o'rnatilgan va ishlab turibdi** (`%LocalAppData%\Programs\Ozbeca`, sinov paytida v1.2.0 dan yangilangan; build commit `de2555e`'dan oldingi — release exe bilan bir xil kod) |
| **Kutilayotgan fikr** | User v1.3.0 ni **boshqa kompyuterga** beradi (u yerda v1.2.0'da avto-yozish ishlamagan, Ctrl+Alt+O ishlagan). Ishlamasa — o'sha kompyuterning `%AppData%\Ozbeca\ozbeca.log` ini so'rang |

Ishlash yo'llari:
1. **Belgilab o'girish:** matnni belgilab <kbd>Ctrl+Alt+O</kbd> (clipboard orqali almashtirish).
2. **Yozish paytida:** so'z tugab probel/Enter bosilganda yoki 1 s pauzada o'zi o'giriladi (klaviatura hook'i). Standart — yoqilgan.
3. **Ctrl+V:** qo'yilgan ko'p so'zli matn o'girilib qo'yiladi.

---

## 1. Kontekst: yangi alifbo (2026) — manbalar bilan tasdiqlangan

- Qonun **OʻRQ-1184, 21.10.2026** (https://lex.uz/uz/docs/8548754), kuchga kirishi 22.04.2027. 28 harf + 1 tutuq.
- O'zgargan harflar: `Sh`→**Ş** (U+015E), `Ch`→**Ç** (U+00C7), `Oʻ`→**Ö** (U+00D6), `Gʻ`→**Ğ** (U+011E) — qonundagi jadvalda oldindan tuzilgan (precomposed) belgilar.
- **Rasmiy tutuq: `ʼ` U+02BC** — qonun HTML matnidan kod nuqtasi bilan tekshirilgan (asl HANDOFF'dagi ochiq savol yopildi).
- `Ng` alohida birlik sifatida chiqarildi, lekin so'zlarda `n`+`g` bo'lib yozilaveradi — kod `ng` ga tegmaydi.
- **1995 imlo qoidalari** (https://lex.uz/docs/1625271): `Isʼhoq`, `asʼhob` (17-band, s va h orasida tutuq); `moʻjiza` — oʻ dan keyin tutuq **qo'yilmaydi**.
- Yangi imlo qoidalari hali e'lon qilinmagan (Vazirlar Mahkamasi tayyorlamoqda) — chiqsa, `Transliterator` qayta ko'rib chiqiladi.

---

## 2. Qarorlar jurnali

Asl qarorlar (D1–D6) va keyin o'zgarganlari. **User bilan kelishilmagan o'z qarorlarini kiritmang** — user buni aniq so'ragan:
"o'zingcha qaror qabul qilib noto'g'ri qilib qo'ymagin". Qoida yoki xatti-harakat o'zgarsa — avval manbadan tekshiring yoki so'rang.

| # | Qaror | Holat / sabab |
|---|---|---|
| D1 | Belgilab o'girish clipboard orqali (Ctrl+C → o'girish → Ctrl+V, clipboard tiklanadi) | ✅ amalda |
| D2 | ~~.NET 8 + WPF~~ → **.NET Framework 4.8 + WPF** | user so'rovi: 68 MB → 0.1 MB (v1.1.0). Core `netstandard2.0`, exe ichiga linklanadi |
| D3 | ~~Ctrl+Alt+Space~~ → **Ctrl+Alt+O** | Ctrl+Alt+Space user kompyuterida band edi; user tanladi (O → Ö) |
| D4 | ~~Keyboard hook yo'q~~ → hotkey `RegisterHotKey`; **avto-yozish uchun WH_KEYBOARD_LL + WH_MOUSE_LL** | user avto-yozishni so'radi va hook variantini tanladi (antivirus/README va'dasi haqida ogohlantirilgan). Hook faqat avto-yozish yoqilganda o'rnatiladi |
| D5 | Minimal, lekin **premium UI** | user so'rovi: Mica, toggle, guruhlangan qatorlar |
| D6 | O'girish mantig'i UI'dan alohida, testlar bilan | ✅ `Ozbeca.Core` |
| D7 | Toast matni **"Özbeça ✓"** | user qarori |
| D8 | `'tong'` — tirnoq bilan boshlangan so'z oxiridagi `g'` = yopuvchi tirnoq (o'zgarmaydi) | user tasdiqlagan |
| D9 | Qo'shimcha tutuq variantlari `′` U+2032, `＇` U+FF07; `ș`→`ş`; tutuqsiz `Ishoq`/`ashob` → `Isʼhoq`/`asʼhob` | user tasdiqlagan |
| D10 | Avto-yozish **standart yoqilgan** | user qarori |
| D11 | Avto-yozish **probel va Enter**da; Enter'da faqat parol maydoni bo'lmasa (UIA `IsPassword`) | user: "Yaxshimisan?" + Enter o'zgarmay ketardi |
| D12 | **Kod muharrirlarida — faqat qo'shtirnoq `"…"` ichi** o'giriladi; terminal — butunlay o'chiq | user g'oyasi |
| D13 | Ctrl+V: faqat bo'shliq bor matn (bitta so'z = ehtimol parol); kodga o'xshash tokenlar o'zgarmaydi | xavfsizlik |
| D14 | Telegram stillari (qalin, chiziqli, premium emoji) saqlanadi | user topgan muammo |
| D15 | Birinchi ishga tushirishda **o'rnatish oynasi**: `%LocalAppData%\Programs\Ozbeca` ga ko'chirish, yorliqlar, autostart, avto-yozish | user so'rovi; installer yo'q, admin kerak emas |
| D16 | Exe hajmi 0.1 MB qoladi (sun'iy kattalashtirilmaydi) | user "virusga o'xshaydi" degandi, tushuntirilgach rozi bo'ldi |
| D17 | Ko'k SmartScreen oynasi qoladi, README'da tushuntiriladi | imzo pullik; Azure Artifact Signing individual uchun faqat AQSh/Kanada; Microsoft Store (bepul, MSIX) — kelajak uchun variant |
| D18 | Repo **ochiq**, commitlarda GitHub **noreply** pochta | user so'rovi; shaxsiy pochta ochiq repoda ko'rinmasin |
| D19 | **Pauzada o'girish:** so'z yozilib 1 s (`AutoType.PauseMs`) kutilsa — probel/Enter'siz o'giriladi; so'z bufferda qoladi | user so'rovi (v1.3.0) |
| D20 | **Yangilash:** yangi exe ishga tushsa eski nusxa yopiladi (`Local\Ozbeca.Exit`, eskilarida Kill), o'rnatilgan exe almashtiriladi, `--updated` bilan qayta ishga tushadi | user: "eskini o'chir, boshqa balo" bo'lmasin (v1.3.0) |

---

## 3. Arxitektura

```
Ozbeca.sln
├─ src/Ozbeca.Core/            netstandard2.0 — Windows'ga bog'liq emas, exe ichiga <Compile Include> bilan kiradi
│   ├─ Transliterator.cs        ToNewAlphabet(text) va ToNewAlphabet(text, out map) — pozitsiyalar xaritasi
│   ├─ TokenBuffer.cs           avto-yozish: probelgacha token; CodeMode (qo'shtirnoq ichi), TryCompleteAtEnter, TryConvertAtPause
│   ├─ CodeGuard.cs             LooksLikeCode(token), ConvertProse(text) — kodga tegmaslik
│   ├─ TelegramTags.cs          application/x-td-field-text + x-td-field-tags (QDataStream BE) — stillarni surish
│   └─ Polyfills.cs             IsExternalInit (record'lar uchun)
├─ src/Ozbeca.App/             net48 WPF (+WinForms faqat NotifyIcon uchun)
│   ├─ App.xaml(.cs)            single-instance (Mutex + EventWaitHandle "sozlamalarni och"), tray, o'rnatish oqimi
│   ├─ HotkeyService.cs         RegisterHotKey, message-only oyna (clipboard egasi ham)
│   ├─ ConvertEngine.cs         belgilab o'girish oqimi (STA thread), Protected View/admin/terminal aniqlash
│   ├─ ClipboardSwap.cs         saqlash/tiklash, no-history formatlar, VS Code bo'sh tanlov
│   ├─ InputSender.cs           SendInput (EngineInput belgisi bilan), modifier kutish, mask key
│   ├─ AutoType.cs              klaviatura/sichqoncha hook, almashtirish, ushlab-qaytarish, UIA parol, Ctrl+V
│   ├─ Install.cs               o'zini ko'chirish, yangilash (UpdateOlder), IShellLinkW yorliqlar (Unicode!), Zone.Identifier'ni o'chirish
│   ├─ Settings.cs              %AppData%\Ozbeca\settings.json (DataContractJsonSerializer, BOM'siz o'qish) + Autostart (HKCU Run)
│   ├─ Theme.cs / Styles.xaml   ranglar, Mica (build ≥ 22621), Toggle/Group/Row/KeyCap uslublari
│   ├─ SettingsWindow, WelcomeWindow, ToastWindow (caret yonida), TrayMenu
│   ├─ Native.cs, Compat.cs     P/Invoke; net48'da yo'q funksiyalar o'rnini bosuvchilar
│   └─ Log.cs                   %AppData%\Ozbeca\ozbeca.log (1 MB dan oshsa qayta boshlanadi)
├─ tests/Ozbeca.Core.Tests/    xUnit, net8.0
├─ tools/e2e/                  real ilovalarda avto-yozish sinovi (run_autotype_tests.bat → autotype_e2e.ps1)
├─ tools/make_icon.py, make_demo.py — ikonka va README rasmlari
└─ docs/                       M4_SINOV.md (sinov natijalari), README rasmlari
```

### 3.1 O'girish qoidalari (Transliterator) — tartib
0. NFC. 1. Himoya: URL (`http(s)://`, `www.`), email, `@user`, `#tag` — tegilmaydi.
2. `o`+A+harf → `ö` (A = `' ` ’ ‘ ʻ ʼ ´ ′ ＇`). 3. `g`+A → `ğ` (so'z oxirida ham; `'tong'` istisno).
4. Tutuqsiz `Ishoq`/`ashob` (so'z boshi) → `Isʼhoq`/`asʼhob`. 5. harf+A+harf → `ʼ` (`Is'hoq` → `Isʼhoq`).
6. `sh`/`ch` → `ş`/`ç` (bosh harf birinchi harfga qarab). 7. `ng` o'zgarmaydi. 8. `ș` → `ş`.
Idempotent: `f(f(x)) == f(x)`. `ʻ`/`ʼ` .NET'da `char.IsLetter == true` — `IsLetter` ularni chiqarib tashlaydi.

### 3.2 Belgilab o'girish oqimi (ConvertEngine) — HANDOFF asl 5-bo'limi asosida
Qayta kirish himoyasi → foreground oyna eslab qolinadi (o'zgarsa bekor) → Office sarlavhasida "Protected View /
Защищенный просмотр / Read-Only" bo'lsa "Tahrirlashga ruxsat yo'q" → Alt mask (VK 0xE8) → modifier'lar qo'yib
yuborilishini kutish → clipboard saqlash → bo'shatish → Ctrl+C (terminalda Ctrl+Insert) → sequence o'zgarishini kutish
(600 ms, zaxira Ctrl+Insert) → VS Code bo'sh tanlov tekshiruvi → Telegram formati bo'lsa stillar bilan o'girish →
o'zgarmasa "O'zgarish yo'q" → no-history formatlar bilan qo'yish → Ctrl+V (terminalda Shift+Insert) → 400 ms dan keyin
tiklash (sequence bizniki bo'lsa **yoki matn hamon biznikiga teng** bo'lsa — Explorer clipboard'ni qayta yozadi) → toast.

### 3.3 Avto-yozish (AutoType) — eng nozik qism
- **Rejimlar** (oyna jarayoni bo'yicha): `Off` (terminallar, KeePass/1Password/Bitwarden, mstsc, VM'lar, admin oynalar,
  o'z oynalarimiz), `Code` (VS Code, Cursor, JetBrains, Visual Studio, Notepad++ va b. — faqat `"…"` ichi, Ctrl+V yo'q),
  `Prose` (qolganlari).
- **Trigger:** probel (so'z + probel almashtiriladi), Enter (faqat Prose, UIA bilan parol emasligi tekshirilgach),
  kod rejimida yopuvchi `"`.
- **So'z unutiladi:** sichqoncha bosilsa, strelka/End/PgUp/Tab/Esc/Delete, Ctrl/Alt/Win kombinatsiya, boshqa oyna.
  Enter/Home — yangi qator (kod rejimi uchun "holat ma'lum").
- **Almashtirish:** Backspace×N + Unicode (KEYEVENTF_UNICODE) harflar + ushlangan tugma. **Hook ichida emas**, alohida
  sender thread'da (hook ichida yuborilgan tugmalar hook'lardan o'tmaydi).
- **Tartib kafolati:** almashtirish yuborilib, hook'dan o'tib (`_pending`) va ilova qayta ishlab ulgurguncha
  (`_settling`, 40 ms) kelgan foydalanuvchi tugmalari **ushlanadi** (`_held`) va keyin tartib bilan qayta yuboriladi
  (`ReplayInput` belgisi). Saqlagich: 1500 ms.
- **Pauzalar:** har tugma juftligi orasida 6 ms (aniq, Stopwatch bilan), backspace'lardan keyin 30 ms.
- **Belgilar (dwExtraInfo):** `OwnInput 0x0E8A0E8A` (almashtirish), `ReplayInput 0x0E8A0E8B`,
  `InputSender.EngineInput 0x0E8A0E8C` (hotkey oqimining Ctrl+C/V — avto-yozish ularga tegmaydi).
  Boshqa "injected" tugmalar (ekran klaviaturasi, test skriptlari) oddiy tugma kabi qayta ishlanadi.
- **Parol:** Win32 `ES_PASSWORD` (tez) + UIA `AutomationElement.FocusedElement.IsPassword` (400 ms timeout,
  javob bo'lmasa — "parol" deb hisoblanadi). Natija maydon uchun keshlanadi (sichqoncha/Tab/oyna — qayta).
- **Ctrl+V:** V ushlanadi → clipboard'dagi matn (bo'shliq bo'lsa) `CodeGuard.ConvertProse` / Telegram yo'li →
  no-history bilan qo'yiladi → V yuboriladi → 500 ms dan keyin asl clipboard tiklanadi.
- **Pauza (v1.3.0):** har harf/Backspace'da `DispatcherTimer` (`PauseMs` = 1000) qayta boshlanadi. Tick'da: navbat bo'sh
  (`_pending`/`_settling`/`_held` = 0), oyna o'sha, modifier'lar (Shift ham) bosilmagan, Win32 parol emas →
  `TokenBuffer.TryConvertAtPause` → `Replace(..., trailing: bo'sh)` (UIA parol tekshiruvi shu yerda). So'z bufferda
  o'girilgan holda qoladi; probelda yana tekshiriladi (idempotent — ikki marta yuborilmaydi). Timer hook bilan bitta
  UI thread'da — poyga yo'q.
- **Oyna o'chiq sababi logda:** `AutoType: <jarayon> — o'chiq (istisno ro'yxatida | terminal | administrator oynasi)`.
  Ishga tushish qatorida `avto-yozish=` va `admin=` bor — boshqa kompyuterdagi muammoni log bilan aniqlang.

### 3.4 O'zini yangilash (Install.UpdateOlder, v1.3.0)
`App.OnStartup` boshida, **mutex'dan oldin** (mutex eski nusxada): o'rnatilgan nusxa bo'lmasak →
`Ozbeca*` nomli jarayonlar (`OriginalFilename` = Ozbeca bilan tasdiqlanadi, yo'l `QueryFullProcessImageName` bilan) →
bittasi bir xil/yangiroq bo'lsa — eski xatti-harakat (sozlamalarni och). Aks holda, o'rnatilgan exe eski yoki eski nusxa
ishlab turgan bo'lsa: eski nusxalar to'xtatiladi (`Local\Ozbeca.Exit` signali → 3 s → `Kill`; v1.2.0'da signal yo'q,
tray'da "arvoh" belgi sichqoncha ustiga borguncha qoladi) → o'rnatilgan bo'lsa `CopySelf` (band bo'lsa 10×300 ms qayta) va
`Ozbeca.exe --updated` ishga tushiriladi; o'rnatilmagan bo'lsa shu nusxa davom etadi, autostart yozuvi bor bo'lsa unga
ko'chiriladi. Yangisi balloon chiqaradi: "X versiyaga yangilandi". `--updated` argumenti hotkey sifatida o'qilmaydi.
**Versiya solishtiruvi FileVersion bo'yicha** — har release'da `<Version>` ni oshirish SHART, aks holda yangilanmaydi.

---

## 4. Sinov

```bash
dotnet test                                   # 392 unit test
dotnet build src/Ozbeca.App -c Release        # → src/Ozbeca.App/bin/Release/net48/Ozbeca.exe
```

**E2E (real ilovalar):** `tools\e2e\run_autotype_tests.bat` (~35 daqiqa) yoki
`powershell -ExecutionPolicy Bypass -File tools\e2e\autotype_e2e.ps1 [-Only Notepad|Chrome|Word|VS] [-Limit N] [-DryRun]`.
- Notepad, Chrome (textarea, input, parol maydoni), Word, VS Code (.js, kod rejimi); oddiy (90 ms) va tez (35 ms).
- Odamga o'xshab yozadi: `keybd_event` + `VkKeyScan`, Shift bilan, tasodifiy kechikish. **`WScript.SendKeys` ishlatmang** —
  u NumLock bosadi, Chrome/Electron'ga Ctrl+C yetkazmaydi.
- Kutilgan natija dasturning o'z kodi bilan (`Ozbeca.Core.TokenBuffer`, exe'dan yuklanadi).
- Natija: `tools\e2e\results\autotype_<vaqt>.md`, oxirgisining yo'li `LATEST.txt` (results/ va work/ git'ga kirmaydi).
- Oxirgi to'liq natija (v1.2.0): **402/408** — Notepad tez 53/55, Word 4 ta (skript artefakti, tuzatildi). Batafsil: `docs/M4_SINOV.md`.

**Muhim shartlar:**
- Sinov faqat **faol** Windows sessiyasida ishlaydi: `query session` → `Active`. User masofadan ulanib, sessiya `Disc`
  bo'lsa — hech qanday oynaga bosib bo'lmaydi va hamma test "yiqiladi" (bu dastur xatosi emas).
- Sinov paytida user klaviatura/sichqonchaga tegmasligi kerak; avval userdan so'rang. Skript "aralashuv"ni (boshqa oyna
  oldinga chiqsa) alohida belgilaydi.
- Oldingi sinovdan qolgan modal oynalar (Notepad "Cannot find file") keyingi sinovni buzadi — tekshirib yoping.
- Word'ni COM orqali ochmang (yashirin nusxa qotib qoladi) — docx `tools/e2e/mkdocx.py` bilan yasaladi.

**M4 (belgilab o'girish)**: Notepad, Word, Excel, Chrome, VS Code, Telegram, Terminal, cmd, Explorer — ✅ (`docs/M4_SINOV.md`).

---

## 5. Ish tartibi (user bilan kelishilgan)

- **Til:** o'zbekcha, qisqa va aniq. Har javob oxirida diagnostika qatori (global CLAUDE.md).
- **Git:** har mantiqiy o'zgarishdan keyin **commit + push** (so'ramasdan), o'zbekcha "nima va nega" xabar.
  `git config` faqat repo ichida — **hech qachon `--global` emas** (Faktura GitLab buziladi). Attribution qatorlari yo'q.
- **GitHub Release** — ommaviy amal: auto-mode bloklaydi, **har safar user ruxsatini so'rang** ("Ha, yarat" degach).
  Release'ga faqat `Ozbeca.exe` (net48, ~135 KB). Versiya: `src/Ozbeca.App/Ozbeca.App.csproj` → `<Version>`.
  Release'dan oldin exe'ni **commit'dan keyin** qayta build qiling (ProductVersion'da commit hash bo'ladi).
- **Obsidian jurnal:** muhim o'zgarishlar `C:\Users\user\Documents\Brain\Claude\logs\task_log.md` ga.
- User'ning kompyuterida sinov o'rnatishlarini qoldirmang: `%LocalAppData%\Programs\Ozbeca`, `%AppData%\Ozbeca`,
  yorliqlar (`Özbeça.lnk` — wildcard `?zbe?a.lnk` bilan qidiring), HKCU Run `Ozbeca`.
- User'ning oynalariga ko'r-ko'rona tugma yubormang (masalan, Notepad varaqlarini yopish uchun) — to'xtang va ayting.

---

## 6. Saboqlar (qayta bosmang)

- `.bat` fayllar **CRLF + ASCII** bo'lsin (`.gitattributes` bor) — LF bilan `cmd` qatorlarni buzadi.
- PowerShell 5.1 skriptlari **UTF-8 BOM** bilan (aks holda o'zbekcha/Ö buziladi); `-replace` va `,` aralashsa parametr bo'linadi.
- Bash heredoc orqali Python bilan C#/PS tahrirlashda `\r`, `\n`, `\"` buziladi — **Edit vositasi** yoki alohida `.py` fayl ishlating.
- PowerShell'da o'zgaruvchi nomlari katta-kichik harfni farqlamaydi (`$undo` = `$Undo` parametri).
- `WScript.Shell` COM (dynamic) — ANSI: `Özbeça.lnk` → `Ozbeca.lnk`, kirillcha yo'llar buziladi → `IShellLinkW`.
- net48: `File.Delete("x:Zone.Identifier")` → NotSupportedException → Win32 `DeleteFile`.
- `DataContractJsonSerializer` BOM'li faylni o'qimaydi; konstruktor chaqirilmaydi → `[OnDeserializing]` standartlar.
- `Thread.Sleep(4)` ≈ 15 ms (taymer aniqligi).
- Win11 Notepad: tez Unicode harflar tartibini almashtiradi, o'chirishdan keyingi birinchi harfni tashlaydi,
  Unicode'ni oddiy tugmalardan kechroq qayta ishlaydi; o'zining **avto-tuzatishi** bor (`tört` → `tort`).
- Word Protected View'da Ctrl+C ishlamaydi; Explorer rename paste'dan keyin clipboard'ni qayta yozadi.
- Toast sichqoncha yonida emas, **caret** yonida (user'da 2 monitor).
- **UAC o'chiq kompyuterda** hamma oyna elevated ko'rinadi — `IsElevatedWindow` o'zimiz elevated bo'lsak false qaytaradi
  (v1.2.0'da avto-yozish boshqa kompyuterda shu sabab jim o'chgan, Ctrl+Alt+O ishlagan). Oyna nega o'chiq — logda.
- `settings.json` sinov skriptlari tomonidan almashtiriladi — skript oxirida tiklaydi.

---

## 7. Ma'lum cheklovlar

- Oxirgi so'zdan keyin sichqoncha bilan "Yuborish" bosilsa — o'sha so'z o'girilmaydi (probel/Enter kerak).
- Inglizcha so'zlar ham o'giriladi (`check` → `çeck`) — tray'dan avto-yozishni o'chirish mumkin.
- Kod rejimida sichqoncha bilan qator o'rtasiga o'tilsa — Enter'gacha hech narsa o'girilmaydi (ataylab).
- Word'da belgilab o'girishda aralash format bitta formatga tushadi.
- Terminalda belgilab o'girish matnni almashtirmaydi, kursor joyiga qo'yadi.
- Himoyalangan ko'rinishni aniqlash sarlavha matniga bog'liq (ingliz/rus/o'zbek).
- Juda tez yozishda (35 ms/harf) Notepad'da ~4% xato; oddiy tezlikda 0.
- Telegram'da avto-yozish va Ctrl+V real sinalmagan (belgilab o'girish — ✅, user tasdiqlagan).
- Google Docs sinalmagan (login kerak).
- Pauza so'z o'rtasida bo'lsa, `Ishoq`/`ashob` maxsus qoidasi ishlamay qolishi mumkin (`Isho`+pauza → `Işo`).

---

## 8. Keyingi qadamlar (takliflar — user bilan kelishiladi)

1. User v1.3.0 ni boshqa kompyuterda sinaydi (UAC tuzatishi u yerda tasdiqlanmagan — faqat koddan topilgan sabab;
   o'z kompyuterimizda sinov: yangilash ✅, pauza va probel ✅) → ishlamasa logni so'rab, sababni logdan aniqlash.
   Ehtimoliy boshqa sabab: sekin kompyuterda UIA parol tekshiruvi 400 ms dan oshib, maydon "parol" deb keshlanadi
   (logda `parol maydoni — so'z o'girilmadi`).
2. Telegram'da avto-yozish va Ctrl+V ni real sinash (user bilan; xabar tasodifan yuborilmasin).
3. Sozlamalarda **istisno ilovalar ro'yxati** (user o'zi qo'shadigan) — hozir ro'yxat kodda.
4. **Microsoft Store** (MSIX, bepul individual akkaunt) — ko'k SmartScreen oynasisiz o'rnatish; user akkaunti kerak.
5. Excel/Telegram uchun E2E kengaytirish; Win10 (Mica'siz) ko'rinishini tekshirish; qorong'i mavzuda skrinshot.
6. Yangi imlo qoidalari e'lon qilinsa — `ng`, tutuq va boshqa qoidalarni qayta tekshirish.
