"""Builds store icons from store/icons/icon_layer.png (rendered by
tools/store/icon_render.tscn) and captions store screenshots.
Needs Pillow:  python tools/store/make_store_art.py"""
import math
import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ICONS = os.path.join(ROOT, "store", "icons")
FONT = os.path.join(ROOT, "assets", "fonts", "Baloo2-ExtraBold.ttf")


def background(size):
    bg = Image.new("RGB", (size, size))
    px = bg.load()
    c0, c1 = (92, 62, 170), (24, 26, 52)
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - size * 0.5, y - size * 0.42) / (size * 0.72)
            t = min(1.0, d)
            px[x, y] = tuple(int(c0[i] * (1 - t) + c1[i] * t) for i in range(3))
    # soft sun rays
    rays = Image.new("L", (size, size), 0)
    dr = ImageDraw.Draw(rays)
    cx, cy = size * 0.5, size * 0.42
    for k in range(16):
        a0 = k * math.tau / 16
        a1 = a0 + math.tau / 40
        pts = [(cx, cy), (cx + math.cos(a0) * size, cy + math.sin(a0) * size),
               (cx + math.cos(a1) * size, cy + math.sin(a1) * size)]
        dr.polygon(pts, fill=28)
    rays = rays.filter(ImageFilter.GaussianBlur(size / 60))
    bg = Image.composite(Image.new("RGB", (size, size), (255, 214, 120)), bg, rays)
    return bg


def coin(draw, cx, cy, r):
    draw.ellipse([cx - r, cy - r * 0.95 + r * 0.12, cx + r, cy + r * 1.05 + r * 0.12], fill=(184, 122, 10))
    draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(255, 210, 63))
    draw.ellipse([cx - r * 0.76, cy - r * 0.76, cx + r * 0.76, cy + r * 0.76], fill=(255, 224, 122))
    f = ImageFont.truetype(FONT, int(r * 1.3))
    draw.text((cx, cy + r * 0.06), "$", font=f, fill=(184, 122, 10), anchor="mm")


