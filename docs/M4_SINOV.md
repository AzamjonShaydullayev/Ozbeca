# M4 — Real ilovalarda sinov natijalari

Sana: 2026-11-10 · Windows 11 Pro · Hotkey: **Ctrl+Alt+O** · Win+V tarixi yoqilgan

Har bir ilovada 4 narsa tekshirildi:

1. matn o'girildimi;
2. clipboard'dagi asl ma'lumot tiklandimi;
3. Win+V tarixiga oraliq matn tushmadimi;
4. Ctrl+Z eski matnni qaytardimi.

Sinovlar avtomatik skript bilan o'tkazildi. Har sinovda noyob matn ishlatildi (`O'zbekiston shahar choy g'isht NNN`). Win+V tarixi WinRT `Clipboard.GetHistoryItemsAsync` orqali tekshirildi.

| Ilova | O'girish | Clipboard tiklandi | Win+V toza | Ctrl+Z | Izoh |
|---|---|---|---|---|---|
| Notepad | ✓ | ✓ | ✓ | ✓ | |
| Word | ✓ | ✓ | ✓ | ✓ | Himoyalangan ko'rinish — pastga qarang |
| Excel — katak belgilangan | ✓ | ✓ | ✓ | ✓ | |
| Excel — F2 tahrirlash rejimi | ✓ | ✓ | ✓ | ✓ | |
| Chrome — input | ✓ | ✓ | ✓ | ✓ | |
| Chrome — textarea | ✓ | ✓ | ✓ | ✓ | |
| Google Docs | — | — | — | — | Sinalmadi: Google akkauntiga kirish kerak |
| Telegram Desktop | ✓ | | | | User sinadi. Qalin, chiziqli va premium emoji stillari saqlanadi |
| VS Code — tanlov bor | ✓ | ✓ | ✓ | ✓ | |
| VS Code — tanlov yo'q | ✓ | ✓ | ✓ | — | Butun qator nusxasi aniqlanadi, qator ikki marta qo'yilmaydi |
| Windows Terminal | ✓ | ✓ | ✓ | — | Terminalda undo yo'q |
| cmd (conhost) | ✓ | ✓ | ✓ | — | Belgilangan qism o'girildi |
| Explorer — faylni qayta nomlash | ✓ | ✓ | ✓ | ✓ | Ctrl+Z nomni qaytaradi |

**Terminal xavfsizligi:** `ping -t` ishlab turganda hotkey bosilsa, dastur faqat Ctrl+Insert va Shift+Insert yuboradi. Ping to'xtamadi (Windows Terminal ✓, cmd ✓).

### v1.1.0 (.NET Framework 4.8, ~0.1 MB) — qayta sinov, 2026-11-11

