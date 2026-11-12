# Özbeça bot - serverga o'rnatish

Bot Linux (x64) serverda bitta fayl sifatida ishlaydi (.NET o'rnatish shart emas). Log - `journalctl`.

## 1. Yig'ish (Windows'da)

```powershell
powershell -ExecutionPolicy Bypass -File deploy/bot/publish.ps1   # yoki: pwsh deploy/bot/publish.ps1
```

Natija: `publish/ozbeca-bot.zip` - ichida `Ozbeca.Bot`, `libe_sqlite3.so` (SQLite, alohida fayl), `.env.example` va `ozbeca-bot.service` (`.pdb` yo'q). Zip'ni serverga yuboring (`scp`).

## 2. Birinchi o'rnatish (serverda, root)

`unzip` bo'lmasa: `apt install unzip`.

```bash
useradd -r -s /usr/sbin/nologin ozbeca
mkdir -p /opt/ozbeca-bot/data/work
unzip ozbeca-bot.zip -d /opt/ozbeca-bot
chmod +x /opt/ozbeca-bot/Ozbeca.Bot
cp /opt/ozbeca-bot/.env.example /opt/ozbeca-bot/.env        # keyin to'ldiring: BOT_TOKEN va ADMIN_IDS
chmod 600 /opt/ozbeca-bot/.env
chown -R ozbeca:ozbeca /opt/ozbeca-bot
cp /opt/ozbeca-bot/ozbeca-bot.service /etc/systemd/system/
systemctl daemon-reload && systemctl enable --now ozbeca-bot
journalctl -u ozbeca-bot -f
```

`.env` ni tahrirlang (`nano /opt/ozbeca-bot/.env`): `BOT_TOKEN` - BotFather bergan token, `ADMIN_IDS` - vergul bilan admin Telegram ID'lari. Token hech qayerga commit qilinmaydi.

## 3. BotFather sozlamalari

- `/setname` - `Özbeça`
- `/setdescription` va `/setabouttext` - bot tavsifi
- `/setinline` - placeholder yangi alifboda (masalan: `Matn yozing`)
- `/setinlinefeedback` - `Enabled`

Buyruqlar (`/start`, `/sozlama`, `/help`) bot ishga tushganda o'zi o'rnatadi.

## 4. Yangilash

```bash
systemctl stop ozbeca-bot
cp /opt/ozbeca-bot/Ozbeca.Bot /opt/ozbeca-bot/Ozbeca.Bot.old     # orqaga qaytish uchun
cp /opt/ozbeca-bot/libe_sqlite3.so /opt/ozbeca-bot/libe_sqlite3.so.old
unzip -o ozbeca-bot.zip -d /opt/ozbeca-bot
chmod +x /opt/ozbeca-bot/Ozbeca.Bot
chown ozbeca:ozbeca /opt/ozbeca-bot/Ozbeca.Bot /opt/ozbeca-bot/libe_sqlite3.so
systemctl start ozbeca-bot && journalctl -u ozbeca-bot -n 30
```

`ozbeca-bot.service` o'zgargan bo'lsa: `cp /opt/ozbeca-bot/ozbeca-bot.service /etc/systemd/system/ && systemctl daemon-reload`.

## 5. Orqaga qaytarish

```bash
systemctl stop ozbeca-bot
mv /opt/ozbeca-bot/Ozbeca.Bot.old /opt/ozbeca-bot/Ozbeca.Bot
mv /opt/ozbeca-bot/libe_sqlite3.so.old /opt/ozbeca-bot/libe_sqlite3.so   # yangi versiyada o'zgargan bo'lishi mumkin - birga qaytariladi
systemctl start ozbeca-bot
```

Ma'lumotlar bazasi (`/opt/ozbeca-bot/data/bot.db`) yangilashda o'zgarmaydi.

## Foydali

```bash
systemctl status ozbeca-bot
systemctl restart ozbeca-bot
```
