"""Collects user-facing strings into translations/strings.csv (keys = English).

Existing translations are preserved; new keys get an empty column so they are
easy to spot. Godot imports the CSV as one .translation file per language.
Run: python tools/i18n/extract_strings.py
"""
import csv
import glob
import json
import os
import re

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
CSV = os.path.join(ROOT, "translations", "strings.csv")
LANGS = ["en", "tr", "de", "es", "fr", "ja", "ko", "pt", "ru", "zh"]

TEXT_KEYS = {"name", "tagline", "text", "label", "desc", "title", "blurb", "hint", "subtitle"}
LIST_KEYS = {"levels", "level_desc", "perks"}
SKIP_FILES = {"model_meta.json", "drivers.json"}


def from_json():
    out = []
    for path in sorted(glob.glob(os.path.join(ROOT, "data", "*.json"))):
        if os.path.basename(path) in SKIP_FILES:
            continue
        data = json.load(open(path))

        def walk(node, parent_key=""):
            if isinstance(node, dict):
                for k, v in node.items():
                    if k == "towns":
                        continue
                    if isinstance(v, str) and (k in TEXT_KEYS or parent_key in ("tab_unlocked",)):
                        out.append(v)
                    elif isinstance(v, str) and k == "hint" :
                        out.append(v)
                    elif isinstance(v, list) and k in LIST_KEYS:
                        out.extend(x for x in v if isinstance(x, str))
                    elif isinstance(v, list) and k == "tags":
                        out.extend(v)
                    elif k in ("requires", "kind") and isinstance(v, str) and v:
                        out.append(v)
                    else:
                        walk(v, k)
            elif isinstance(node, list):
                for x in node:
                    walk(x, parent_key)
        walk(data)
    # rarity names
    d = json.load(open(os.path.join(ROOT, "data", "drivers.json")))
    out.extend(r["name"] for r in d.get("rarities", []))
    return out


LIT = r'"((?:[^"\\\n]|\\.)+)"'
CALLS = [
    r'\btr\(' + LIT,
    r'TranslationServer\.translate\(' + LIT,
    r'UIKit\.(?:label|button|section|pill)\(' + LIT,
    r'UIKit\.stat_chip\([^,]+,\s*' + LIT,
    r'\btoast\(' + LIT,
    r'(?:show_)?modal\(' + LIT + r'(?:,\s*' + LIT + r')?',
    r'set_title\(' + LIT,
    r'^\s*title = ' + LIT,
    r'"reason":\s*' + LIT,
    r'_row\([^,]+,\s*' + LIT,
    r'_stat\([^,]+,\s*' + LIT,
    r'_metric_name|"(?:vehicles|garage|deliveries|regions|automation|depots)":\s*' + LIT,
    r'return \[' + LIT,
    r'\[\s*"[a-z_]+",\s*' + LIT + r',\s*"[a-z_]+"\s*\]',
    r'\["(?:low|medium|high|en|tr|auto)",\s*' + LIT + r'\]',
    r'\[\[' + LIT + r',\s*"Btn',
    r'\[' + LIT + r',\s*"Btn',
    r'set_tabs\(\[([^\]]+)\]',
    r'"(?:speed|reliability|stress)",\s*' + LIT,
    r'"text":\s*' + LIT,
]

# any capitalised literal in UI / gameplay-message code (extras are harmless)
GENERIC_FILES = ("scripts/ui/", "scripts/core/event_manager.gd", "scripts/contracts/contract_manager.gd",
                 "scripts/monetization/monetization.gd", "scripts/world/world.gd", "scripts/world/region_view.gd", "scripts/progression/tutorial_manager.gd")
