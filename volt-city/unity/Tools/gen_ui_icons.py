#!/usr/bin/env python3
"""Volt City UI icons (P3): simple white glyphs on transparency, 96 px, drawn deterministically with Pillow and tinted in
USS (--unity-image-tint-color). Generic shapes only (a bolt, a truck, buildings, a flag…) — no brand marks, no real
utility logos (DESIGN section 11: a lightning bolt alone is generic, no circle-bolt logo).

  python3 games/volt-city/unity/Tools/gen_ui_icons.py            # writes Assets/Resources/ui/icons/*.png
  python3 games/volt-city/unity/Tools/gen_ui_icons.py --check    # exit 1 if a file is missing / differs
"""
import io
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
OUT = HERE.parent / "Assets/Resources/ui/icons"
S = 96          # final size
K = 4           # supersampling
W = (255, 255, 255, 255)


def canvas():
    im = Image.new("RGBA", (S * K, S * K), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)


def P(*pts):
    return [(x * K, y * K) for x, y in pts]


def R(x0, y0, x1, y1):
    return [x0 * K, y0 * K, x1 * K, y1 * K]


def bolt(d):
    d.polygon(P((54, 8), (22, 54), (44, 54), (36, 88), (74, 38), (51, 38), (62, 8)), fill=W)


def truck(d):
    d.rounded_rectangle(R(10, 36, 58, 70), 4 * K, fill=W)
    d.polygon(P((58, 44), (76, 44), (88, 58), (88, 70), (58, 70)), fill=W)
    for cx in (26, 72):
        d.ellipse(R(cx - 9, 62, cx + 9, 80), fill=(0, 0, 0, 0))
        d.ellipse(R(cx - 7, 64, cx + 7, 78), fill=W)
    d.line(P((22, 36), (40, 18)), fill=W, width=5 * K)   # bucket boom


def city(d):
    d.rectangle(R(10, 44, 30, 86), fill=W)
    d.rectangle(R(36, 18, 58, 86), fill=W)
    d.rectangle(R(64, 34, 86, 86), fill=W)
    for y in range(26, 80, 12):
        d.rectangle(R(42, y, 46, y + 5), fill=(0, 0, 0, 0))
        d.rectangle(R(49, y, 53, y + 5), fill=(0, 0, 0, 0))


def build(d):
    d.rounded_rectangle(R(14, 54, 82, 86), 4 * K, fill=W)
    d.polygon(P((48, 12), (84, 48), (12, 48)), fill=W)
    d.rectangle(R(42, 64, 54, 86), fill=(0, 0, 0, 0))


def bag(d):
    d.rounded_rectangle(R(16, 34, 80, 86), 8 * K, fill=W)
    d.arc(R(30, 12, 66, 52), 180, 360, fill=W, width=6 * K)


def lock(d):
    # a padlock: shackle arc over a rounded body with a keyhole (City hookup "between shifts", Patron #173)
    d.arc(R(28, 10, 68, 56), 180, 360, fill=W, width=9 * K)
    d.line(P((32.5, 32), (32.5, 46)), fill=W, width=9 * K)
    d.line(P((63.5, 32), (63.5, 46)), fill=W, width=9 * K)
    d.rounded_rectangle(R(18, 42, 78, 88), 9 * K, fill=W)
    d.ellipse(R(42, 54, 54, 66), fill=(0, 0, 0, 0))
    d.rectangle(R(45.5, 62, 50.5, 76), fill=(0, 0, 0, 0))


def gear(d):
    c = 48
    for i in range(8):
        a = i * math.pi / 4
        x, y = c + 34 * math.cos(a), c + 34 * math.sin(a)
        d.ellipse(R(x - 9, y - 9, x + 9, y + 9), fill=W)
    d.ellipse(R(18, 18, 78, 78), fill=W)
    d.ellipse(R(36, 36, 60, 60), fill=(0, 0, 0, 0))


def check(d):
    d.line(P((18, 50), (40, 72), (80, 26)), fill=W, width=11 * K, joint="curve")


def arrow(d, up):
    if up:
        d.polygon(P((48, 14), (82, 52), (60, 52), (60, 84), (36, 84), (36, 52), (14, 52)), fill=W)
    else:
        d.polygon(P((48, 84), (82, 46), (60, 46), (60, 14), (36, 14), (36, 46), (14, 46)), fill=W)


