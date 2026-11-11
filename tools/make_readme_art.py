"""README uchun rasmlar — ilova uslubida (firuza gradient, yaltiroq, yumaloq).

Yasaydi (docs/):
  icon.png          — ilova ikonkasi
  banner.png        — sahifa boshidagi katta banner
  download.png      — "Yuklab olish" tugmasi
  alphabet.png      — 4 ta yangi harf kartochkalari
  icons/*.png       — imkoniyatlar ikonkalari
  demo-type.gif     — yozish paytida o'girish (jonli)
  demo-hotkey.gif   — belgilab o'girish, Ctrl+Alt+O (jonli)
  settings.png, welcome.png, apps.png — docs/raw/ dagi oyna rasmlari, Windows 11 oyna ramkasida

Ishlatish: python tools/make_readme_art.py   (Pillow kerak; Windows shriftlari: Segoe UI Variable, Segoe Fluent Icons)
docs/raw/*.png — ilova oynalarining ekrandan tashqarida olingan rasmlari (PowerShell: exe'ni LoadFrom qilib, RenderTargetBitmap).
"""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
DOCS = ROOT / "docs"
UI_FONT = "C:/Windows/Fonts/SegUIVar.ttf"
ICON_FONT = "C:/Windows/Fonts/SegoeIcons.ttf"

HERO = [(28, 200, 226), (0, 153, 181), (0, 101, 124)]  # #1CC8E2 → #0099B5 → #00657C (ilovadagi HeroBrush)
ACCENT = (0, 153, 181)
ACCENT_LIGHT = (184, 243, 255)
INK = (26, 26, 26)
MUTED = (92, 92, 92)
WHITE = (255, 255, 255)

WEIGHTS = {"regular": 400, "semibold": 600, "bold": 700}


def font(size: float, weight: str = "regular", display: bool = False) -> ImageFont.FreeTypeFont:
    f = ImageFont.truetype(UI_FONT, round(size))
    f.set_variation_by_axes([WEIGHTS[weight], 36 if display else 10.5])
    return f


def icon_font(size: float) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(ICON_FONT, round(size))


# ---------------------------------------------------------------- yordamchilar

class Blend:
    """ImageDraw o'rnini bosadi, lekin yarim shaffof ranglarni ostidagi rasm bilan aralashtiradi
    (Pillow RGBA rasmga to'g'ridan-to'g'ri chizganda alfani almashtiradi, aralashtirmaydi)."""

    def __init__(self, img: Image.Image):
        self.img = img
        self._measure = ImageDraw.Draw(img)

    def textbbox(self, *a, **k):
        return self._measure.textbbox(*a, **k)

    def textlength(self, *a, **k):
        return self._measure.textlength(*a, **k)

    def __getattr__(self, name):
        def call(*a, **k):
            layer = Image.new("RGBA", self.img.size, (0, 0, 0, 0))
            getattr(ImageDraw.Draw(layer), name)(*a, **k)
            self.img.alpha_composite(layer)
        return call


def gradient(w: int, h: int, stops, diagonal: bool = True) -> Image.Image:
    """Ko'p nuqtali gradient: diagonal — chap-tepadan o'ng-pastga, aks holda tepadan pastga."""
    pal = []
    for i in range(256):
        t = i / 255 * (len(stops) - 1)
        k = min(int(t), len(stops) - 2)
        f = t - k
        pal += [round(a + (b - a) * f) for a, b in zip(stops[k], stops[k + 1])]
    vert = Image.linear_gradient("L").resize((w, h))
    t = ImageChops.add(vert, Image.linear_gradient("L").rotate(90, expand=True).resize((w, h)), scale=2) if diagonal else vert
    t = t.convert("P")
    t.putpalette(pal)
    return t.convert("RGB")


def rounded_mask(w: int, h: int, r: int) -> Image.Image:
    m = Image.new("L", (w, h), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, w - 1, h - 1), radius=r, fill=255)
    return m


