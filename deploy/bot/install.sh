#!/usr/bin/env bash
# Özbeça botni o'rnatadi yoki yangilaydi (root bilan): bash install.sh
# Zip ochilgan papkadan ishga tushiriladi; boshqa botlar va nginx/aaPanel sozlamalariga tegmaydi.
set -euo pipefail
D=/www/wwwroot/azamjondev.uz/lichkali/ozbecabot
SRC=$(cd "$(dirname "$0")" && pwd)
[ "$(id -u)" = 0 ] || { echo "root bilan ishga tushiring"; exit 1; }
[ "$SRC" != "$D" ] || { echo "zip'ni $D ga emas, vaqtinchalik papkaga oching"; exit 1; }
[ "$(uname -m)" = x86_64 ] || { echo "faqat x86_64 server"; exit 1; }

id ozbeca >/dev/null 2>&1 || useradd -r -s /usr/sbin/nologin ozbeca
mkdir -p "$D/data/work"

systemctl stop ozbecabot 2>/dev/null || true
for f in Ozbeca.Bot libe_sqlite3.so; do
  [ -f "$D/$f" ] && cp -p "$D/$f" "$D/$f.old"   # orqaga qaytish uchun
done
install -m 755 "$SRC/Ozbeca.Bot" "$D/Ozbeca.Bot"
install -m 644 "$SRC/libe_sqlite3.so" "$D/libe_sqlite3.so"
install -m 644 "$SRC/.env.example" "$D/.env.example"
if [ ! -f "$D/.env" ]; then
  if [ -f "$SRC/.env" ]; then cp "$SRC/.env" "$D/.env"; else cp "$D/.env.example" "$D/.env"; fi
fi
chmod 600 "$D/.env"

# Faqat ozbeca o'qiydi: nginx (www) bu papkadan .env va bot.db ni bera olmaydi (403).
chown -R ozbeca:ozbeca "$D"
chmod 700 "$D"
runuser -u ozbeca -- test -x "$D/Ozbeca.Bot" || { echo "ozbeca $D ga kira olmayapti (yuqori papkalar huquqi)"; exit 1; }

install -m 644 "$SRC/ozbecabot.service" /etc/systemd/system/ozbecabot.service
systemctl daemon-reload
systemctl enable ozbecabot >/dev/null

if grep -q '^BOT_TOKEN=.\+' "$D/.env"; then
  systemctl start ozbecabot
  sleep 3
  systemctl --no-pager --lines=15 status ozbecabot || true
else
  echo "BOT_TOKEN bo'sh: nano $D/.env  keyin: systemctl start ozbecabot"
fi
