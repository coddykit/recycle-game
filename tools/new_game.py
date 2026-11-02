#!/usr/bin/env python3
"""Clones bottle-route/ (the reference Unity game) into a new game folder with every identifier renamed, so a new game
starts from a project that already has the fleet infrastructure (CoddyKit, shop, rewards, saves, guide frame, gates,
editor setup, check scripts). Game-specific art is NOT copied (Meshy characters, kit props, UI renders, icon): the new
game makes its own. Everything domain-specific (Sim, data, Coach steps, strings, World props, Sheets content, IAP table)
is still Bottle Route's and must be replaced — see GAME_TEMPLATE.md.
  python3 tools/new_game.py <slug> <Namespace> <Prefix> "<Product Name>"
  e.g. python3 tools/new_game.py grill-convoy GrillConvoy Grill "Grill Convoy"
"""
import os, re, shutil, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "bottle-route")
SKIP_DIRS = {"Library", "Temp", "Logs", "obj", "Build", "UserSettings", "Meshy", "Icon", "__pycache__"}
SKIP_FILES = re.compile(r"(Resources/kit/.*|Resources/ui/(coach_operator|intro_\d|curtain)\.png|docs/(ART|BALANCE|MONETIZATION_PARITY)\.md)$")
TEXT = {".cs", ".asmdef", ".json", ".md", ".py", ".sh", ".uss", ".asset", ".txt", ".xml", ".csv", ".gitignore", ".tss"}


def main():
    slug, ns, prefix, product = sys.argv[1:5]
    pkg = slug.replace("-", "")
    upper = pkg.upper()
    dst = os.path.join(ROOT, slug)
    if os.path.exists(dst):
        sys.exit("exists: " + dst)
    subs = [
        ("com.coddykit.bottleroute", "com.coddykit." + pkg), ("BottleRoute", ns), ("BOTTLEROUTE", upper), ("bottle-route", slug),
        ("bottleroute", pkg), ("Bottle Route", product), ("BottlePanelText", prefix + "PanelText"),
    ]
    ident = re.compile(r"\bBottle(?=[A-Z])")   # BottleMain -> <Prefix>Main, BottleRewards -> <Prefix>Rewards
    n = 0
    for dirpath, dirnames, filenames in os.walk(SRC):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        rel_dir = os.path.relpath(dirpath, SRC)
        for f in filenames:
            rel = os.path.normpath(os.path.join(rel_dir, f))
            if SKIP_FILES.search(rel.replace(os.sep, "/")) or f.endswith(".meta") and "CoddyKit" not in rel and "Settings" not in rel and "GoogleMobileAds" not in rel and "fonts" not in rel:
                continue
            out_rel = rel
            for a, b in subs:
                out_rel = out_rel.replace(a, b)
            out_rel = ident.sub(prefix, out_rel)
            src, out = os.path.join(dirpath, f), os.path.join(dst, out_rel)
            os.makedirs(os.path.dirname(out), exist_ok=True)
            ext = os.path.splitext(f)[1] or f
            if ext in TEXT and "CoddyKit" not in rel and "/fonts/" not in rel.replace(os.sep, "/"):
                s = open(src, encoding="utf-8").read()
                for a, b in subs:
                    s = s.replace(a, b)
                if ext == ".cs":
                    s = ident.sub(prefix, s)
                open(out, "w", encoding="utf-8").write(s)
            else:
                shutil.copy2(src, out)
            n += 1
    os.makedirs(os.path.join(dst, "unity/Assets/Resources/kit"), exist_ok=True)
    print("cloned", n, "files ->", dst)


main()