def spark(d):
    pts = []
    for i in range(16):
        r = 42 if i % 2 == 0 else 16
        a = i * math.pi / 8
        pts.append((48 + r * math.cos(a), 48 + r * math.sin(a)))
    d.polygon(P(*pts), fill=W)


def district(d):
    d.polygon(P((14, 46), (36, 26), (58, 46)), fill=W)
    d.rectangle(R(18, 46, 54, 84), fill=W)
    d.rectangle(R(60, 30, 84, 84), fill=W)
    d.rectangle(R(30, 62, 40, 84), fill=(0, 0, 0, 0))


def hospital(d):
    d.rounded_rectangle(R(12, 12, 84, 84), 14 * K, fill=W)
    d.rectangle(R(41, 24, 55, 72), fill=(0, 0, 0, 0))
    d.rectangle(R(24, 41, 72, 55), fill=(0, 0, 0, 0))


def flag(d):
    d.rectangle(R(18, 10, 25, 88), fill=W)
    d.polygon(P((25, 12), (80, 24), (25, 50)), fill=W)


def star(d):
    pts = []
    for i in range(10):
        r = 42 if i % 2 == 0 else 18
        a = -math.pi / 2 + i * math.pi / 5
        pts.append((48 + r * math.cos(a), 50 + r * math.sin(a)))
    d.polygon(P(*pts), fill=W)


def line(d):
    # a pylon with conductors
    d.polygon(P((48, 8), (66, 88), (56, 88), (48, 40), (40, 88), (30, 88)), fill=W)
    d.rectangle(R(18, 22, 78, 28), fill=W)
    d.rectangle(R(24, 38, 72, 43), fill=W)


def ops(d):
    d.ellipse(R(14, 14, 82, 82), outline=W, width=9 * K)
    d.line(P((48, 48), (48, 24)), fill=W, width=8 * K)
    d.line(P((48, 48), (66, 58)), fill=W, width=8 * K)


def report(d):
    d.rounded_rectangle(R(18, 10, 78, 86), 8 * K, fill=W)
    for y in (28, 44, 60):
        d.rectangle(R(30, y, 66, y + 6), fill=(0, 0, 0, 0))


def cash(d):
    d.rounded_rectangle(R(8, 26, 88, 72), 8 * K, fill=W)
    d.ellipse(R(36, 34, 60, 64), fill=(0, 0, 0, 0))


def leaf(d):
    d.chord(R(14, 10, 82, 86), 200, 70, fill=W)
    d.line(P((22, 80), (64, 34)), fill=(0, 0, 0, 0), width=5 * K)


def moon(d):
    d.ellipse(R(12, 12, 84, 84), fill=W)
    d.ellipse(R(34, 4, 96, 66), fill=(0, 0, 0, 0))


def clock(d):
    d.ellipse(R(10, 10, 86, 86), outline=W, width=9 * K)
    d.line(P((48, 48), (48, 24)), fill=W, width=8 * K)
    d.line(P((48, 48), (66, 48)), fill=W, width=8 * K)


def event(d):
    d.polygon(P((48, 8), (90, 84), (6, 84)), fill=W)
    d.rectangle(R(44, 34, 52, 62), fill=(0, 0, 0, 0))
    d.rectangle(R(44, 68, 52, 76), fill=(0, 0, 0, 0))


def tip(d):
    d.ellipse(R(22, 8, 74, 60), fill=W)
    d.rectangle(R(36, 56, 60, 74), fill=W)
    d.rectangle(R(38, 78, 58, 86), fill=W)


def cloud(d):
    d.ellipse(R(10, 40, 46, 76), fill=W)
    d.ellipse(R(30, 22, 72, 64), fill=W)
    d.ellipse(R(54, 40, 88, 76), fill=W)
    d.rectangle(R(28, 56, 72, 76), fill=W)


def restore(d):
    d.arc(R(14, 14, 82, 82), 40, 330, fill=W, width=9 * K)
    d.polygon(P((70, 6), (86, 30), (60, 32)), fill=W)


def globe(d):
    d.ellipse(R(10, 10, 86, 86), outline=W, width=7 * K)
    d.ellipse(R(32, 10, 64, 86), outline=W, width=6 * K)
    d.line(P((12, 48), (84, 48)), fill=W, width=6 * K)


