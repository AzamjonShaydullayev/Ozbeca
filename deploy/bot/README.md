# Özbeça bot - serverga o'rnatish

Bot Linux (x64) serverda bitta fayl sifatida ishlaydi (.NET o'rnatish shart emas, PHP botlarga aralashmaydi). Joyi:
`/www/wwwroot/azamjondev.uz/lichkali/ozbecabot`, xizmat: `ozbecabot`, log: `journalctl -u ozbecabot`.

Boshqa botlarni himoya qilish (`ozbecabot.service`): CPU ko'pi bilan 50%, `Nice=10`, RAM 300 MB, `IOWeight=20`.
Port ochmaydi (long polling), nginx/aaPanel sozlamalariga tegmaydi. Papka `ozbeca:ozbeca 700` - nginx `.env` va `bot.db` ni
bera olmaydi (403). **aaPanel'da `lichkali` papkasiga rekursiv huquq/egani o'zgartirmang** - `ozbecabot` ham o'zgarib, bot buziladi.

## 1. Yig'ish (Windows'da)

```powershell
powershell -ExecutionPolicy Bypass -File deploy/bot/publish.ps1 -EnvFrom <lokal .env>   # -EnvFrom ixtiyoriy
```

Natija: `publish/ozbecabot.zip` - `Ozbeca.Bot`, `libe_sqlite3.so`, `.env.example`, `ozbecabot.service`, `install.sh`
(va `-EnvFrom` berilsa tayyor `.env`, unda token bor - zip'ni hech kimga bermang).

## 2. O'rnatish va yangilash (serverda, root)

Lokal bot shu token bilan ishlayotgan bo'lsa, avval uni to'xtating (bitta token ikki joyda ishlamaydi).

```bash
rm -rf /root/ozbecabot-new && unzip -q /root/ozbecabot.zip -d /root/ozbecabot-new
bash /root/ozbecabot-new/install.sh
```

`install.sh` birinchi marta `ozbeca` user'ini, papkani, `.env` ni va xizmatni yaratadi; keyingi safar faqat fayllarni
almashtiradi (eski fayl `*.old` bo'lib qoladi, `.env` va `bot.db` o'zgarmaydi). `.env` da token bo'sh bo'lsa:
`nano /www/wwwroot/azamjondev.uz/lichkali/ozbecabot/.env`, keyin `systemctl start ozbecabot`.

## 3. BotFather sozlamalari

- `/setname` - `Özbeça`
- `/setdescription` va `/setabouttext` - bot tavsifi
- `/setinline` - placeholder yangi alifboda (masalan: `Matn yozing`)
- `/setinlinefeedback` - `Enabled`

Buyruqlar (`/start`, `/sozlama`, `/oyin`, `/statistika`, `/harflar`, `/help`) bot ishga tushganda o'zi o'rnatadi. `WORK_DIR` fayl tizimi ildizi, `DB_PATH` katalogi yoki uning otasi bo'lsa bot ishga tushmaydi (ish papkasi startda tozalanadi).

## 4. Orqaga qaytarish

```bash
cd /www/wwwroot/azamjondev.uz/lichkali/ozbecabot
systemctl stop ozbecabot
mv Ozbeca.Bot.old Ozbeca.Bot && mv libe_sqlite3.so.old libe_sqlite3.so
systemctl start ozbecabot
```

## Foydali

```bash
systemctl status ozbecabot
journalctl -u ozbecabot -f
systemctl show ozbecabot -p MemoryCurrent -p CPUUsageNSec
```
