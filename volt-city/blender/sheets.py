"""Volt City P2 frame sheets + rendered-pixel colour check (Pillow only):
    python3 blender/sheets.py <frames dir>     -> compare-<moment>.png, tiers.png, pixel_check.json; exit 1 on a FAIL
compare-<moment>.png = before (plain import look, Medium) | after Medium | after Low (85 % internal, upscaled),
tiers.png = Low row / Medium row for dusk, night, storm (+ the ripple close-up when present).
Pixel check per frame: (1) not flat (the largest 16-level histogram bin < 70 % of pixels: a failed shader renders one
flat colour; 540 x 1170 sample), (2) dusk / night / storm show lit windows + lamp pools (warm bright pixels >= 0.25 %),
(3) the power network reads (cyan pixels >= 0.05 % in after frames), (4) mean luminance inside the moment's band (night never so
dark that decor disappears), (5) no real utility logo colour PAIR: for every pair both colours may not exceed 1.5 %
of the frame each (generic utility-truck yellow alone is allowed)."""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

OUT = Path(sys.argv[1] if len(sys.argv) > 1 else "/Volumes/BuildSSD/game-builds/volt-city/p2")
W, H = 1080, 2340
MOMENTS = ["dusk", "night", "storm"]
# approximate brand colour pairs of real utilities / grid operators (sRGB); never both dominant in one frame
BRAND_PAIRS = {
    "pair_a": ((0, 114, 206), (255, 255, 255)), "pair_b": ((10, 61, 123), (246, 168, 0)),
    "pair_c": ((0, 163, 224), (122, 184, 0)), "pair_d": ((254, 88, 21), (0, 26, 112)),
    "pair_e": ((234, 28, 10), (255, 255, 255)), "pair_f": ((0, 20, 140), (0, 160, 223)),
    "pair_g": ((92, 182, 21), (255, 130, 0)), "pair_h": ((255, 218, 0), (32, 113, 181)),
    "pair_i": ((229, 0, 18), (255, 255, 255)),
}
LUM_BAND = {"dusk": (0.08, 0.6), "night": (0.03, 0.35), "storm": (0.06, 0.55), "ripple": (0.03, 0.4)}


def load(p):
    im = Image.open(p).convert("RGB")
    return im if im.size == (W, H) else im.resize((W, H), Image.BILINEAR)


def stats(im):
    small = im.resize((540, 1170), Image.BILINEAR)
    px = list(small.get_flattened_data()) if hasattr(small, "get_flattened_data") else list(small.getdata())
    n = len(px)
    hist = {}
    warm = cyan = 0
    lum = 0.0
    near = {k: [0, 0] for k in BRAND_PAIRS}
    for r, g, b in px:
        key = (r >> 4, g >> 4, b >> 4)
        hist[key] = hist.get(key, 0) + 1
        y = (.2126 * r + .7152 * g + .0722 * b) / 255
        lum += y
        if r > 170 and g > 90 and r - b > 45:
            warm += 1
        if b > 100 and b - r > 30 and g - r > 25:
            cyan += 1
        for k, (c1, c2) in BRAND_PAIRS.items():
            for j, c in enumerate((c1, c2)):
                if abs(r - c[0]) + abs(g - c[1]) + abs(b - c[2]) < 36:
                    near[k][j] += 1
    return {"flat_bin": max(hist.values()) / n, "warm": warm / n, "cyan": cyan / n, "lum": lum / n,
            "brand": {k: (v[0] / n, v[1] / n) for k, v in near.items()}}


def label(im, text):
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, im.width, 54), fill=(12, 18, 28))
    d.text((18, 16), text, fill=(235, 235, 235))
    return im


def main():
    results = {}
    fails = []
    moments = MOMENTS + (["ripple"] if (OUT / "medium/ripple.png").exists() else [])
    for m in moments:
        frames = {}
        for t in ("before", "medium", "low"):
            p = OUT / t / f"{m}.png"
            if p.exists():
                frames[t] = load(p)
        for t, im in frames.items():
            s = stats(im)
            results[f"{t}/{m}"] = s
            if t == "before":
                continue
            lo, hi = LUM_BAND[m]
            checks = {"not_flat": s["flat_bin"] < .7, "lit_windows": s["warm"] >= .0025,
                      "network_cyan": s["cyan"] >= .0005, "luminance": lo <= s["lum"] <= hi,
                      "no_brand_pair": all(not (a > .015 and b > .015) for a, b in s["brand"].values())}
            s["checks"] = checks
            for k, ok in checks.items():
                if not ok:
                    fails.append(f"{t}/{m}:{k}")
        cols = [t for t in ("before", "medium", "low") if t in frames]
        sheet = Image.new("RGB", (W // 2 * len(cols), H // 2), (8, 10, 14))
        for i, t in enumerate(cols):
            name = {"before": "before (plain import)", "medium": "after Medium", "low": "after Low (0.85 scale)"}[t]
            sheet.paste(label(frames[t].resize((W // 2, H // 2), Image.LANCZOS), f"{m} - {name}"), (i * W // 2, 0))
        sheet.save(OUT / f"compare-{m}.png", optimize=True)
    tiers = Image.new("RGB", (W // 3 * len(moments), H // 3 * 2), (8, 10, 14))
    for i, m in enumerate(moments):
        for j, t in enumerate(("low", "medium")):
            p = OUT / t / f"{m}.png"
            if p.exists():
                tiers.paste(label(load(p).resize((W // 3, H // 3), Image.LANCZOS), f"{t} {m}"), (i * W // 3, j * H // 3))
    tiers.save(OUT / "tiers.png", optimize=True)
    (OUT / "pixel_check.json").write_text(json.dumps({"results": results, "fails": fails}, indent=1) + "\n")
    for k, s in results.items():
        print(f"PIXEL {k} lum={s['lum']:.3f} warm={s['warm'] * 100:.2f}% cyan={s['cyan'] * 100:.2f}% flat={s['flat_bin']:.2f}"
              + ("" if "checks" not in s else " " + ("PASS" if all(s["checks"].values()) else "FAIL")))
    print(f"PIXEL_CHECK: {len(results)} frames, {len(fails)} failures {fails}")
    sys.exit(1 if fails else 0)


main()
