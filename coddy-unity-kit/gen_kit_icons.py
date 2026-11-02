#!/usr/bin/env python3
"""CoddyKit (Unity) UI icons: white glyphs on transparency, 96 px, drawn deterministically with Pillow and tinted in USS
(-unity-background-image-tint-color). Replaces unicode symbols on buttons (✕ ‹ ⌂ ✉ 🔒 📌 ✓ ⚙): a device draws a glyph
only when a font has it, and an emoji/symbol missing from every font is an invisible or broken button (SE 2.0.0 device
check 2026-10-30: ✉ invisible, ⌂ broken in every language).

  python3 Tools/gen_kit_icons.py <unity_project>           # writes Assets/CoddyKit/UI/Resources/coddykit/icons/*.png
  python3 Tools/gen_kit_icons.py <unity_project> --check   # exit 1 if a file is missing / differs
"""
import io
import sys
from pathlib import Path

from PIL import Image, ImageDraw

S, K = 96, 4
W = (255, 255, 255, 255)


def canvas():
    im = Image.new("RGBA", (S * K, S * K), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)


def P(*pts):
    return [(x * K, y * K) for x, y in pts]


def R(x0, y0, x1, y1):
    return [x0 * K, y0 * K, x1 * K, y1 * K]


def line(d, pts, w):
    d.line(P(*pts), fill=W, width=int(w * K), joint="curve")
    for x, y in pts:
        r = w / 2
        d.ellipse(R(x - r, y - r, x + r, y + r), fill=W)


def close(d):
    line(d, [(26, 26), (70, 70)], 10)
    line(d, [(70, 26), (26, 70)], 10)


def back(d):
    line(d, [(60, 20), (32, 48), (60, 76)], 11)


def home(d):
    d.polygon(P((48, 14), (88, 48), (78, 48), (78, 84), (56, 84), (56, 62), (40, 62), (40, 84), (18, 84), (18, 48), (8, 48)), fill=W)


def mail(d):
    d.rounded_rectangle(R(12, 24, 84, 74), radius=6 * K, outline=W, width=8 * K)
    line(d, [(16, 30), (48, 54), (80, 30)], 8)


def lock(d):
    d.rounded_rectangle(R(20, 44, 76, 86), radius=7 * K, fill=W)
    d.arc(R(30, 14, 66, 56), 180, 360, fill=W, width=9 * K)
    d.line(P((30.5, 35), (30.5, 46)), fill=W, width=9 * K)
    d.line(P((65.5, 35), (65.5, 46)), fill=W, width=9 * K)
    d.ellipse(R(43, 56, 53, 66), fill=(0, 0, 0, 0))
    d.rectangle(R(46, 62, 50, 74), fill=(0, 0, 0, 0))


def pin(d):
    d.ellipse(R(26, 8, 70, 52), fill=W)
    d.ellipse(R(40, 22, 56, 38), fill=(0, 0, 0, 0))
    d.polygon(P((30, 42), (66, 42), (48, 90)), fill=W)


def check(d):
    line(d, [(18, 50), (40, 72), (80, 26)], 11)


def gear(d):
    import math
    cx = cy = 48
    pts = []
    for i in range(16):
        a = math.pi * 2 * i / 16
        r = 40 if i % 2 == 0 else 30
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    d.polygon(P(*pts), fill=W)
    d.ellipse(R(22, 22, 74, 74), fill=W)
    d.ellipse(R(36, 36, 60, 60), fill=(0, 0, 0, 0))


def star(d):
    import math
    pts = []
    for i in range(10):
        a = -math.pi / 2 + math.pi * i / 5
        r = 42 if i % 2 == 0 else 18
        pts.append((48 + r * math.cos(a), 52 + r * math.sin(a)))
    d.polygon(P(*pts), fill=W)


ICONS = {"close": close, "back": back, "home": home, "mail": mail, "lock": lock, "pin": pin, "check": check, "gear": gear, "star": star}


def render(fn):
    im, d = canvas()
    fn(d)
    out = io.BytesIO()
    im.resize((S, S), Image.LANCZOS).save(out, "PNG", optimize=True)
    return out.getvalue()


def main():
    proj = Path(sys.argv[1]).resolve()
    out = proj / "Assets/CoddyKit/UI/Resources/coddykit/icons"
    chk = "--check" in sys.argv
    bad = 0
    out.mkdir(parents=True, exist_ok=True)
    for name, fn in ICONS.items():
        data, p = render(fn), out / f"{name}.png"
        if chk:
            if not p.exists() or p.read_bytes() != data:
                print(f"KIT_ICONS: stale {p.name}"); bad = 1
        else:
            p.write_bytes(data)
    print(("KIT_ICONS: FAIL" if bad else "KIT_ICONS: PASS") + f" ({len(ICONS)} icons, {out.relative_to(proj)})")
    return bad


if __name__ == "__main__":
    sys.exit(main())