GENERIC_SKIP = re.compile(r"^(Btn|Card|Pill|Sheet|Modal|UI$|World$|CameraRig|Traffic$|HQ$|LockIcon|Body$|Wheel|MM_|Region_|"
                          r"JavaObject$|Label$|Button$|PanelContainer|ProgressBar|VBoxContainer|HBoxContainer|VScrollBar|HScrollBar|Thumbnail|Needs $|"
                          r"Vehicle_|Torso|Arm_|Leg_|DOT$|NEW$|R$|L$|"
                          r"PrimaryButton|SecondaryButton|TonalButton|DangerButton|IconButton|TabButton|SegmentButton|RowButton|GhostButton|"
                          r"Glass$|Title$|Headline$|Display$|Caption|BodyMuted|BodyLarge|StatBar|Value$|SettingsButton|SpeedButton|Overline|LabelStrong|"
                          r"Accent$|Text$|NavBar|LineEdit|TextEdit|Brand$|Quality_|Group$|Chip$|Muted$|Step n of N|"
                          # 2026-10-26 kit redesign: node names / paths and theme variations of the kit list card + page header
                          r"Col/|Divider$|Tile$|ListCard|OutlineButton$|Tabs$|Tab_$|Facts$|Note$|Blocked$|Skills$|Grabber$|Row$)")


def from_code():
    out = []
    for path in glob.glob(os.path.join(ROOT, "scripts", "**", "*.gd"), recursive=True):
        if "debug_tools" in path or "/monetization/ads/" in path:   # kit ad cards: own 10-language text (data/house_ad_text.json)
            continue
        src = open(path).read()
        rel = os.path.relpath(path, ROOT)
        if rel.startswith(GENERIC_FILES):
            for m in re.finditer(r'"([A-Z][^"\n]{1,})"', src):
                g = m.group(1)
                if not GENERIC_SKIP.match(g) and not re.fullmatch(r"[0-9A-F]{6}", g):
                    out.append(g)
        for pat in CALLS:
            for m in re.finditer(pat, src, re.M):
                for g in m.groups():
                    if not g:
                        continue
                    if pat.startswith("set_tabs"):
                        out.extend(re.findall(LIT, g))
                    else:
                        out.append(g)
    # nav labels and other literal tables
    hud = open(os.path.join(ROOT, "scripts", "ui", "hud.gd")).read()
    out.extend(re.findall(r'\["[a-z]+", "([A-Z][a-z]+)", "[a-z_]+"\]', hud))
    return out


def clean(keys):
    seen = []
    for k in keys:
        k = k.replace('\\n', '\n')
        if not re.search(r"[A-Za-z]{2,}", re.sub(r"%[-+0-9.]*[sdf%]", "", k)):
            continue
        if re.fullmatch(r"[a-z_0-9]+", k) and k not in ("cargo", "passenger", "armored", "available", "vehicle",
                                                          "offer", "driver", "units", "pax"):
            continue
        if k.startswith(("Btn", "res://", "Card", "Pill")):
            continue
        if k not in seen:
            seen.append(k)
    return seen


def main():
    keys = clean(from_json() + from_code() + EXTRA)
    existing = {}
    if os.path.exists(CSV):
        with open(CSV, newline="", encoding="utf-8") as f:
            for row in csv.DictReader(f):
                existing[row["keys"]] = row
    os.makedirs(os.path.dirname(CSV), exist_ok=True)
    with open(CSV, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["keys"] + LANGS)
        for k in keys:
            row = existing.get(k, {})
            w.writerow([k, k] + [row.get(l, "") for l in LANGS[1:]])
    missing = sum(1 for k in keys if not existing.get(k, {}).get("tr"))
    print("%d keys, %d without Turkish" % (len(keys), missing))


# strings shown through other paths (Label auto-translation of plain texts)
EXTRA = ["Contracts", "Fleet", "Map", "Upgrades", "Business", "Vehicles", "Showroom", "Drivers",
         "vehicle", "offer", "driver", "units", "pax", "Speed", "Reliab.", "Nerves", "available",
         "Low", "Medium", "High", "Extreme", "Cold", "Warm", "Hot", "Critical", "Graphics", "Language", "Auto",
         # loading-curtain tips (scripts/boot.gd Curtain.TIPS)
         "Accept easy contracts first: low risk means steady cash.",
         "Highways are fast but risky; back roads are slow but safe.",
         "Heat adds risk but also better pay. Spread your work across regions.",
         "Upgrade the engine to shorten trips, then the garage for more vehicles.",
         "Good drivers cut risk. Hire the best you can afford.",
         "Automation keeps the empire working while you are away."]

if __name__ == "__main__":
    main()
