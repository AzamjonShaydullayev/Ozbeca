# Özbeça bot - serverga o'rnatish

Bot Linux (x64) serverda bitta fayl sifatida ishlaydi (.NET o'rnatish shart emas). Log - `journalctl`.

## 1. Yig'ish (Windows'da)

```powershell
pwsh deploy/bot/publish.ps1
```

Natija: `publish/ozbeca-bot.zip` (ichida `Ozbeca.Bot`). Zip'ni serverga yuboring (`scp`).

## 2. Birinchi o'rnatish (serverda, root)

```bash
useradd -r -s /usr/sbin/nologin ozbeca
mkdir -p /opt/ozbeca-bot/data/work
unzip ozbeca-bot.zip -d /opt/ozbeca-bot
chmod +x /opt/ozbeca-bot/Ozbeca.Bot
cp .env.example /opt/ozbeca-bot/.env        # keyin to'ldiring: BOT_TOKEN va ADMIN_IDS
chmod 600 /opt/ozbeca-bot/.env
chown -R ozbeca:ozbeca /opt/ozbeca-bot
cp ozbeca-bot.service /etc/systemd/system/
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
unzip -o ozbeca-bot.zip -d /opt/ozbeca-bot
chmod +x /opt/ozbeca-bot/Ozbeca.Bot
chown ozbeca:ozbeca /opt/ozbeca-bot/Ozbeca.Bot
systemctl start ozbeca-bot && journalctl -u ozbeca-bot -n 30
```

## 5. Orqaga qaytarish

```bash
systemctl stop ozbeca-bot
mv /opt/ozbeca-bot/Ozbeca.Bot.old /opt/ozbeca-bot/Ozbeca.Bot
systemctl start ozbeca-bot
```

Ma'lumotlar bazasi (`/opt/ozbeca-bot/data/bot.db`) yangilashda o'zgarmaydi.

## Foydali

```bash
systemctl status ozbeca-bot
systemctl restart ozbeca-bot
```