def meter(d):
    d.pieslice(R(8, 20, 88, 100), 180, 360, fill=W)
    d.pieslice(R(26, 38, 70, 82), 180, 360, fill=(0, 0, 0, 0))
    d.line(P((48, 60), (70, 30)), fill=W, width=7 * K)


def plant_steam(d):
    d.rectangle(R(12, 50, 70, 86), fill=W)
    d.rectangle(R(56, 18, 68, 50), fill=W)
    for i, (x, y, r) in enumerate(((72, 14, 9), (82, 6, 7))):
        d.ellipse(R(x - r, y - r, x + r, y + r), fill=W)


def plant_gas(d):
    d.rounded_rectangle(R(10, 40, 86, 80), 10 * K, fill=W)
    d.rectangle(R(20, 22, 32, 40), fill=W)
    bolt_small = P((52, 46), (40, 64), (50, 64), (46, 76), (60, 56), (50, 56), (56, 46))
    d.polygon(bolt_small, fill=(0, 0, 0, 0))


def plant_hydro(d):
    d.polygon(P((10, 30), (86, 30), (74, 86), (22, 86)), fill=W)
    for y in (46, 62):
        d.arc(R(26, y - 8, 70, y + 8), 20, 160, fill=(0, 0, 0, 0), width=5 * K)


def plant_wind(d):
    d.polygon(P((46, 40), (50, 40), (54, 90), (42, 90)), fill=W)
    c = (48, 36)
    for i in range(3):
        a = -math.pi / 2 + i * 2 * math.pi / 3
        x, y = c[0] + 36 * math.cos(a), c[1] + 36 * math.sin(a)
        nx, ny = -math.sin(a) * 6, math.cos(a) * 6
        d.polygon(P((c[0] + nx, c[1] + ny), (x, y), (c[0] - nx, c[1] - ny)), fill=W)
    d.ellipse(R(42, 30, 54, 42), fill=W)


def plant_solar(d):
    d.polygon(P((16, 34), (80, 34), (90, 74), (6, 74)), fill=W)
    for x in (32, 48, 64):
        d.line(P((x, 34), (x + (x - 48) * 0.25, 74)), fill=(0, 0, 0, 0), width=3 * K)
    d.line(P((11, 54), (85, 54)), fill=(0, 0, 0, 0), width=3 * K)
    d.rectangle(R(44, 74, 52, 88), fill=W)


def plant_battery(d):
    d.rounded_rectangle(R(12, 24, 78, 76), 8 * K, fill=W)
    d.rectangle(R(78, 40, 88, 60), fill=W)
    d.rectangle(R(22, 34, 56, 66), fill=(0, 0, 0, 0))


def dot(d):
    d.ellipse(R(30, 30, 66, 66), fill=W)


ICONS = {
    "lock": lock,
    "bolt": bolt, "truck": truck, "city": city, "build": build, "bag": bag, "gear": gear, "check": check,
    "up": lambda d: arrow(d, True), "down": lambda d: arrow(d, False), "spark": spark, "district": district,
    "hospital": hospital, "flag": flag, "star": star, "line": line, "ops": ops, "report": report, "cash": cash,
    "leaf": leaf, "moon": moon, "clock": clock, "event": event, "tip": tip, "cloud": cloud, "restore": restore,
    "globe": globe, "meter": meter, "plant_steam": plant_steam, "plant_gas": plant_gas, "plant_hydro": plant_hydro,
    "plant_wind": plant_wind, "plant_solar": plant_solar, "plant_battery": plant_battery, "dot": dot,
}


def render(fn):
    im, d = canvas()
    fn(d)
    im = im.resize((S, S), Image.LANCZOS)
    buf = io.BytesIO()
    im.save(buf, "PNG", optimize=True)
    return buf.getvalue()


def main():
    check = "--check" in sys.argv
    OUT.mkdir(parents=True, exist_ok=True)
    bad = []
    for name in sorted(ICONS):
        data = render(ICONS[name])
        p = OUT / (name + ".png")
        if check:
            if not p.exists() or p.read_bytes() != data:
                bad.append(name)
        else:
            p.write_bytes(data)
    if check:
        print("UI_ICONS:", "fresh" if not bad else "STALE " + ",".join(bad))
        return 1 if bad else 0
    print(f"UI_ICONS: {len(ICONS)} icons -> {OUT.relative_to(HERE.parent)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