def shadow(mask: Image.Image, blur: int, opacity: float, color=(0, 40, 50)) -> Image.Image:
    a = mask.filter(ImageFilter.GaussianBlur(blur)).point(lambda v: round(v * opacity))
    s = Image.new("RGBA", mask.size, color + (0,))
    s.putalpha(a)
    return s


def gloss(w: int, h: int, r: int, strength: int = 70, part: float = 0.5) -> Image.Image:
    """Tepada oq nur (ilovadagi Gloss)."""
    g = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(g)
    top = int(h * part)
    for y in range(top):
        d.line((0, y, w, y), fill=round(strength * (1 - y / top) ** 1.6))
    out = Image.new("RGBA", (w, h), WHITE + (0,))
    out.putalpha(Image.composite(g, Image.new("L", (w, h), 0), rounded_mask(w, h, r)))
    return out


def radial_glow(w: int, h: int, cx: float, cy: float, radius: float, alpha: int) -> Image.Image:
    g = Image.new("L", (w, h), 0)
    ImageDraw.Draw(g).ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=alpha)
    g = g.filter(ImageFilter.GaussianBlur(radius * 0.45))
    out = Image.new("RGBA", (w, h), WHITE + (0,))
    out.putalpha(g)
    return out


def tile(size: int, radius_part: float = 0.26, stops=None) -> Image.Image:
    """Gradient + yaltiroq yumaloq plitka (logo va ikonkalar asosi)."""
    stops = stops or [(43, 208, 232), (0, 144, 173)]
    r = int(size * radius_part)
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    img.paste(gradient(size, size, stops), (0, 0), rounded_mask(size, size, r))
    img.alpha_composite(gloss(size, size, r, 80, 0.55))
    edge = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(edge).rounded_rectangle((1, 1, size - 2, size - 2), radius=r, outline=WHITE + (70,), width=max(2, size // 60))
    img.alpha_composite(edge)
    return img


def centered(d: ImageDraw.ImageDraw, box, text, f, fill):
    l, t, r, b = d.textbbox((0, 0), text, font=f)
    x0, y0, x1, y1 = box
    d.text((x0 + (x1 - x0 - (r - l)) / 2 - l, y0 + (y1 - y0 - (b - t)) / 2 - t), text, font=f, fill=fill)


def logo(size: int) -> Image.Image:
    img = tile(size, 0.26)
    d = Blend(img)
    f = font(size * 0.62, "bold", display=True)
    l, t, r, b = d.textbbox((0, 0), "Ö", font=f)
    x, y = (size - (r - l)) / 2 - l, (size - (b - t)) / 2 - t
    d.text((x, y + size * 0.02), "Ö", font=f, fill=(0, 70, 90, 80))
    d.text((x, y), "Ö", font=f, fill=WHITE)
    return img


def runs(d: ImageDraw.ImageDraw, xy, parts, f, base, hi):
    """Matnni bo'laklab chizadi: parts = [(matn, ajratilganmi)]."""
    x, y = xy
    for text, mark in parts:
        d.text((x, y), text, font=f, fill=hi if mark else base)
        x += d.textlength(text, font=f)
    return x


def diff_parts(old: str, new: str):
    """new ichida o'zgargan harflarni belgilaydi (oddiy: yangi alifbo harflari va ʼ)."""
    special = set("ÖöĞğŞşÇçʼ")
    out, cur, cur_mark = [], "", None
    for ch in new:
        m = ch in special
        if cur_mark is None or m == cur_mark:
            cur += ch
        else:
            out.append((cur, cur_mark))
            cur = ch
        cur_mark = m
    if cur:
        out.append((cur, bool(cur_mark)))
    return out


# ---------------------------------------------------------------- banner

def banner() -> None:
    W, H, pad = 1600, 660, 28
    cw, ch, r = W - 2 * pad, H - 2 * pad - 16, 52
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    m = rounded_mask(cw, ch, r)
    sh = Image.new("L", (W, H), 0)
    sh.paste(m, (pad, pad + 14))
    img.alpha_composite(shadow(sh, 18, 0.35, (0, 110, 135)))

    card = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    card.paste(gradient(cw, ch, HERO), (0, 0))
    card.alpha_composite(radial_glow(cw, ch, cw * 0.12, ch * 0.0, 520, 95))
    wm = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    ImageDraw.Draw(wm).text((cw - 560, ch - 820), "Ö", font=font(1000, "bold", True), fill=WHITE + (12,))
    card.alpha_composite(wm)
    card.putalpha(m)
    img.alpha_composite(card, (pad, pad))

    d = Blend(img)
    x0, y0 = pad + 88, pad + 92
    lg = logo(132)
    lsh = Image.new("L", (W, H), 0)
    lsh.paste(lg.getchannel("A"), (x0, y0 + 8))
    img.alpha_composite(shadow(lsh, 10, 0.35, (0, 60, 75)))
    img.alpha_composite(lg, (x0, y0))
    d.text((x0 + 164, y0 - 6), "Özbeça", font=font(108, "semibold", True), fill=WHITE)
    d.text((x0 + 170, y0 + 134), "Yangi alifbo  ·  Windows 10 / 11", font=font(30), fill=WHITE + (215,))

    tag = font(40, "semibold", True)
    d.text((x0, y0 + 214), "O'zbekcha matn — yangi alifboda.", font=tag, fill=WHITE)
    d.text((x0, y0 + 268), "Yozayotganingizda, o'zi.", font=tag, fill=WHITE + (225,))

    cx = x0
    for letter in "ÖĞŞÇ":
        box = (cx, y0 + 352, cx + 84, y0 + 436)
        glass = Image.new("RGBA", (84, 84), (0, 0, 0, 0))
        gd = Blend(glass)
        gd.rounded_rectangle((0, 0, 83, 83), radius=22, fill=WHITE + (40,), outline=WHITE + (110,), width=2)
        img.alpha_composite(glass, box[:2])
        centered(d, box, letter, font(46, "bold", True), WHITE)
        cx += 100

    # O'ngda: oyna-shisha kartochka — oldin → keyin
    gx, gy, gw, gh = pad + 900, pad + 96, 600, 420
    glass = Image.new("RGBA", (gw, gh), (0, 0, 0, 0))
    gd = Blend(glass)
    gd.rounded_rectangle((0, 0, gw - 1, gh - 1), radius=34, fill=WHITE + (34,), outline=WHITE + (95,), width=2)
    img.alpha_composite(glass, (gx, gy))
    rows = [("O'zbekiston", "Özbekiston"), ("choy ichdik", "çoy içdik"), ("shahar bog'i", "şahar boği")]
    fo, fn, fa = font(30), font(44, "semibold", True), icon_font(22)
    y = gy + 42
    for i, (old, new) in enumerate(rows):
        d.text((gx + 44, y), old, font=fo, fill=WHITE + (185,))
        d.text((gx + 44, y + 50), "\uE74B", font=fa, fill=WHITE + (170,))
        runs(d, (gx + 84, y + 34), diff_parts(old, new), fn, WHITE, ACCENT_LIGHT)
        y += 126
        if i < len(rows) - 1:
            d.line((gx + 44, y - 18, gx + gw - 44, y - 18), fill=WHITE + (50,), width=2)
    img.save(DOCS / "banner.png", optimize=True)


# ---------------------------------------------------------------- yuklab olish tugmasi

def download_button() -> None:
    W, H, pad = 760, 196, 26
    bw, bh, r = W - 2 * pad, H - 2 * pad - 14, 40
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    m = rounded_mask(bw, bh, r)
    sh = Image.new("L", (W, H), 0)
    sh.paste(m, (pad, pad + 12))
    img.alpha_composite(shadow(sh, 14, 0.55, (0, 140, 170)))
    btn = Image.new("RGBA", (bw, bh), (0, 0, 0, 0))
    btn.paste(gradient(bw, bh, [(43, 208, 232), (0, 153, 181), (0, 120, 145)]), (0, 0))
    btn.alpha_composite(gloss(bw, bh, r, 85, 0.55))
    btn.putalpha(m)
    Blend(btn).rounded_rectangle((1, 1, bw - 2, bh - 2), radius=r, outline=WHITE + (80,), width=2)
    img.alpha_composite(btn, (pad, pad))
    d = Blend(img)
    cx, cy = pad + 82, pad + bh / 2
    d.ellipse((cx - 46, cy - 46, cx + 46, cy + 46), fill=WHITE + (48,))
    centered(d, (cx - 46, cy - 46, cx + 46, cy + 46), "\uE896", icon_font(44), WHITE)
    d.text((pad + 158, pad + 28), "Yuklab olish", font=font(50, "bold", True), fill=WHITE)
    d.text((pad + 160, pad + 92), "Ozbeca.exe  ·  Windows 10 / 11  ·  0.3 MB", font=font(26), fill=WHITE + (230,))
    img.save(DOCS / "download.png", optimize=True)


# ---------------------------------------------------------------- alifbo kartochkalari

def alphabet() -> None:
    letters = [("Sh  sh", "Ş ş", "shahar", "şahar"), ("Ch  ch", "Ç ç", "choy", "çoy"),
               ("Oʻ  oʻ", "Ö ö", "oʻzbek", "özbek"), ("Gʻ  gʻ", "Ğ ğ", "gʻisht", "ğişt")]
    cw, ch, gap, pad = 372, 380, 28, 18
    W, H = 4 * cw + 3 * gap + 2 * pad, ch + 2 * pad + 14
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for i, (old, new, ex_old, ex_new) in enumerate(letters):
        x = pad + i * (cw + gap)
        m = rounded_mask(cw, ch, 40)
        sh = Image.new("L", (W, H), 0)
        sh.paste(m, (x, pad + 10))
        img.alpha_composite(shadow(sh, 12, 0.3, (0, 110, 135)))
        card = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        card.paste(gradient(cw, ch, HERO), (0, 0))
        card.alpha_composite(radial_glow(cw, ch, cw * 0.1, 0, 220, 90))
        card.alpha_composite(gloss(cw, ch, 40, 45, 0.4))
        card.putalpha(m)
        img.alpha_composite(card, (x, pad))
        d = Blend(img)
        centered(d, (x, pad + 30, x + cw, pad + 80), "edi:  " + old, font(30), WHITE + (200,))
        centered(d, (x, pad + 90, x + cw, pad + 250), new, font(124, "bold", True), WHITE)
        pill = (x + 36, pad + ch - 98, x + cw - 36, pad + ch - 38)
        d.rounded_rectangle(pill, radius=30, fill=WHITE + (38,), outline=WHITE + (90,), width=2)
        f = font(30, "semibold")
        text_w = d.textlength(ex_old + "  →  " + ex_new, font=f)
        tx = x + (cw - text_w) / 2
        tx = runs(d, (tx, pill[1] + 11), [(ex_old + "  →  ", False)], f, WHITE + (200,), WHITE)
        runs(d, (tx, pill[1] + 11), diff_parts(ex_old, ex_new), f, WHITE, ACCENT_LIGHT)
    img.save(DOCS / "alphabet.png", optimize=True)


# ---------------------------------------------------------------- ikonkalar

ICONS = {
    "type": "\uE765", "hotkey": "\uE8B3", "telegram": "\uE8BD", "code": "\uE943", "privacy": "\uE72E",
    "light": "\uE945", "dictionary": "\uE82D", "apps": "\uE71D", "clipboard": "\uE77F",
}


def icons() -> None:
    (DOCS / "icons").mkdir(exist_ok=True)
    for name, glyph in ICONS.items():
        s, pad = 112, 8
        img = Image.new("RGBA", (s + 2 * pad, s + 2 * pad + 6), (0, 0, 0, 0))
        sh = Image.new("L", img.size, 0)
        sh.paste(rounded_mask(s, s, int(s * 0.28)), (pad, pad + 5))
        img.alpha_composite(shadow(sh, 5, 0.35, (0, 120, 145)))
        img.alpha_composite(tile(s, 0.28), (pad, pad))
        centered(Blend(img), (pad, pad, pad + s, pad + s), glyph, icon_font(s * 0.46), WHITE)
        img.save(DOCS / "icons" / f"{name}.png", optimize=True)


# ---------------------------------------------------------------- oyna ramkasi

def framed(name: str, title: str) -> None:
    raw = Image.open(DOCS / "raw" / f"{name}.png").convert("RGBA")
    # raw — 2x (192 DPI); fon rangi chetidagi pikseldan
    bg = raw.getpixel((2, 2))
    tb, r, pad = 72, 26, 56
    w, h = raw.width, raw.height + tb
    win = Image.new("RGBA", (w, h), bg)
    win.paste(raw, (0, tb))
    d = Blend(win)
    ico = logo(36)
    win.alpha_composite(ico, (30, (tb - 36) // 2 + 4))
    d.text((80, tb / 2 - 15), title, font=font(25), fill=(40, 40, 40))
    fi = icon_font(20)
    for i, g in enumerate(["\uE8BB", "\uE922", "\uE921"]):
        cx = w - 50 - i * 92
        centered(d, (cx - 24, 4, cx + 24, tb), g, fi, (40, 40, 40) if i != 1 else (190, 190, 190))
    m = rounded_mask(w, h, r)
    win.putalpha(m)
    out = Image.new("RGBA", (w + 2 * pad, h + 2 * pad + 20), (0, 0, 0, 0))
    sh = Image.new("L", out.size, 0)
    sh.paste(m, (pad, pad + 18))
    out.alpha_composite(shadow(sh, 26, 0.32, (0, 30, 40)))
    out.alpha_composite(win, (pad, pad))
    Blend(out).rounded_rectangle((pad, pad, pad + w - 1, pad + h - 1), radius=r, outline=(0, 0, 0, 40), width=2)
    out.save(DOCS / f"{name}.png", optimize=True)


# ---------------------------------------------------------------- jonli namunalar (GIF)

GIF_W, GIF_H, SS = 720, 440, 2  # SS — supersampling


def backdrop() -> Image.Image:
    """GIF foni: tor diapazonli firuza gradient (GIF palitrasida chiziq-chiziq bo'lmasin)."""
    b = gradient(GIF_W * SS, GIF_H * SS, [(18, 186, 210), (0, 148, 176)]).convert("RGBA")
    b.alpha_composite(radial_glow(GIF_W * SS, GIF_H * SS, 0, 0, 420 * SS, 60))
    return b


def app_card(base: Image.Image, box, radius=22):
    x0, y0, x1, y1 = [v * SS for v in box]
    w, h = x1 - x0, y1 - y0
    m = rounded_mask(w, h, radius * SS)
    sh = Image.new("L", base.size, 0)
    sh.paste(m, (x0, y0 + 10 * SS))
    base.alpha_composite(shadow(sh, 14 * SS, 0.35, (0, 60, 75)))
    card = Image.new("RGBA", (w, h), WHITE + (255,))
    card.putalpha(m)
    base.alpha_composite(card, (x0, y0))


def wrap(words, f, max_w, d):
    lines, cur = [], []
    for w in words:
        test = " ".join(cur + [w])
        if cur and d.textlength(test, font=f) > max_w:
            lines.append(cur)
            cur = [w]
        else:
            cur.append(w)
    lines.append(cur)
    return lines


def to_gif(frames, durations, path):
    small = [f.resize((GIF_W, GIF_H), Image.LANCZOS).convert("RGB") for f in frames]
    # Umumiy palitra: hamma kadrlardagi ranglar bitta palitrada — kadrlar orasida "miltillash" bo'lmasin.
    strip = Image.new("RGB", (GIF_W, GIF_H * 3))
    for i, k in enumerate([0, len(small) // 2, len(small) - 1]):
        strip.paste(small[k], (0, GIF_H * i))
    pal = strip.quantize(colors=255, method=Image.Quantize.MEDIANCUT)
    q = [f.quantize(palette=pal, dither=Image.Dither.NONE) for f in small]
    q[0].save(path, save_all=True, append_images=q[1:], duration=durations, loop=0, optimize=True, disposal=1)


def demo_type() -> None:
    """Chat oynasi: so'zlar yozilyapti, probelda o'giriladi, Enter — xabar yuboriladi."""
    words = [("Salom!", "Salom!"), ("Ozbek", "Özbek"), ("choyini", "çoyini"), ("ichamiz,", "içamiz,"),
             ("shahar", "şahar"), ("juda", "juda"), ("go'zal", "gözal")]
    bg = backdrop()
    card_box = (40, 34, GIF_W - 40, GIF_H - 34)
    f_msg, f_in, f_name, f_small = font(23 * SS), font(24 * SS), font(22 * SS, "semibold"), font(17 * SS)

    def frame(done, typing, flash=None, sent=False, cursor=True):
        img = bg.copy()
        app_card(img, card_box)
        d = Blend(img)
        x0, y0, x1, y1 = [v * SS for v in card_box]
        # sarlavha
        d.ellipse((x0 + 22 * SS, y0 + 18 * SS, x0 + 66 * SS, y0 + 62 * SS), fill=(110, 140, 255))
        centered(d, (x0 + 22 * SS, y0 + 18 * SS, x0 + 66 * SS, y0 + 62 * SS), "A", font(22 * SS, "semibold"), WHITE)
        d.text((x0 + 80 * SS, y0 + 16 * SS), "Aziz", font=f_name, fill=INK)
        d.text((x0 + 80 * SS, y0 + 44 * SS), "online", font=f_small, fill=ACCENT)
        d.line((x0, y0 + 80 * SS, x1, y0 + 80 * SS), fill=(234, 236, 239), width=2 * SS)
        # kelgan xabar
        bx, by = x0 + 24 * SS, y0 + 100 * SS
        msg = "Qachon kelasizlar?"
        mw = d.textlength(msg, font=f_msg)
        d.rounded_rectangle((bx, by, bx + mw + 36 * SS, by + 50 * SS), radius=18 * SS, fill=(241, 243, 245))
        d.text((bx + 18 * SS, by + 10 * SS), msg, font=f_msg, fill=INK)
        # yuborilgan xabar
        if sent:
            text = " ".join(n for _, n in words)
            lines = wrap(text.split(), f_msg, 540 * SS, d)
            lh = 32 * SS
            bh = len(lines) * lh + 22 * SS
            wmax = max(d.textlength(" ".join(l), font=f_msg) for l in lines)
            rx1 = x1 - 24 * SS
            rx0 = rx1 - wmax - 36 * SS
            ry0 = by + 66 * SS
            d.rounded_rectangle((rx0, ry0, rx1, ry0 + bh), radius=18 * SS, fill=ACCENT)
            for i, l in enumerate(lines):
                d.text((rx0 + 18 * SS, ry0 + 10 * SS + i * lh), " ".join(l), font=f_msg, fill=WHITE)
            d.text((rx1 - 96 * SS, ry0 + bh + 6 * SS), "12:04", font=f_small, fill=(150, 150, 150))
            d.text((rx1 - 40 * SS, ry0 + bh + 9 * SS), "\uE73E", font=icon_font(15 * SS), fill=ACCENT)
        # kiritish maydoni
        iy0, iy1 = y1 - 110 * SS, y1 - 22 * SS
        d.rounded_rectangle((x0 + 22 * SS, iy0, x1 - 92 * SS, iy1), radius=22 * SS, fill=(244, 246, 248), outline=(226, 229, 233), width=2 * SS)
        sx = x1 - 76 * SS
        d.ellipse((sx, iy0 + 18 * SS, sx + 54 * SS, iy0 + 72 * SS), fill=ACCENT)
        centered(d, (sx, iy0 + 18 * SS, sx + 54 * SS, iy0 + 72 * SS), "\uE724", icon_font(22 * SS), WHITE)
        tokens = [(n, i == flash) for i, (_, n) in enumerate(words[:done])]
        if typing:
            tokens.append((typing, False))
        if not tokens and not sent:
            d.text((x0 + 46 * SS, iy0 + 26 * SS), "Xabar yozing…", font=f_in, fill=(160, 160, 160))
        lines = wrap([t for t, _ in tokens], f_in, (x1 - x0 - 150 * SS), d) if tokens else [[]]
        k = 0
        lx, ly = x0 + 46 * SS, iy0 + (12 if len(lines) > 1 else 26) * SS
        for li, line in enumerate(lines):
            cx = lx
            for t in line:
                text, hl = tokens[k]
                k += 1
                if hl:
                    old = words[[n for _, n in words].index(text)][0]
                    tw = d.textlength(text, font=f_in)
                    d.rounded_rectangle((cx - 4 * SS, ly + 2 * SS, cx + tw + 4 * SS, ly + 34 * SS), radius=8 * SS, fill=(214, 244, 249))
                    runs(d, (cx, ly), diff_parts(old, text), f_in, INK, ACCENT)
                else:
                    d.text((cx, ly), text, font=f_in, fill=INK)
                cx += d.textlength(text + " ", font=f_in)
            if li == len(lines) - 1 and cursor and tokens:
                cxx = cx - (0 if not typing else d.textlength(" ", font=f_in))
                d.rectangle((cxx, ly + 4 * SS, cxx + 2 * SS, ly + 32 * SS), fill=ACCENT)
            ly += 36 * SS
        return img

    frames, dur = [], []

    def add(img, ms):
        frames.append(img)
        dur.append(ms)

    add(frame(0, "", cursor=False), 900)
    for i, (old, new) in enumerate(words):
        for c in range(1, len(old) + 1):
            add(frame(i, old[:c]), 70)
        if i == len(words) - 1:
            add(frame(i, old), 450)  # oxirgi so'z — Enter
        if old != new:
            add(frame(i + 1, "", flash=i), 520)
        else:
            add(frame(i + 1, ""), 90)
    add(frame(len(words), "", cursor=False), 500)
    add(frame(0, "", sent=True, cursor=False), 2600)
    to_gif(frames, dur, DOCS / "demo-type.gif")


def demo_hotkey() -> None:
    """Hujjat: matn belgilanadi → Ctrl+Alt+O → yangi alifboda, «Özbeça ✓»."""
    old = ["O'zbekiston — go'zal yurt.", "Bog'larda choy ichib, shaharni", "kezdik. Hammaga rahmat!"]
    new = ["Özbekiston — gözal yurt.", "Boğlarda çoy içib, şaharni", "kezdik. Hammaga rahmat!"]
    bg = backdrop()
    card_box = (40, 34, GIF_W - 40, GIF_H - 34)
    f_t, f_title = font(27 * SS), font(18 * SS)
    lh = 46 * SS

    def frame(lines, sel=0.0, keys=0, changed=False, toast=0.0):
        img = bg.copy()
        app_card(img, card_box)
        d = Blend(img)
        x0, y0, x1, y1 = [v * SS for v in card_box]
        d.rounded_rectangle((x0, y0, x1, y0 + 50 * SS), radius=22 * SS, fill=(246, 247, 249))
        d.rectangle((x0, y0 + 30 * SS, x1, y0 + 50 * SS), fill=(246, 247, 249))
        d.line((x0, y0 + 50 * SS, x1, y0 + 50 * SS), fill=(232, 234, 237), width=2 * SS)
        d.text((x0 + 24 * SS, y0 + 13 * SS), "maktub.txt", font=f_title, fill=MUTED)
        tx, ty = x0 + 40 * SS, y0 + 82 * SS
        total = sum(len(l) for l in lines)
        left = sel * total
        for i, line in enumerate(lines):
            y = ty + i * lh
            take = max(0, min(len(line), round(left)))
            left -= len(line)
            if take:
                wsel = d.textlength(line[:take], font=f_t)
                d.rectangle((tx - 3 * SS, y - 2 * SS, tx + wsel + 3 * SS, y + 38 * SS), fill=(190, 228, 238))
            if changed:
                runs(d, (tx, y), diff_parts(old[i], line), f_t, INK, ACCENT)
            else:
                d.text((tx, y), line, font=f_t, fill=INK)
        # tugmalar
        if keys:
            names = ["Ctrl", "Alt", "O"]
            fk = font(24 * SS, "semibold")
            kx = (x0 + x1) / 2 - 172 * SS
            ky = y1 - 92 * SS
            for k, n in enumerate(names):
                w = max(78 * SS, d.textlength(n, font=fk) + 40 * SS)
                down = k < keys
                off = 4 * SS if down else 0
                d.rounded_rectangle((kx, ky + 6 * SS, kx + w, ky + 58 * SS), radius=12 * SS, fill=(200, 205, 210))
                d.rounded_rectangle((kx, ky + off, kx + w, ky + 52 * SS + off), radius=12 * SS,
                                    fill=(214, 244, 249) if down else WHITE, outline=ACCENT if down else (200, 205, 210), width=2 * SS)
                centered(d, (kx, ky + off, kx + w, ky + 52 * SS + off), n, fk, ACCENT if down else INK)
                kx += w + 30 * SS
                if k < 2:
                    centered(d, (kx - 30 * SS, ky, kx, ky + 52 * SS), "+", fk, MUTED)
        # «Özbeça ✓» xabari
        if toast > 0:
            fo = font(22 * SS, "semibold")
            text = "Özbeça"
            tw = d.textlength(text, font=fo)
            w, h = tw + 112 * SS, 52 * SS
            px = x0 + 40 * SS + d.textlength(lines[2], font=f_t) + 24 * SS
            py = ty + 2 * lh - 6 * SS + (1 - toast) * 10 * SS
            pill = Image.new("RGBA", (int(w), int(h)), (0, 0, 0, 0))
            pd = Blend(pill)
            pd.rounded_rectangle((0, 0, w - 1, h - 1), radius=h / 2, fill=(32, 36, 40, round(240 * toast)))
            lg = logo(int(34 * SS))
            lg.putalpha(lg.getchannel("A").point(lambda v: round(v * toast)))
            pill.alpha_composite(lg, (int(10 * SS), int(9 * SS)))
            pd.text((54 * SS, 11 * SS), text, font=fo, fill=WHITE + (round(255 * toast),))
            pd.text((60 * SS + tw, 17 * SS), "\uE73E", font=icon_font(18 * SS), fill=(110, 231, 160, round(255 * toast)))
            img.alpha_composite(pill, (int(px), int(py)))
        return img

    frames, dur = [], []

    def add(img, ms):
        frames.append(img)
        dur.append(ms)

    add(frame(old), 1100)
    for s in range(1, 9):
        add(frame(old, sel=s / 8), 60)
    add(frame(old, sel=1), 500)
    for k in range(1, 4):
        add(frame(old, sel=1, keys=k), 230)
    add(frame(old, sel=1, keys=3), 250)
    add(frame(new, sel=1, changed=True), 350)
    for t in (0.35, 0.7, 1.0):
        add(frame(new, sel=1, changed=True, toast=t), 60)
    add(frame(new, sel=1, changed=True, toast=1.0), 1400)
    add(frame(new, changed=True), 2200)
    to_gif(frames, dur, DOCS / "demo-hotkey.gif")


def app_icon() -> None:
    """docs/icon.png — ilova ikonkasi (tools/make_icon.py yasagan .ico dan)."""
    ico = Image.open(ROOT / "src" / "Ozbeca.App" / "Assets" / "ozbeca.ico")
    ico.size = (256, 256)
    ico.load()
    ico.convert("RGBA").resize((128, 128), Image.LANCZOS).save(DOCS / "icon.png", optimize=True)


def main() -> None:
    app_icon()
    banner()
    download_button()
    alphabet()
    icons()
    framed("settings", "Özbeça")
    framed("welcome", "Özbeça — o'rnatish")
    framed("apps", "Özbeça — dasturlar")
    demo_type()
    demo_hotkey()
    print("OK: docs/ rasmlari yangilandi")


if __name__ == "__main__":
    main()
