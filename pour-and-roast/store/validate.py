#!/usr/bin/env python3
"""Offline checks for the Cafe Manager: Pour & Roast (pour-and-roast) Play listing + screenshot copy. No store / API access.

  python3 pour-and-roast/store/validate.py          # exit 0 = STORE_LISTING_PASS (the locales present now; en-US required)
  python3 pour-and-roast/store/validate.py --all    # release gate: all 10 locales must be present

Checks: known locales only (en-US now; the other 9 are translated centrally later and are checked the same way once they
exist), Play limits (chars AND UTF-16 units), title ends with the brand (keyword first), no trial / free / price / ranking
wording, no real coffee chains / competitor or reference-game names / the reference's signature words (plan §1, §12, §13
risk 6), the four ad-removal products named exactly as iap.json (CLAUDE.md §3/§8), the privacy policy URL exactly once,
screenshot copy = 8 shots with the same ids as en and length limits, feature-graphic tagline length.
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
GAME = ROOT.parent
LOCALES = {"en-US": "en", "tr-TR": "tr", "de-DE": "de", "es-ES": "es", "fr-FR": "fr", "pt-BR": "pt", "ru-RU": "ru",
           "ja-JP": "ja", "ko-KR": "ko", "hi-IN": "hi"}
LIMITS = {"title": 30, "shortDescription": 80, "fullDescription": 4000}
BRAND = ": Pour & Roast"  # owner ASO rule: keyword first, brand after the colon
CJK = {"ja", "ko"}
FORBIDDEN = re.compile(
    r"\btrial\b|\bfree\b|try\s+free|\bgift\b|\$\s?\d|\bprice\s*:|\bpricing\b|\bsale\b|\bdiscount\b|"
    r"ücretsiz|bedava|\bdeneme\b|kostenlos\w*|gratis|gratuit\w*|testversion|probeabo|prueba\s+gratuita|essai\s+gratuit|"
    r"grátis|teste\s+gr[aá]tis|avaliação\s+gratuita|бесплатн\w*|пробн\w*|"
    r"無料|体験版|お試し|무료|체험판|मुफ़्त|मुफ्त|फ्री|ट्रायल|निःशुल्क", re.IGNORECASE)
# real coffee chains / roasters, the reference game and its publisher, the market competitors named in the plan, the
# reference's signature mechanics (orchard / garden / event cafe / oven task cards) and sibling-game words (plan §12:
# no "rush / tapri / stall" copy), ranking claims.
BRANDS = re.compile(
    r"starbucks|costa\s+coffee|tim\s+hortons|dunkin|nescaf[eé]|nespresso|lavazza|\billy\b|peet'?s|blue\s+bottle|"
    r"caribou\s+coffee|gloria\s+jean|second\s+cup|pret\s+a\s+manger|tchibo|juan\s+valdez|caf[eé]\s+coffee\s+day|"
    r"kahve\s+d[üu]nyas[ıi]|segafredo|kimbo|jacobs|doutor|tully'?s|ediya|mega\s*coffee|\bpaik|"
    r"codigames|idle\s+coffee\s+shop|coffee\s+shop\s+(empire|tycoon\s*-\s*idle)|empire\s+tycoon|"
    r"coffee\s+break|good\s+coffee,?\s+great\s+coffee|my\s+coffee\s+shop|own\s+coffee\s+shop|coffee\s+craze|"
    r"coffee\s+shop\s+simulator|coffee\s+roaster\s+empire|supercent|tapblaze|\btapps\b|owngames|five\s*amp|"
    r"coffee\s+rush|rush\s+hour\s+caf|crema\s+co|\brush\b|\btapri\b|chaiwala|\bstall\b|"
    r"orchard|\bgarden\b|event\s+caf|oven\s+task|"
    r"#\s*1\b|number\s+one|\bno\.\s*1\b|best\s+game|top\s+rated|1位|최고의|最高", re.IGNORECASE)
URL = "https://www.coddykit.com/pages/privacy-policy"
AD_FREE = ("noads", "premium", "roast_pass", "bundle")

catalog = json.loads((GAME / "unity/Assets/Resources/data/iap.json").read_text(encoding="utf-8"))
products = catalog["products"]
fails = []


def check(cond, *msg):
    if not cond:
        fails.append(" ".join(str(m) for m in msg))


def units(s):
    return len(s.encode("utf-16-le")) // 2


check(sorted(catalog.get("ad_free_products", [])) == sorted(AD_FREE), "iap.json ad_free_products != validate.py AD_FREE",
      catalog.get("ad_free_products"))

require_all = "--all" in sys.argv[1:]
files = {p.stem: p for p in (ROOT / "listing").glob("*.json")}
check("en-US" in files, "listing/en-US.json missing")
check(not (set(files) - set(LOCALES)), "unknown listing locale(s):", sorted(set(files) - set(LOCALES)))
if require_all:
    check(set(files) == set(LOCALES), "--all: missing listing locale(s):", sorted(set(LOCALES) - set(files)))
print("locale | title | title / short / full (chars, UTF-16)")
for loc in sorted(files):
    lang = LOCALES.get(loc)
    if lang is None:
        continue
    doc = json.loads(files[loc].read_text(encoding="utf-8"))
    check(set(doc) == set(LIMITS), loc, "unexpected keys", sorted(doc))
    counts = []
    for key, lim in LIMITS.items():
        v = doc.get(key, "")
        check(isinstance(v, str) and v and v.strip() == v, loc, key, "empty / untrimmed")
        check(len(v) <= lim and units(v) <= lim, loc, key, "too long", len(v), units(v), lim)
        m = FORBIDDEN.search(v)
        check(not m, loc, key, "offer / price wording:", m and m.group(0))
        m = BRANDS.search(v)
        check(not m, loc, key, "brand / competitor / reference / ranking wording:", m and m.group(0))
        check("�" not in v and not any(ord(c) < 32 and c != "\n" for c in v), loc, key, "bad character")
        counts.append(f"{len(v)}/{units(v)}")
    check(doc.get("title", "").endswith(BRAND), loc, "title must end with", BRAND)
    full = doc.get("fullDescription", "")
    for pid in AD_FREE:
        name = products[pid]["title"][lang]
        check(name in full, loc, pid, "ad-removal product name missing:", name)
    check(full.count(URL) == 1, loc, URL, "must appear exactly once")
    check(full.rstrip().endswith(URL), loc, "the full description must end with the privacy policy URL")
    print(loc, doc.get("title"), *counts, sep=" | ")

en_path = ROOT / "screenshots/en.json"
check(en_path.exists(), en_path, "missing")
if en_path.exists():
    en = json.loads(en_path.read_text(encoding="utf-8"))
    ids = [s["id"] for s in en["shots"]]
    check(len(ids) == 8 and len(set(ids)) == 8, "screenshot plan must have 8 distinct shots")
    for s in en["shots"]:
        check(s.get("scene"), "en", s.get("id"), "scene (what the frame shows) missing")
    for lang in LOCALES.values():
        p = ROOT / "screenshots" / f"{lang}.json"
        if not p.exists():
            check(not require_all, p, "missing (--all)")
            continue
        d = json.loads(p.read_text(encoding="utf-8"))
        check([s["id"] for s in d["shots"]] == ids, lang, "shot ids differ from en")
        hl, sl, fl = (15, 25, 20) if lang in CJK else (30, 50, 40)
        for s in d["shots"]:
            for k, lim in (("headline", hl), ("sub", sl)):
                v = s.get(k, "")
                check(v and len(v) <= lim, lang, s["id"], k, "length", len(v), "max", lim)
                check(not FORBIDDEN.search(v) and not BRANDS.search(v), lang, s["id"], k, "forbidden wording:", v)
        f = d.get("feature", "")
        check(f and len(f) <= fl, lang, "feature tagline length", len(f), "max", fl)
        check(not FORBIDDEN.search(f) and not BRANDS.search(f), lang, "feature tagline forbidden wording")

for name in ("play-data-safety.csv",):
    check((ROOT / name).exists(), name, "missing")

if fails:
    print("\n".join("FAIL " + f for f in fails))
    print(f"STORE_LISTING_FAIL ({len(fails)})")
    sys.exit(1)
print("STORE_LISTING_PASS")
