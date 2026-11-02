#!/usr/bin/env python3
"""Borderline Empire circuit store gate (offline; port of games/driftwood/tools/store/validate.py --final).
  python3 store/circuit/validate.py              slide copy: locales, two-line headlines, length budgets, no trial / free /
                                                 best / #1 / price (lint.py)                      -> STORE_COPY_PASS
  python3 store/circuit/validate.py --final DIR  also the rendered set: <DIR>/<locale>/{phone,tablet-7,tablet-10}/NN-<id>.png
                                                 (1080x1920, 1200x1920, 1600x2560), <DIR>/<locale>/feature.png (1024x500) when
                                                 present, the raw frames <DIR>/raw/<locale>/{phone,tablet}/NN-<id>.png
                                                 (1080x2340, 1200x1920), the state of every frame (raw/scenes.json) and the
                                                 locale list = the live Play listing (<DIR>/play-listing-read.json)  -> STORE_ASSETS_PASS"""
import json
import struct
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import lint  # noqa: E402

LOCALES = ["en-US", "tr-TR", "de-DE", "es-ES", "fr-FR", "ja-JP", "ko-KR", "pt-BR", "ru-RU", "zh-CN"]
FINAL_KINDS = {"phone": (1080, 1920), "tablet-7": (1200, 1920), "tablet-10": (1600, 2560)}
RAW_KINDS = {"phone": (1080, 2340), "tablet": (1200, 1920)}
PANEL = {"02-dispatch": "dispatch", "04-fleet": "fleet", "05-upgrades": "upgrades", "07-contracts": "contracts"}
fails: list = []


def check(ok, msg):
    if not ok:
        fails.append(msg)


def png_size(p):
    b = Path(p).read_bytes()[:26]
    if b[:8] != b"\x89PNG\r\n\x1a\n":
        return (0, 0), -1
    return struct.unpack(">II", b[16:24]), b[25]


def copy_gate():
    copy = json.loads((HERE / "copy.json").read_text(encoding="utf-8"))
    check(list(copy) == LOCALES, f"copy.json locales {list(copy)}")
    for loc, words in copy.items():
        errs = lint.copy_problems(words, loc)
        check(not errs, f"copy {loc}: {errs}")


def finals(final: Path):
    live = json.loads((final / "play-listing-read.json").read_text())
    check(sorted(live["locales"]) == sorted(LOCALES), f"live listing locales {live['locales']} != {LOCALES}")
    feature = any((final / loc / "feature.png").exists() for loc in LOCALES)
    for loc in LOCALES:
        for s in lint.SHOT_IDS:
            for kind, size in FINAL_KINDS.items():
                p = final / loc / kind / f"{s}.png"
                got = png_size(p) if p.exists() else ((0, 0), -1)
                check(got[0] == size and got[1] == 2, f"{p} {size[0]}x{size[1]} RGB (got {got})")
            for kind, size in RAW_KINDS.items():
                r = final / "raw" / loc / kind / f"{s}.png"
                check(r.exists() and png_size(r)[0] == size, f"{r} {size[0]}x{size[1]}")
        if feature:
            f = final / loc / "feature.png"
            check(f.exists() and png_size(f) == ((1024, 500), 2), f"{f} 1024x500 RGB")
    recs = json.loads((final / "raw/scenes.json").read_text())["shots"]
    check(len(recs) == len(LOCALES) * len(lint.SHOT_IDS) * len(RAW_KINDS), f"capture records {len(recs)}")
    for r in recs:
        tag = f"{r['locale']} {r['device']} {r['shot']}"
        check("banner_inset" in r, f"no frame state {tag}")
        if "banner_inset" not in r:
            continue
        check(r["banner_inset"] == 0.0 and r["capture_banner_installed"] and r["capture_banner_verified"], f"house banner not held off {tag}")
        check(not r["toast_visible"] and not r["tutorial_active"], f"toast / guide on screen {tag}")
        check(r["modal_open"] == (r["shot"] == "03-event"), f"modal state {tag}")
        check(r["store_sells"] and not r["purchased_any"] and not r["ad_free"], f"store state (nothing owned) {tag}")
        check(r["quality"] == "high", f"quality {tag}")
        check(r["language"].split("_")[0] == r["locale"].split("-")[0], f"language {r['language']} {tag}")
        check(r["panel"] == PANEL.get(r["shot"], ""), f"panel {r['panel']} {tag}")
        check(r["regions"] >= 3 and r["vehicles"] >= 3 and r["garage"] >= 3, f"mid-game state {tag}")
        if r["shot"] == "08-frost":
            check(r["regions"] == 8 and r["garage"] == 5, f"late game: 8 regions + Underground HQ {tag}")
        w, h = RAW_KINDS[r["device"]]
        check(abs(r["viewport"][0] / r["viewport"][1] - w / h) < 0.01, f"viewport aspect {tag}")


def main():
    copy_gate()
    final = None
    if "--final" in sys.argv:
        final = Path(sys.argv[sys.argv.index("--final") + 1])
        finals(final)
    if fails:
        print("STORE_FAIL", len(fails))
        for f in fails[:40]:
            print("  ", f)
        sys.exit(1)
    print("STORE_ASSETS_PASS" if final else "STORE_COPY_PASS")


main()
