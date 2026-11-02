"""Post-process the Blender renders (icons.py) into the candidate set + contact sheet.

    python3 post.py <dir>      (reads <dir>/raw/cN_raw.png, writes <dir>/cN.png, cN_96.png, cN_48.png, sheet.png)

Glow = bright-pass bloom (Cycles has no bloom); 512 = Lanczos from the 1024 render, 32-bit RGBA, opaque, full bleed.
"""
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

D = Path(sys.argv[1])
RAW = D / "raw"
IDS = sorted(p.stem.split("_")[0] for p in RAW.glob("c*_raw.png"))
LABELS = {
    "c1": "Dusk pylon", "c2": "Bolt gauge", "c3": "Grid island",
    "c3b": "Grid island, steam plant (chosen)", "c4": "City bulb", "c5": "Keeper hard hat", "c6": "Storm strike",
}


def bloom(im, threshold=200, strength=0.55):
    lum = im.convert("L").point(lambda v: 255 if v > threshold else int(v * max(0, (v - threshold * 0.75)) / 255))
    bright = Image.composite(im, Image.new("RGB", im.size), lum)
    glow = Image.new("RGB", im.size)
    for r, w in ((6, 0.5), (18, 0.35), (44, 0.25)):
        g = bright.filter(ImageFilter.GaussianBlur(r)).point(lambda v, w=w: int(v * w * strength * 2))
        glow = ImageChops.add(glow, g)
    return ImageChops.screen(im, glow)


def mask_rounded(im, radius_frac=0.2):
    """Play-style adaptive preview: rounded square (Play's listing mask) with soft shadow."""
    s = im.size[0]
    m = Image.new("L", (s * 4, s * 4), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, s * 4 - 1, s * 4 - 1), radius=int(s * 4 * radius_frac), fill=255)
    m = m.resize((s, s), Image.LANCZOS)
    out = im.convert("RGBA")
    out.putalpha(m)
    return out


def safe_circle(im):
    o = im.convert("RGBA").copy()
    d = ImageDraw.Draw(o)
    s = o.size[0]
    r = s * 0.33
    d.ellipse((s / 2 - r, s / 2 - r, s / 2 + r, s / 2 + r), outline=(255, 255, 255, 140), width=max(1, s // 256))
    return o


def font(sz, bold=True):
    for f in ("/System/Library/Fonts/Supplemental/Arial Bold.ttf", "/System/Library/Fonts/Helvetica.ttc"):
        try:
            return ImageFont.truetype(f, sz)
        except Exception:
            pass
    return ImageFont.load_default()


def main():
    finals = {}
    for i in IDS:
        raw = Image.open(RAW / f"{i}_raw.png").convert("RGB")
        big = bloom(raw)
        im = big.resize((512, 512), Image.LANCZOS)
        im.convert("RGBA").save(D / f"{i}.png")
        for s in (96, 48):
            big.resize((s, s), Image.LANCZOS).convert("RGBA").save(D / f"{i}_{s}.png")
        finals[i] = im
    # contact sheet: one row per candidate: 512 | 512 rounded (light) | safe circle 192 | 96 & 48 on light | on dark
    rowh, pad = 560, 24
    W = 512 + 360 + 220 + 300 + 300 + pad * 7
    sheet = Image.new("RGB", (W, rowh * len(IDS) + 90), "#F2F0EC")
    dr = ImageDraw.Draw(sheet)
    dr.text((pad, 24), "Grid Keeper: Energy Tycoon - icon candidates 2026-10-29 (512 | Play mask | safe circle | 96 + 48 light | 96 + 48 dark)",
            fill="#222", font=font(26))
    for r, i in enumerate(IDS):
        y0 = 90 + r * rowh
        im = finals[i]
        x = pad
        dr.text((x, y0), f"{i.upper()}  {LABELS.get(i, '')}", fill="#111", font=font(30))
        y = y0 + 40
        sheet.paste(im, (x, y))
        x += 512 + pad
        m = mask_rounded(im.resize((340, 340), Image.LANCZOS))
        sheet.paste(m, (x, y + 80), m)
        x += 360 + pad
        sc = safe_circle(im.resize((200, 200), Image.LANCZOS))
        sheet.paste(sc, (x, y + 150))
        x += 220 + pad
        for bg, txt in (("#F2F0EC", "#333"), ("#121417", "#ddd")):
            dr.rectangle((x, y, x + 300, y + 512), fill=bg)
            for k, s in enumerate((96, 48)):
                ic = Image.open(D / f"{i}_{s}.png")
                ic = mask_rounded(ic.convert("RGB"), 0.22)
                yy = y + 60 + k * 200
                sheet.paste(ic, (x + 30, yy), ic)
                # also 1:1 unmasked next to it
                raw_s = Image.open(D / f"{i}_{s}.png").convert("RGB")
                sheet.paste(raw_s, (x + 160, yy))
                ImageDraw.Draw(sheet).text((x + 30, yy + s + 8), f"{s}px", fill=txt, font=font(18, False))
            x += 300 + pad
    sheet.save(D / "sheet.png", optimize=True)
    print("SHEET", D / "sheet.png", sheet.size)
    if "c3" in finals and "c3b" in finals:
        compare("c3", "c3b")


def compare(a, b):
    """sheet_<b>.png: <a> vs <b> at 512 / 96 / 48 on light and dark, plus <b>'s adaptive-icon preview when present."""
    W, H = 2 * (512 + 40) + 40, 512 + 360 + 80
    sh = Image.new("RGB", (W, H), "#F2F0EC")
    dr = ImageDraw.Draw(sh)
    for k, i in enumerate((a, b)):
        x = 40 + k * (512 + 40)
        dr.text((x, 16), f"{i.upper()}  {LABELS.get(i, '')}", fill="#111", font=font(28))
        sh.paste(Image.open(D / f"{i}.png").convert("RGB"), (x, 60))
        for r, (bg, txt) in enumerate((("#F2F0EC", "#333"), ("#121417", "#ddd"))):
            y = 60 + 512 + 20 + r * 170
            dr.rectangle((x, y, x + 512, y + 160), fill=bg)
            for j, s in enumerate((96, 48)):
                ic = Image.open(D / f"{i}_{s}.png").convert("RGB")
                m = mask_rounded(ic, 0.22)
                sh.paste(m, (x + 20 + j * 260, y + 20), m)
                sh.paste(ic, (x + 130 + j * 260, y + 20))
                dr.text((x + 20 + j * 260, y + 125), f"{s}px", fill=txt, font=font(18, False))
    sh.save(D / f"sheet_{b}.png", optimize=True)
    print("SHEET", D / f"sheet_{b}.png", sh.size)


main()
