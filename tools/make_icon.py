"""Özbeça ikonkasini yasaydi: src/Ozbeca.App/Assets/ozbeca.ico (16, 24, 32, 48, 256 px).

Ishlatish: python tools/make_icon.py   (Pillow kerak)
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "src" / "Ozbeca.App" / "Assets" / "ozbeca.ico"
FONT = "C:/Windows/Fonts/segoeuib.ttf"
TOP = (43, 208, 232)    # #2BD0E8 — gradient boshi (yorqin firuza)
BOTTOM = (0, 120, 145)  # #007891 — gradient oxiri (bayroqdagi moviyning to'qi)
FG = (255, 255, 255, 255)
SIZES = [16, 24, 32, 48, 256]


def gradient(s: int) -> Image.Image:
    """Diagonal gradient (chap-tepadan o'ng-pastga)."""
    g = Image.new("RGBA", (s, s))
    px = g.load()
    for y in range(s):
        for x in range(s):
            t = (x + y) / (2 * (s - 1))
            px[x, y] = tuple(round(a + (b - a) * t) for a, b in zip(TOP, BOTTOM)) + (255,)
    return g


def render(size: int) -> Image.Image:
    scale = 8 if size <= 48 else 2  # supersampling — kichik o'lchamda ham silliq
    s = size * scale
    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, s - 1, s - 1), radius=int(s * 0.24), fill=255)
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    img.paste(gradient(s), (0, 0), mask)
    if size >= 32:  # yaltiroq: tepa yarmida oq nur (kichik o'lchamda harfni xiralashtirmasin)
        gloss = Image.new("L", (s, s), 0)
        gd = ImageDraw.Draw(gloss)
        for y in range(s // 2):
            gd.line((0, y, s, y), fill=round(70 * (1 - y / (s / 2)) ** 1.5))
        white = Image.new("RGBA", (s, s), (255, 255, 255, 255))
        img = Image.composite(white, img, Image.composite(gloss, Image.new("L", (s, s), 0), mask))
    d = ImageDraw.Draw(img)
    # Kichik o'lchamlarda harf kattaroq — o'qilishi uchun
    font = ImageFont.truetype(FONT, int(s * (0.78 if size <= 24 else 0.68)))
    l, t, r, b = d.textbbox((0, 0), "Ö", font=font)
    x, y = (s - (r - l)) / 2 - l, (s - (b - t)) / 2 - t
    if size >= 48:  # yengil soya — harf "ko'tarilib" tursin
        d.text((x, y + s * 0.015), "Ö", font=font, fill=(0, 70, 90, 90))
    d.text((x, y), "Ö", font=font, fill=FG)
    return img.resize((size, size), Image.LANCZOS)


def main() -> None:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    images = [render(sz) for sz in SIZES]
    images[-1].save(OUT, format="ICO", sizes=[(sz, sz) for sz in SIZES], append_images=images[:-1])
    images[-1].save(OUT.with_suffix(".png"))
    print(f"OK: {OUT}")


if __name__ == "__main__":
    main()
