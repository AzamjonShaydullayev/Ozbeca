"""Özbeça ikonkasini yasaydi: src/Ozbeca.App/Assets/ozbeca.ico (16, 24, 32, 48, 256 px).

Ishlatish: python tools/make_icon.py   (Pillow kerak)
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "src" / "Ozbeca.App" / "Assets" / "ozbeca.ico"
FONT = "C:/Windows/Fonts/segoeuib.ttf"
BG = (0, 153, 181, 255)  # bayroqdagi moviy (#0099B5)
FG = (255, 255, 255, 255)
SIZES = [16, 24, 32, 48, 256]


def render(size: int) -> Image.Image:
    scale = 8  # supersampling — kichik o'lchamda ham silliq
    s = size * scale
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((0, 0, s - 1, s - 1), radius=int(s * 0.22), fill=BG)
    # Kichik o'lchamlarda harf kattaroq — o'qilishi uchun
    font = ImageFont.truetype(FONT, int(s * (0.78 if size <= 24 else 0.70)))
    l, t, r, b = d.textbbox((0, 0), "Ö", font=font)
    d.text(((s - (r - l)) / 2 - l, (s - (b - t)) / 2 - t), "Ö", font=font, fill=FG)
    return img.resize((size, size), Image.LANCZOS)


def main() -> None:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    images = [render(sz) for sz in SIZES]
    images[-1].save(OUT, format="ICO", sizes=[(sz, sz) for sz in SIZES], append_images=images[:-1])
    images[-1].save(OUT.with_suffix(".png"))
    print(f"OK: {OUT}")


if __name__ == "__main__":
    main()