def main():
    layer = Image.open(os.path.join(ICONS, "icon_layer.png")).convert("RGBA")
    size = 1024
    bg = background(size).convert("RGBA")
    art = layer.resize((int(size * 0.96), int(size * 0.96)), Image.LANCZOS)
    bg.alpha_composite(art, ((size - art.width) // 2, int(size * 0.08)))
    d = ImageDraw.Draw(bg)
    coin(d, int(size * 0.78), int(size * 0.2), int(size * 0.11))
    coin(d, int(size * 0.88), int(size * 0.33), int(size * 0.07))
    full = bg.convert("RGB")
    full.save(os.path.join(ICONS, "icon_1024.png"))
    for s in (512, 192, 180, 167, 152, 120, 76):
        full.resize((s, s), Image.LANCZOS).save(os.path.join(ICONS, "icon_%d.png" % s))
    # Android adaptive icon: background + foreground (art inside the 66% safe zone)
    a = 432
    background(a).save(os.path.join(ICONS, "adaptive_background_432.png"))
    fg = Image.new("RGBA", (a, a), (0, 0, 0, 0))
    small = layer.resize((int(a * 0.72), int(a * 0.72)), Image.LANCZOS)
    fg.alpha_composite(small, ((a - small.width) // 2, int(a * 0.16)))
    fg.save(os.path.join(ICONS, "adaptive_foreground_432.png"))
    print("icons written to", ICONS)


if __name__ == "__main__":
    main()


CAPTIONS = {
    "en": {
        "1_mentor": "Meet Dot, your mentor",
        "2_empire": "Grow a tiny garage into an empire",
        "3_contracts": "Pick the jobs that pay",
        "4_dispatch": "Fast, normal or safe? Your call",
        "5_event": "Handle trouble on the road",
        "6_map": "Expand across 8 fictional regions",
        "feature": "Build a shadow logistics empire",
    },
    "tr": {
        "1_mentor": "Akıl hocan Dot ile tanış",
        "2_empire": "Küçük garajı imparatorluğa dönüştür",
        "3_contracts": "Kazandıran işleri seç",
        "4_dispatch": "Hızlı, normal, güvenli? Karar senin",
        "5_event": "Yoldaki sorunlarla başa çık",
        "6_map": "8 kurgusal bölgeye yayıl",
    },
}


def compose_screens(raw_dir):
    """Phone screenshots (1080x1920) with a caption band, saved as JPEG."""
    out_root = os.path.join(ROOT, "store", "screenshots")
    for lang, caps in CAPTIONS.items():
        os.makedirs(os.path.join(out_root, lang), exist_ok=True)
        for key, text in caps.items():
            src = os.path.join(raw_dir, "%s_%s.png" % (key, lang))
            if key == "feature" or not os.path.exists(src):
                continue
            W, H = 1080, 1920
            canvas = Image.new("RGB", (W, H))
            g = background(W).resize((W, H))
            canvas.paste(g)
            shot = Image.open(src).convert("RGB")
            band = 330
            scale = (H - band - 40) / shot.height
            sw, sh = int(shot.width * scale), int(shot.height * scale)
            shot = shot.resize((sw, sh), Image.LANCZOS)
            mask = Image.new("L", (sw, sh), 0)
            ImageDraw.Draw(mask).rounded_rectangle([0, 0, sw, sh], radius=46, fill=255)
            shadow = Image.new("L", (W, H), 0)
            ImageDraw.Draw(shadow).rounded_rectangle([(W - sw) // 2 + 6, band + 18, (W + sw) // 2 + 6, band + sh + 18],
                                                     radius=46, fill=150)
            shadow = shadow.filter(ImageFilter.GaussianBlur(18))
            canvas = Image.composite(Image.new("RGB", (W, H), (10, 10, 25)), canvas, shadow)
            canvas.paste(shot, ((W - sw) // 2, band), mask)
            d = ImageDraw.Draw(canvas)
            size = 78
            font = ImageFont.truetype(FONT, size)
            while d.textlength(text, font=font) > W - 100 and size > 40:
                size -= 4
                font = ImageFont.truetype(FONT, size)
            d.text((W // 2 + 4, band // 2 + 8), text, font=font, fill=(20, 12, 40), anchor="mm")
            d.text((W // 2, band // 2 + 2), text, font=font, fill=(255, 214, 80), anchor="mm")
            canvas.save(os.path.join(out_root, lang, key + ".jpg"), quality=88)
    feat = os.path.join(raw_dir, "feature_en.png")
    if os.path.exists(feat):
        img = Image.open(feat).convert("RGB").resize((1024, 500))
        shade = Image.new("L", (1024, 500), 0)
        ImageDraw.Draw(shade).rectangle([0, 0, 1024, 500], fill=0)
        for x in range(1024):
            ImageDraw.Draw(shade).line([(x, 0), (x, 500)], fill=int(max(0, 170 - x * 0.32)))
        img = Image.composite(Image.new("RGB", (1024, 500), (16, 14, 40)), img, shade)
        d = ImageDraw.Draw(img)
        f1 = ImageFont.truetype(FONT, 82)
        f2 = ImageFont.truetype(FONT, 34)
        d.text((52, 190), "BORDERLINE", font=f1, fill=(255, 210, 63), anchor="lm", stroke_width=6, stroke_fill=(16, 18, 28))
        d.text((52, 278), "EMPIRE", font=f1, fill=(244, 241, 234), anchor="lm", stroke_width=6, stroke_fill=(16, 18, 28))
        d.text((56, 350), CAPTIONS["en"]["feature"], font=f2, fill=(60, 242, 255), anchor="lm", stroke_width=4,
               stroke_fill=(16, 18, 28))
        img.save(os.path.join(ROOT, "store", "screenshots", "feature_graphic_1024x500.jpg"), quality=90)
    print("store screenshots written")


if __name__ == "__main__" and len(os.sys.argv) > 1:
    compose_screens(os.sys.argv[1])
