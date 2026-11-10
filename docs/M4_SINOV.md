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
