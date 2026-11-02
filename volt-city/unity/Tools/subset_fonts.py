#!/usr/bin/env python3
"""Volt City UI fonts (P5, games/CLAUDE.md §9: fonts ship with the game, never trust the system fallback).

Noto Sans (Latin + Cyrillic + symbols) is the main face; Noto Sans JP / KR / Devanagari are fallbacks. Each is cut to
exactly the characters the game can show and instanced to static Regular (400) + Bold (700), so the AAB grows by a few
hundred KB instead of ~25 MB. Layout features are kept whole (Devanagari conjuncts / matras need GSUB + GPOS).

    /Volumes/BuildSSD/lego-fonts-2026-10-09/venv/bin/python games/volt-city/unity/Tools/subset_fonts.py \
        --src /Volumes/BuildSSD/tmp/volt-fonts-src          # NotoSans.ttf NotoSansJP.ttf NotoSansKR.ttf NotoSansDevanagari.ttf
    ... --check   # exit 1 when a shown character is missing from the bundled fonts (run by Tools/check.sh when fontTools exists)

Characters: every text file under Assets/Resources (strings.csv, data/*.json: IAP names, …) and every Assets/Scripts .cs
(native language names in the picker, ★ ✓ ·). Sources: Google Fonts (OFL; licence copied next to the output).
"""
import argparse
import glob
import io
import os
import sys

UNITY = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(UNITY, "Assets", "Resources", "ui", "fonts")
FACES = {  # output name -> (source file, char filter)
    "NotoSans": ("NotoSans.ttf", lambda c: ord(c) < 0x0900 or 0x1E00 <= ord(c) < 0x2E80),
    "NotoSansDevanagari": ("NotoSansDevanagari.ttf", lambda c: 0x0900 <= ord(c) < 0x0980 or ord(c) in (0x200C, 0x200D, 0x25CC)),
    "NotoSansJP": ("NotoSansJP.ttf", lambda c: ord(c) >= 0x2E80 and not 0xAC00 <= ord(c) <= 0xD7AF and not 0x1100 <= ord(c) <= 0x11FF and not 0x3130 <= ord(c) <= 0x318F),
    "NotoSansKR": ("NotoSansKR.ttf", lambda c: 0xAC00 <= ord(c) <= 0xD7AF or 0x1100 <= ord(c) <= 0x11FF or 0x3130 <= ord(c) <= 0x318F),
    # symbols Noto Sans lacks (★ ☆ ✓ ▶ / → ↔ ⚙): last in the fallback chain, filled from the leftovers in per_face
    "NotoSansSymbols2": ("NotoSansSymbols2.ttf", None),
    "NotoSansSymbols": ("NotoSansSymbols.ttf", None),
}
ALWAYS = {
    "NotoSans": {chr(c) for c in range(0x20, 0x7F)} | set("★☆✓✔✕×·•…–—‘’“”«»€£¥₹₩₽°±%‰→←↑↓⚡"),
    "NotoSansDevanagari": {chr(c) for c in range(0x0966, 0x0970)} | {"।", "॥", "◌"},
    "NotoSansJP": {chr(c) for c in range(0x3000, 0x3040)} | {chr(c) for c in range(0x3040, 0x3100)} | {chr(c) for c in range(0xFF01, 0xFF5F)},
    "NotoSansKR": set(),
    "NotoSansSymbols2": set("★☆✓✔▶✕"),
    "NotoSansSymbols": set("→↔⚙"),
}


def shown_chars():
    text = io.StringIO()
    for f in glob.glob(os.path.join(UNITY, "Assets", "Resources", "**", "*"), recursive=True):
        if f.endswith((".csv", ".json", ".txt")) and os.path.isfile(f):
            text.write(open(f, encoding="utf-8").read())
    # the game's AND the kit's code (the kit's close chip "✕" was missing: device frame 10-29, notdef box)
    cs = glob.glob(os.path.join(UNITY, "Assets", "Scripts", "**", "*.cs"), recursive=True) + \
         glob.glob(os.path.join(UNITY, "Assets", "CoddyKit", "Runtime", "**", "*.cs"), recursive=True)
    for f in cs:
        for line in open(f, encoding="utf-8"):
            text.write(line.split("//")[0] if '"' not in line.split("//")[0] or line.count('"') % 2 == 0 else line)
    s = text.getvalue()
    chars = set()
    for c in s:
        if c.isprintable() or c in "‌‍":
            chars.add(c)
            if c.isalpha():
                chars |= set(c.upper()) | set(c.lower())
    return chars


def per_face(chars, src):
    from fontTools.ttLib import TTFont
    out = {}
    cmaps = {n: set(TTFont(os.path.join(src, f)).getBestCmap()) for n, (f, _) in FACES.items()}
    covered = set()
    for name, (_, keep) in FACES.items():
        if keep is None:
            # symbols: whatever is shown and not drawable by an earlier face
            out[name] = {c for c in chars if ord(c) not in covered and ord(c) in cmaps[name]} | ALWAYS[name]
        else:
            out[name] = {c for c in chars if keep(c) and ord(c) in cmaps[name]} | ALWAYS[name]
        covered |= {ord(c) for c in out[name]} & cmaps[name]
    return out


def build(src):
    from fontTools import subset
    from fontTools.ttLib import TTFont
    from fontTools.varLib import instancer
    os.makedirs(OUT, exist_ok=True)
    sets = per_face(shown_chars(), src)
    for name, (fname, _) in FACES.items():
        for style, wght in (("Regular", 400), ("Bold", 700)) if not name.startswith("NotoSansSymbols") else (("Regular", 400),):
            f = TTFont(os.path.join(src, fname))
            if "fvar" in f:
                axes = {a.axisTag: a for a in f["fvar"].axes}
                loc = {"wght": wght}
                if "wdth" in axes:
                    loc["wdth"] = 100
                f = instancer.instantiateVariableFont(f, loc, updateFontNames=True)
            opts = subset.Options()
            opts.layout_features = ["*"]
            opts.name_IDs = ["*"]
            opts.notdef_outline = True
            opts.glyph_names = False
            opts.hinting = False
            sub = subset.Subsetter(opts)
            sub.populate(unicodes=[ord(c) for c in sets[name]])
            sub.subset(f)
            path = os.path.join(OUT, f"{name}-{style}.ttf")
            f.save(path)
            print(f"{os.path.relpath(path, UNITY)}: {len(sets[name])} chars, {os.path.getsize(path) // 1024} KB")
    lic = os.path.join(src, "OFL.txt")
    if os.path.exists(lic):
        with open(lic, encoding="utf-8") as a, open(os.path.join(OUT, "OFL.txt"), "w", encoding="utf-8") as b:
            b.write(a.read())


def check():
    from fontTools.ttLib import TTFont
    have = set()
    for name in FACES:
        p = os.path.join(OUT, f"{name}-Regular.ttf")
        if not os.path.exists(p):
            print(f"FONTS: missing {p}")
            return 1
        have |= set(TTFont(p).getBestCmap().keys())
    missing = sorted({c for c in shown_chars() if ord(c) > 0x20 and ord(c) not in have and c not in "‌‍﻿"})
    if missing:
        print("FONTS: %d shown characters missing: %s" % (len(missing), "".join(missing[:80])))
        return 1
    print("FONTS: every shown character is in the bundled fonts")
    return 0


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default="/Volumes/BuildSSD/tmp/volt-fonts-src")
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    sys.exit(check() if a.check else build(a.src) or check())
