#!/usr/bin/env python3
"""Borderline Empire store deck renderer — style "Cargo Manifest".

Composes every store slide from a REAL full-width game capture (tools/store/tool_store_shots.py) under a caption band:
night-asphalt band, typewritten MANIFEST tag, stencil headline (cream lead + gold punchline), a red rubber stamp,
a typewritten sub-line and a yellow/black barrier stripe where the band meets the game.

  python3 store/deck/render.py --raw /tmp/be-raw --out store/screenshots --langs en,tr [--devices iphone69,...]

Writes <out>/<lang>/<device>/NN.png (devices + derived iphone65 / tab7), <out>/<lang>/feature-graphic.png.
"""
import argparse
import json
import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

from deck_spec import DERIVED, DEVICES, LANGS, ORDER

HERE = Path(__file__).resolve().parent
F = HERE / "fonts"
ASPHALT = (21, 26, 46)
ASPHALT2 = (30, 36, 62)
CREAM = (244, 235, 217)
GOLD = (255, 201, 60)
KRAFT = (217, 185, 140)
STAMP = (238, 84, 88)
INK = (16, 18, 28)


def copy_for(lang):
    if lang == "en":
        return json.loads((HERE / "copy/en.json").read_text())
    return json.loads((HERE / "copy/l10n.json").read_text())[lang]


def fonts(lang):
    cjk = {"ja": "JP", "ko": "KR", "zh": "SC"}.get(lang)
    if cjk:
        heavy = str(F / f"Noto-Sans-{cjk}_wght-900.ttf")
        bold = str(F / f"Noto-Sans-{cjk}_wght-700.ttf")
        return {"head": (heavy, None), "type": (bold, None), "stamp": (heavy, None), "cjk": True}
    if lang == "ru":
        return {"head": (str(F / "RussoOne-Regular.ttf"), None), "type": (str(F / "PTMono-Regular.ttf"), None),
                "stamp": (str(F / "RussoOne-Regular.ttf"), None), "cjk": False}
    st = str(F / "BigShouldersStencilDisplay.ttf")
    return {"head": (st, 900), "type": (str(F / "SpecialElite-Regular.ttf"), None), "stamp": (st, 800), "cjk": False}


def font(spec, size):
    path, wght = spec
    f = ImageFont.truetype(path, int(size))
    if wght:
        f.set_variation_by_axes([wght])
    return f


def fit(spec, text, max_w, size, min_size=10):
    while size > min_size:
        f = font(spec, size)
        if f.getlength(text) <= max_w:
            return f, size
        size *= 0.95
    return font(spec, min_size), min_size