Jadvaldagi hamma ilovalar yangi exe bilan qayta sinaldi: Notepad, Word, Excel (2 rejim), Chrome (input, textarea), VS Code (tanlov bor va yo'q), Windows Terminal, cmd, Explorer. Natijalar avvalgidek ✓.

O'rnatish oynasi toza holatdan sinaldi. Exe'ga "internetdan yuklangan" belgisi qo'yilgan holda ham tekshirildi:
- dastur `%LocalAppData%\Programs\Ozbeca` ga ko'chiriladi va o'sha belgi nusxadan olib tashlanadi;
- `Özbeça.lnk` yorliqlari ish stoli va Start menyusida yaratiladi;
- avtomatik ishga tushirish yoqiladi;
- yorliq bosilsa, ishlab turgan nusxada sozlamalar ochiladi.

Bu sinovda topilgan xato: .NET Framework'ning `File.Delete` funksiyasi `fayl:Zone.Identifier` yo'lini qabul qilmaydi, shuning uchun o'rnatish to'xtab qolardi. Windows'ning `DeleteFile` funksiyasiga o'tildi.

## Sinov davomida topilgan va tuzatilgan xatolar

1. **Word — himoyalangan ko'rinish (Protected View).** Bu rejimda Ctrl+C umuman ishlamaydi, shuning uchun dastur "Matn belgilanmagan" der edi. Xatoni user topdi.
   - Endi Office oynasi sarlavhasida "Protected View", "Защищенный просмотр", "Read-Only" kabi belgi bo'lsa, clipboard'ga tegilmaydi va "Tahrirlashga ruxsat yo'q — avval «Tahrirlashni yoqish»ni bosing" degan xabar chiqadi.
   - Office'da nusxa olinmasa, xabar "Matn belgilanmagan yoki tahrirlashga ruxsat yo'q" bo'ladi.
2. **Explorer — clipboard tiklanmas edi.** Explorer paste'dan keyin o'sha matnni clipboard'ga qayta yozadi, shuning uchun dastur "boshqa dastur o'zgartirdi" deb hisoblardi. Endi clipboard'da hamon bizning matn tursa, asl nusxa tiklanadi.

3. **Telegram — stillar yo'qolar edi.** Bu xatoni ham user topdi. Qalin, chiziqli matn va premium emoji o'girilgandan keyin oddiy matnga aylanib qolardi. Endi Telegram'ning `application/x-td-field-text` va `application/x-td-field-tags` formatlari o'qiladi. Stillarning o'rni pozitsiyalar xaritasi bo'yicha yangi matnga suriladi va paste'da qaytariladi (`Ozbeca.Core/TelegramTags.cs`).

## Ma'lum cheklovlar

- **Terminallar:** konvertatsiya qilingan matn belgilangan joy o'rniga emas, kursor turgan joyga qo'yiladi. Terminal kiritish qatoridagi matnni almashtirib bo'lmaydi.
- **Word'dagi aralash format:** qalin, kursiv yoki rangli qismlar bitta formatga tushadi (HANDOFF 5.1).
- **Himoyalangan ko'rinishni aniqlash** sarlavha matniga bog'liq: ingliz, rus va o'zbek tillari qo'llab-quvvatlanadi. Boshqa tildagi Office'da umumiy xabar chiqadi.

## Yozish paytida o'girish — v1.2.0 (2026-11-11)

Avtomatik sinov: [`tools/e2e/run_autotype_tests.bat`](../tools/e2e). Unda odamga o'xshab, harfma-harf va Shift bilan yoziladi.
- 55 ta jumla, ikki tezlikda: 90 ms va 35 ms.
- Ctrl+V bilan 6 xil matn.
- Parol maydoni.
- VS Code kod rejimi.

Kutilgan natija dasturning o'z kodi bilan hisoblanadi.

| Ilova | Oddiy (90 ms) | Juda tez (35 ms) | Ctrl+V |
|---|---|---|---|
| Notepad | 55 / 55 | 53 / 55 | 6 / 6 |
| Chrome textarea | 55 / 55 | 55 / 55 | 6 / 6 |
| Chrome input | 51 / 51 | 51 / 51 | 5 / 5 |
| Word | 55 / 55 * | — | 6 / 6 |
| Chrome parol maydoni (o'girmasligi kerak) | 2 / 2 | | |
| VS Code (faqat qo'shtirnoq ichi) | 6 / 6 | | |

\* Word'dagi 4 ta mos kelmaslik skriptning o'zida edi: Word nusxa olganda oxiriga bo'sh paragraf va "aqlli probel" qo'shadi. O'girishning o'zi to'g'ri ishlagan, skript tuzatildi.

**Sinov paytida topilgan va tuzatilgan xatolar:**
- Hook ichida yuborilgan tugmalar hook'dan o'tmaydi, shuning uchun almashtirish hook tugagach yuboriladi.
- Almashtirish paytida foydalanuvchining keyingi harflari undan oldin kirib qolardi. Endi ular ushlab turiladi va keyin tartib bilan qaytariladi.
- Windows 11 Notepad tez kelgan Unicode harflarning tartibini almashtirardi, o'chirishdan keyingi birinchi harfni esa tashlab yuborardi. Endi aniq pauzalar va "tinchlanish" vaqti bor.
- `Thread.Sleep(4)` Windows'da ~15 ms uxlaydi va saqlagich muddatidan oldin ishlab ketardi.
- Parol maydonida probelli parol buzilishi mumkin edi. Endi UI Automation bilan tekshiriladi.

**Ma'lum holat:** Windows 11 Notepad'ning o'z avto-tuzatishi ba'zi so'zlarni o'zgartiradi (`tört` → `tort`). Bu Özbeça yopiq holda ham sodir bo'ladi.
