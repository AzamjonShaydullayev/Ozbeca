"""README uchun rasmlar: docs/icon.png va docs/demo.png ("oldin → keyin").

Ishlatish: python tools/make_demo.py   (Pillow kerak; ikonka uchun avval tools/make_icon.py)
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
DOCS = ROOT / "docs"
FONT = "C:/Windows/Fonts/segoeui.ttf"
FONT_B = "C:/Windows/Fonts/segoeuib.ttf"
BLUE = (0, 153, 181)

ROWS = [
    ("O'zbekiston shaharlari", "Özbekiston şaharlari"),
    ("Bog'da choy ichdik", "Boğda çoy içdik"),
    ("G'isht, qo'shiq, ma'no", "Ğişt, qöşiq, maʼno"),
    ("Is'hoq — https://shop.uz/choy", "Isʼhoq — https://shop.uz/choy"),
]


def demo() -> None:
    scale = 2
    w, row_h, pad = 980, 64, 32
    h = pad * 2 + 56 + row_h * len(ROWS)
    img = Image.new("RGB", (w * scale, h * scale), (255, 255, 255))
    d = ImageDraw.Draw(img)
    f = ImageFont.truetype(FONT, 24 * scale)
    fb = ImageFont.truetype(FONT_B, 18 * scale)
    fa = ImageFont.truetype(FONT_B, 26 * scale)
    d.rounded_rectangle((0, 0, w * scale - 1, h * scale - 1), radius=18 * scale, outline=(225, 228, 232), width=2 * scale)
    col1, col2, arrow_x = pad, 540, 480
    d.text((col1 * scale, pad * scale), "OLDIN (eski yozuv)", font=fb, fill=(120, 120, 120))
    d.text((col2 * scale, pad * scale), "KEYIN (yangi alifbo)", font=fb, fill=BLUE)
    y = pad + 56
    for old, new in ROWS:
        d.text((col1 * scale, y * scale), old, font=f, fill=(60, 60, 60))
        d.text((arrow_x * scale, (y - 2) * scale), "→", font=fa, fill=BLUE)
        d.text((col2 * scale, y * scale), new, font=f, fill=(20, 20, 20))
        y += row_h
    img.resize((w, h), Image.LANCZOS).save(DOCS / "demo.png", optimize=True)


def main() -> None:
    DOCS.mkdir(exist_ok=True)
    ico = Image.open(ROOT / "src" / "Ozbeca.App" / "Assets" / "ozbeca.ico")
    ico.size = (256, 256)
    ico.load()
    ico.convert("RGBA").resize((128, 128), Image.LANCZOS).save(DOCS / "icon.png", optimize=True)
    demo()
    print("OK: docs/icon.png, docs/demo.png")


if __name__ == "__main__":
    main()