def noise(img, amount=10, seed=1):
    rnd = random.Random(seed)
    w, h = img.size
    n = Image.new("L", (w // 4, h // 4))
    n.putdata([rnd.randint(128 - amount, 128 + amount) for _ in range(n.width * n.height)])
    n = n.resize((w, h), Image.NEAREST).filter(ImageFilter.GaussianBlur(1))
    grain = Image.merge("RGB", (n, n, n))
    return Image.blend(img, Image.composite(img, grain, Image.new("L", img.size, 235)), 0.25)


def hazard(w, h, seed=0):
    im = Image.new("RGB", (w, h), GOLD)
    d = ImageDraw.Draw(im)
    step = h * 2
    for x in range(-h * 2, w + h * 2, step):
        d.polygon([(x, h), (x + h, 0), (x + h + step / 2, 0), (x + step / 2, h)], fill=INK)
    return im


def stamp(text, spec, height, seed):
    """Red rubber stamp: double rule box, uppercase word, worn ink, slight rotation."""
    f, size = fit(spec, text, height * 6, height * 0.62)
    tw = f.getlength(text)
    pad = height * 0.32
    w, h = int(tw + pad * 2), int(height)
    im = Image.new("RGBA", (w + 20, h + 20), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    lw = max(3, int(h * 0.07))
    d.rounded_rectangle((10, 10, 10 + w, 10 + h), radius=h * 0.12, outline=STAMP + (255,), width=lw)
    d.rounded_rectangle((10 + lw * 2, 10 + lw * 2, 10 + w - lw * 2, 10 + h - lw * 2), radius=h * 0.08, outline=STAMP + (255,), width=max(2, lw // 2))
    bb = d.textbbox((0, 0), text, font=f)
    d.text((10 + (w - (bb[2] - bb[0])) / 2 - bb[0], 10 + (h - (bb[3] - bb[1])) / 2 - bb[1]), text, font=f, fill=STAMP + (255,))
    # worn ink: knock out speckles
    rnd = random.Random(seed)
    a = im.split()[3]
    mask = Image.new("L", im.size, 255)
    md = ImageDraw.Draw(mask)
    for _ in range(int(w * h / 900)):
        x, y = rnd.uniform(0, im.width), rnd.uniform(0, im.height)
        r = rnd.uniform(1, h * 0.03)
        md.ellipse((x - r, y - r, x + r, y + r), fill=rnd.randint(0, 120))
    a = Image.composite(a, Image.new("L", im.size, 0), mask)
    im.putalpha(a.point(lambda v: int(v * 0.93)))
    return im.rotate(-6 if seed % 2 else 5, resample=Image.BICUBIC, expand=True)


def band(w, h, c, n, lang, seed):
    fs = fonts(lang)
    im = Image.new("RGB", (w, h), ASPHALT)
    d = ImageDraw.Draw(im)
    # faint manifest grid
    gap = w / 18
    for i in range(1, 19):
        d.line([(i * gap, 0), (i * gap - h * 0.25, h)], fill=ASPHALT2, width=max(1, w // 700))
    im = noise(im, 14, seed)
    d = ImageDraw.Draw(im)
    m = w * 0.065
    stripe = int(h * 0.055)
    usable = h - stripe
    # MANIFEST tag (ticket with punched hole)
    tag_h = usable * 0.115
    tf = font(fs["type"], tag_h * 0.62)
    tag_txt = f"{c['manifest']} Nº 0{n}"
    tw = tf.getlength(tag_txt)
    ty = usable * 0.085
    d.rounded_rectangle((m, ty, m + tw + tag_h * 1.4, ty + tag_h), radius=tag_h * 0.18, fill=KRAFT)
    d.ellipse((m + tag_h * 0.28, ty + tag_h * 0.36, m + tag_h * 0.56, ty + tag_h * 0.64), fill=ASPHALT)
    bb = d.textbbox((0, 0), tag_txt, font=tf)
    d.text((m + tag_h * 0.95 - bb[0], ty + (tag_h - (bb[3] - bb[1])) / 2 - bb[1]), tag_txt, font=tf, fill=INK)
    s = c["slides"][ORDER[n - 1]]
    maxw = w - 2 * m
    # headline: lead (cream) + key (gold, biggest)
    lead_size = usable * (0.15 if not fs["cjk"] else 0.125)
    lf, _ = fit(fs["head"], s["lead"], maxw, lead_size)
    key_size = usable * (0.34 if not fs["cjk"] else 0.26)
    kf, _ = fit(fs["head"], s["key"], maxw, key_size)
    sub_size = usable * 0.078
    sf, _ = fit(fs["type"], s["sub"], maxw, sub_size)
    lb = d.textbbox((0, 0), s["lead"], font=lf)
    kb = d.textbbox((0, 0), s["key"], font=kf)
    sb = d.textbbox((0, 0), s["sub"], font=sf)
    g1, g2 = usable * 0.045, usable * 0.065
    block = (lb[3] - lb[1]) + g1 + (kb[3] - kb[1]) + g2 + (sb[3] - sb[1])
    top = ty + tag_h
    y = top + (usable - top - block) * 0.5
    d.text((m - lb[0], y - lb[1]), s["lead"], font=lf, fill=CREAM)
    y += (lb[3] - lb[1]) + g1
    # gold key with a hard ink drop shadow (stencil sprayed on a crate)
    off = max(3, int(usable * 0.012))
    d.text((m - kb[0] + off, y - kb[1] + off), s["key"], font=kf, fill=INK)
    d.text((m - kb[0], y - kb[1]), s["key"], font=kf, fill=GOLD)
    y += (kb[3] - kb[1]) + g2
    d.text((m - sb[0], y - sb[1]), s["sub"], font=sf, fill=KRAFT)
    # stamp top-right, tilted, clear of the tag
    st = stamp(s["stamp"], fs["stamp"], usable * 0.19, seed)
    if st.width > w * 0.42:
        st = st.resize((int(w * 0.42), int(st.height * w * 0.42 / st.width)), Image.LANCZOS)
    im.paste(st, (int(w - m * 0.6 - st.width), int(usable * 0.015)), st)
    im.paste(hazard(w, stripe), (0, h - stripe))
    return im


def slide(device, lang, n, raw):
    W, H = DEVICES[device]["slide"]
    B = DEVICES[device]["band"]
    c = copy_for(lang)
    game = Image.open(raw / device / lang / f"{ORDER[n - 1]}.png").convert("RGB")
    im = Image.new("RGB", (W, H), ASPHALT)
    im.paste(band(W, B, c, n, lang, n), (0, 0))
    im.paste(game, (0, B))
    # soft shadow of the band onto the game
    sh = Image.new("L", (W, int(B * 0.06)), 0)
    for yy in range(sh.height):
        ImageDraw.Draw(sh).line([(0, yy), (W, yy)], fill=int(110 * (1 - yy / sh.height)))
    im.paste(Image.new("RGB", sh.size, INK), (0, B), sh)
    return im


def feature(lang, raw, out):
    """1024x500: HUD-less hero capture on the right, band on the left with the brand."""
    c = copy_for(lang)
    fs = fonts("en")  # brand stays Latin stencil
    tfs = fonts(lang)
    W, H = 1024, 500
    hero = Image.open(raw / "feature" / "hero.png").convert("RGB")
    im = hero.resize((W, H), Image.LANCZOS)
    panel_w = 560
    mask = Image.new("L", (W, H), 0)
    md = ImageDraw.Draw(mask)
    md.polygon([(0, 0), (panel_w, 0), (panel_w - 90, H), (0, H)], fill=255)
    bandim = noise(Image.new("RGB", (W, H), ASPHALT), 14, 3)
    im.paste(bandim, (0, 0), mask)
    d = ImageDraw.Draw(im)
    d.line([(panel_w, 0), (panel_w - 90, H)], fill=GOLD, width=10)
    m = 44
    lf, _ = fit(tfs["type"], c["feature"]["lead"], 430, 34)
    d.text((m, 70), c["feature"]["lead"], font=lf, fill=KRAFT)
    b1, _ = fit(fs["head"], "BORDERLINE", 440, 128)
    b2, _ = fit(fs["head"], "EMPIRE", 440, 150)
    bb = d.textbbox((0, 0), "BORDERLINE", font=b1)
    y = 125
    d.text((m - bb[0] + 5, y - bb[1] + 5), "BORDERLINE", font=b1, fill=INK)
    d.text((m - bb[0], y - bb[1]), "BORDERLINE", font=b1, fill=CREAM)
    y += bb[3] - bb[1] + 18
    bb = d.textbbox((0, 0), "EMPIRE", font=b2)
    d.text((m - bb[0] + 6, y - bb[1] + 6), "EMPIRE", font=b2, fill=INK)
    d.text((m - bb[0], y - bb[1]), "EMPIRE", font=b2, fill=GOLD)
    y += bb[3] - bb[1] + 28
    sf, _ = fit(tfs["type"], c["feature"]["sub"], 420, 30)
    d.text((m, y), c["feature"]["sub"], font=sf, fill=KRAFT)
    im.paste(hazard(W, 16), (0, H - 16))
    out.parent.mkdir(parents=True, exist_ok=True)
    im.save(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--raw", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--langs", default=",".join(LANGS))
    ap.add_argument("--devices", default=",".join(DEVICES))
    ap.add_argument("--slides", default="")
    a = ap.parse_args()
    raw, out = Path(a.raw), Path(a.out)
    nums = [int(x) for x in a.slides.split(",")] if a.slides else list(range(1, len(ORDER) + 1))
    for lang in a.langs.split(","):
        for dev in a.devices.split(","):
            for n in nums:
                im = slide(dev, lang, n, raw)
                p = out / lang / dev / f"{n:02d}.png"
                p.parent.mkdir(parents=True, exist_ok=True)
                im.save(p)
                for dname, (src, size) in DERIVED.items():
                    if src == dev:
                        q = out / lang / dname / f"{n:02d}.png"
                        q.parent.mkdir(parents=True, exist_ok=True)
                        im.resize(size, Image.LANCZOS).save(q)
            print("ok", lang, dev, flush=True)
        if (raw / "feature" / "hero.png").exists():
            feature(lang, raw, out / lang / "feature-graphic.png")


if __name__ == "__main__":
    main()
