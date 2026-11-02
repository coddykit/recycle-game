#!/usr/bin/env python3
"""Volt City app-icon gate: Unity's default icon must never ship (owner 2026-10-29, icon = candidate C3b).

  python3 icon_check.py              static (check.sh): the 5 icon PNGs exist with the right size / alpha, and
                                     ProjectSettings.asset references them in EVERY slot (default icon + Android adaptive
                                     bg+fg x6, round x6, legacy x6). An empty slot = Unity's default icon.
  python3 icon_check.py --aab X.aab  built bundle (build_unity_android.sh): the launcher icons inside the AAB exist
                                     (adaptive xml + xxxhdpi PNGs) and look like OUR art (pixel distance to the source).
Exit 0 = pass.
"""
import io
import re
import sys
import zipfile
from pathlib import Path

from PIL import Image, ImageChops, ImageStat

UNITY = Path(__file__).resolve().parents[1]
ICON = UNITY / "Assets/Art/Icon"
FILES = {  # name: (size, needs transparent corners)
    "icon_full.png": (1024, False),
    "icon_adaptive_fg.png": (432, True),
    "icon_adaptive_bg.png": (432, False),
    "icon_round.png": (512, True),
    "icon_legacy.png": (512, True),
}
KINDS = {0: ("legacy", ["icon_legacy.png"]), 1: ("round", ["icon_round.png"]),
         2: ("adaptive", ["icon_adaptive_bg.png", "icon_adaptive_fg.png"])}


def guid(name):
    m = re.search(r"^guid: ([0-9a-f]{32})", (ICON / (name + ".meta")).read_text(), re.M)
    return m.group(1) if m else None


def static_check():
    bad = []
    guids = {}
    for name, (size, corners) in FILES.items():
        p = ICON / name
        if not p.exists():
            bad.append(f"missing {p.relative_to(UNITY)}")
            continue
        if not (ICON / (name + ".meta")).exists():
            bad.append(f"missing {name}.meta")
            continue
        guids[name] = guid(name)
        im = Image.open(p)
        if im.size != (size, size):
            bad.append(f"{name}: {im.size} != {size}x{size}")
        if corners:
            a = im.convert("RGBA").getchannel("A")
            if a.getpixel((0, 0)) != 0 or a.getpixel((size - 1, size - 1)) != 0:
                bad.append(f"{name}: corners must be transparent")
        elif im.convert("RGBA").getchannel("A").getextrema()[0] < 255:
            bad.append(f"{name}: must be opaque")
    ps = (UNITY / "ProjectSettings/ProjectSettings.asset").read_text()
    m = re.search(r"m_BuildTargetIcons:(.*?)\n  m_BuildTargetPlatformIcons:", ps, re.S)
    if not m or guids.get("icon_full.png", "x") not in m.group(1):
        bad.append("Player Settings default icon is not icon_full.png (Unity default icon would be used)")
    m = re.search(r"m_BuildTargetPlatformIcons:(.*?)\n  m_BuildTarget[A-Z]", ps, re.S)
    android = re.search(r"- m_BuildTarget: Android\n(.*?)(?=\n  - m_BuildTarget:|\Z)", m.group(1) if m else "", re.S)
    slots = re.findall(r"- m_Textures:(.*?)m_Width: (\d+).*?m_Kind: (\d)", android.group(1) if android else "", re.S)
    seen = {0: 0, 1: 0, 2: 0}
    for tex, width, kind in slots:
        kind = int(kind)
        if kind not in KINDS:
            continue
        seen[kind] += 1
        label, want = KINDS[kind]
        for w in want:
            if guids.get(w, "x") not in tex:
                bad.append(f"android {label} {width}px: {w} not set (Unity default icon)")
    for kind, n in seen.items():
        if n < 6:
            bad.append(f"android {KINDS[kind][0]}: {n}/6 slots in ProjectSettings")
    return bad


def distance(a, b):
    a = a.convert("RGB").resize((64, 64), Image.LANCZOS)
    b = b.convert("RGB").resize((64, 64), Image.LANCZOS)
    return sum(ImageStat.Stat(ImageChops.difference(a, b)).mean) / 3


def on_black(im):
    im = im.convert("RGBA")
    bg = Image.new("RGBA", im.size, (0, 0, 0, 255))
    return Image.alpha_composite(bg, im)


def aab_check(aab):
    bad = []
    z = zipfile.ZipFile(aab)
    names = z.namelist()
    icons = [n for n in names if re.match(r"base/res/mipmap-[a-z0-9-]+/(app_icon|ic_launcher)[a-z_]*\.(png|xml|webp)$", n)]
    if not any(n.endswith("anydpi-v26/app_icon.xml") for n in icons):
        bad.append("no adaptive icon xml (base/res/mipmap-anydpi-v26/app_icon.xml)")
    xxx = {Path(n).stem: n for n in icons if "xxxhdpi" in n and not n.endswith(".xml")}
    # Unity 6 writes the adaptive layers as ic_launcher_{foreground,background}, referenced by anydpi-v26/app_icon.xml
    checks = {"ic_launcher_foreground": "icon_adaptive_fg.png", "ic_launcher_background": "icon_adaptive_bg.png",
              "app_icon_round": "icon_round.png", "app_icon": "icon_legacy.png"}
    for stem, src in checks.items():
        if stem not in xxx:
            bad.append(f"xxxhdpi {stem}.png missing (have: {sorted(xxx)})")
            continue
        got = Image.open(io.BytesIO(z.read(xxx[stem])))
        d = distance(on_black(got), on_black(Image.open(ICON / src)))
        print(f"AAB {xxx[stem]} {got.size[0]}x{got.size[1]} distance to {src} = {d:.1f}")
        if d > 18:
            bad.append(f"{stem}: does not match {src} (distance {d:.1f}) - Unity default icon?")
    return bad


def main():
    if len(sys.argv) > 2 and sys.argv[1] == "--aab":
        bad = aab_check(sys.argv[2])
        tag = "ICON_AAB"
    else:
        bad = static_check()
        tag = "ICON_CHECK"
    for b in bad:
        print("FAIL", b)
    print(f"{tag}: {'PASS' if not bad else 'FAIL'}")
    sys.exit(1 if bad else 0)


main()
