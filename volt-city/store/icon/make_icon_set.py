"""Build the Volt City icon set from the C3b Blender renders (owner choice 2026-10-29, Patron's C3b = C3 with the game's
own steam plant instead of a cooling tower). Sources: ../icon-candidates/icons.py (c3b) + layers.py (fg / bg).

    python3 make_icon_set.py <render_dir>
      <render_dir>/raw/c3b_raw.png               1024 full composite (Cycles)
      <render_dir>/c3b-layers/c3b_fg_raw.png     1024 transparent art only
      <render_dir>/c3b-layers/c3b_bg_raw.png     1024 backdrop only
Writes (paths relative to games/volt-city):
  store/icon/play-512.png                        Play listing icon, 512 x 512 32-bit RGBA, opaque, full bleed
  store/icon/house-256.png                       house-ads icon (ads_app.icon_url), 256 x 256 RGB
  unity/Assets/Art/Icon/icon_full.png            1024, Player Settings default icon
  unity/Assets/Art/Icon/icon_adaptive_fg.png     432, adaptive foreground (art inside the 66 dp safe circle of 108 dp)
  unity/Assets/Art/Icon/icon_adaptive_bg.png     432, adaptive background
  unity/Assets/Art/Icon/icon_round.png           512, round launcher icon (circle, transparent corners)
  unity/Assets/Art/Icon/icon_legacy.png          512, legacy launcher icon (rounded square, transparent corners)
"""
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

GAME = Path(__file__).resolve().parents[2]
SRC = Path(sys.argv[1])
ICON_DIR = GAME / "unity/Assets/Art/Icon"
STORE = GAME / "store/icon"
SAFE = 0.30  # art radius as a fraction of the 108 dp layer (66 dp safe circle = 0.3056)


def glow_of(im, threshold=200, strength=0.55):
    lum = im.convert("L").point(lambda v: 255 if v > threshold else int(v * max(0, (v - threshold * 0.75)) / 255))
    bright = Image.composite(im, Image.new("RGB", im.size), lum)
    glow = Image.new("RGB", im.size)
    for r, w in ((6, 0.5), (18, 0.35), (44, 0.25)):
        glow = ImageChops.add(glow, bright.filter(ImageFilter.GaussianBlur(r)).point(lambda v, w=w: int(v * w * strength * 2)))
    return glow


def bloom(im):
    return ImageChops.screen(im, glow_of(im))


def bloom_rgba(im):
    """Glow for a transparent layer: premultiplied colour + glow, alpha grows where the glow is."""
    a = im.getchannel("A")
    pre = Image.composite(im.convert("RGB"), Image.new("RGB", im.size), a)
    g = glow_of(pre)
    out = ImageChops.screen(pre, g)
    ga = g.convert("L").point(lambda v: min(255, int(v * 1.6)))
    alpha = ImageChops.lighter(a, ga)
    # un-premultiply
    rgb = Image.merge("RGB", [Image.eval(c, lambda v: v) for c in out.split()])
    px, ap = rgb.load(), alpha.load()
    w, h = rgb.size
    for y in range(h):
        for x in range(w):
            al = ap[x, y]
            if 0 < al < 255:
                r, g_, b = px[x, y]
                k = 255.0 / al
                px[x, y] = (min(255, int(r * k)), min(255, int(g_ * k)), min(255, int(b * k)))
    res = rgb.convert("RGBA")
    res.putalpha(alpha)
    return res


def masked(im, shape, radius_frac=0.18):
    s = im.size[0]
    m = Image.new("L", (s * 4, s * 4), 0)
    d = ImageDraw.Draw(m)
    if shape == "circle":
        d.ellipse((0, 0, s * 4 - 1, s * 4 - 1), fill=255)
    else:
        d.rounded_rectangle((0, 0, s * 4 - 1, s * 4 - 1), radius=int(s * 4 * radius_frac), fill=255)
    out = im.convert("RGBA")
    out.putalpha(m.resize((s, s), Image.LANCZOS))
    return out


def main():
    ICON_DIR.mkdir(parents=True, exist_ok=True)
    STORE.mkdir(parents=True, exist_ok=True)
    full = bloom(Image.open(SRC / "raw/c3b_raw.png").convert("RGB"))
    full.convert("RGBA").save(ICON_DIR / "icon_full.png", optimize=True)
    full.resize((512, 512), Image.LANCZOS).convert("RGBA").save(STORE / "play-512.png", optimize=True)
    full.resize((256, 256), Image.LANCZOS).save(STORE / "house-256.png", optimize=True)
    masked(full.resize((512, 512), Image.LANCZOS), "circle").save(ICON_DIR / "icon_round.png", optimize=True)
    masked(full.resize((512, 512), Image.LANCZOS), "rounded").save(ICON_DIR / "icon_legacy.png", optimize=True)

    # adaptive: foreground scaled so every opaque pixel sits inside the safe circle, centred on the art
    fg = bloom_rgba(Image.open(SRC / "c3b-layers/c3b_fg_raw.png").convert("RGBA").resize((512, 512), Image.LANCZOS))
    a = fg.getchannel("A").point(lambda v: 255 if v > 24 else 0)
    bb = a.getbbox()
    cx, cy = (bb[0] + bb[2]) / 2, (bb[1] + bb[3]) / 2
    ap = a.load()
    rmax = max(((x - cx) ** 2 + (y - cy) ** 2) ** 0.5 for y in range(bb[1], bb[3]) for x in range(bb[0], bb[2]) if ap[x, y])
    S = 432
    k = SAFE * S / rmax
    big = fg.resize((round(512 * k), round(512 * k)), Image.LANCZOS)
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    layer.alpha_composite(big, (round(S / 2 - cx * k), round(S / 2 - cy * k)))
    layer.save(ICON_DIR / "icon_adaptive_fg.png", optimize=True)
    bg = Image.open(SRC / "c3b-layers/c3b_bg_raw.png").convert("RGB").resize((S, S), Image.LANCZOS)
    bg.convert("RGBA").save(ICON_DIR / "icon_adaptive_bg.png", optimize=True)
    prev = Image.alpha_composite(bg.convert("RGBA"), layer)
    prev.save(SRC / "c3b-layers/adaptive_preview_432.png")
    for p in sorted(ICON_DIR.glob("*.png")) + sorted(STORE.glob("*.png")):
        im = Image.open(p)
        print(f"ICON {p.relative_to(GAME)} {im.size[0]}x{im.size[1]} {im.mode} {p.stat().st_size} B")
    print(f"ADAPTIVE fg scale {k:.3f} (art radius {rmax:.0f}/512 -> {SAFE * S:.0f}/{S})")


main()
