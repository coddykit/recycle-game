#!/usr/bin/env python3
"""Store-art glyph gate (fonttools, offline; port of games/driftwood/tools/store/validate_fonts.py): every character of
store/circuit/copy.json (+ the header brand and the page counter) is in the face that renders its language in render.mjs —
Latin / Cyrillic in Noto Sans, ja in Noto Sans JP, ko in Noto Sans KR, zh in Noto Sans SC (700 and 900) — so no slide
falls back to a system face or shows tofu. Fonts = the sha256 pins of store/circuit/fonts.json.
  python3 store/circuit/validate_fonts.py [--out DIR]   -> STORE_GLYPHS_PASS (+ DIR/font-coverage.json)"""
import hashlib
import json
import os
import sys
from pathlib import Path

from fontTools.ttLib import TTFont

HERE = Path(__file__).resolve().parent
SRC = Path(os.environ.get("FONT_SRC", "/Volumes/BuildSSD/game-deps/font-src"))
DECK = HERE.parent / "deck/fonts"
copy_file = HERE / "copy.json"
copy = json.loads(copy_file.read_text(encoding="utf-8"))
pins = json.loads((HERE / "fonts.json").read_text(encoding="utf-8"))
FILES = {"NotoSans.ttf": SRC, "NotoSansJP.ttf": SRC, "NotoSansKR.ttf": SRC, "Noto-Sans-SC_wght-700.ttf": DECK, "Noto-Sans-SC_wght-900.ttf": DECK}
cmap = {}
for n, d in FILES.items():
    h = hashlib.sha256((d / n).read_bytes()).hexdigest()
    assert h == pins[n], f"{n} is not the pinned font"
    cmap[n] = set(TTFont(d / n).getBestCmap())
LEAD = {"ja-JP": ["NotoSansJP.ttf"], "ko-KR": ["NotoSansKR.ttf"], "zh-CN": ["Noto-Sans-SC_wght-700.ttf", "Noto-Sans-SC_wght-900.ttf"]}
BRAND = "Borderline Empire 0123456789 /"
missing, chars = [], set(BRAND)
for locale, words in copy.items():
    text = json.dumps(words, ensure_ascii=False).replace("\\n", " ")
    chars |= set(text)
    for c in sorted(set(text)):
        if c.isspace() or ord(c) < 32:
            continue
        lead = LEAD.get(locale)
        # CJK locales: every face the lead family uses must have it (zh: both weights), or Latin Noto Sans (digits, names)
        ok = (lead is not None and all(ord(c) in cmap[f] for f in lead)) or ord(c) in cmap["NotoSans.ttf"]
        if not ok:
            missing.append(f"{locale}:{c} U+{ord(c):04X}")
for c in BRAND:
    if not c.isspace() and ord(c) not in cmap["NotoSans.ttf"]:
        missing.append(f"brand:{c}")
result = {"status": "PASS" if not missing else "FAIL", "copy_sha256": hashlib.sha256(copy_file.read_bytes()).hexdigest(),
          "fonts": {n: pins[n] for n in FILES}, "missing": missing, "covered_characters": len(chars),
          "checked_locales": list(copy)}
if "--out" in sys.argv:
    out = Path(sys.argv[sys.argv.index("--out") + 1])
    out.mkdir(parents=True, exist_ok=True)
    (out / "font-coverage.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
if missing:
    print("STORE_GLYPHS_FAIL", missing[:30])
    sys.exit(1)
print("STORE_GLYPHS_PASS", result["covered_characters"], "characters")
