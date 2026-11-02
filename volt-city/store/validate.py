#!/usr/bin/env python3
"""Offline P6 checks for the Grid Keeper: Energy Tycoon (games/volt-city) Play listing + screenshot copy. No store / API access.

  python3 games/volt-city/store/validate.py      # exit 0 = STORE_LISTING_PASS

Checks: 10 locales, Play limits (chars AND UTF-16 units), title ends with the brand (keyword first), no trial / free / price / ranking
wording, no real utility / brand / board-game / competitor names, no old codename "Volt City", the four ad-removal products
named exactly as iap.json (games/CLAUDE.md §8), both policy URLs once, screenshot copy = same ids as en with length limits.
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
BRAND = ": Grid Keeper"  # owner ASO rule: keyword first, brand last
CJK = {"ja", "ko"}
FORBIDDEN = re.compile(
    r"\btrial\b|\bfree\b|try\s+free|\bgift\b|\$\s?\d|\bprice\b|\bpricing\b|"
    r"ücretsiz|bedava|\bdeneme\b|kostenlos\w*|gratis|gratuit\w*|testversion|probeabo|prueba\s+gratuita|essai|"
    r"grátis|gratuit[oa]|teste\s+gr[aá]tis|avaliação\s+gratuita|бесплатн\w*|пробн\w*|"
    r"無料|体験版|お試し|무료|체험|मुफ़्त|मुफ्त|फ्री|ट्रायल|निःशुल्क", re.IGNORECASE)
BRANDS = re.compile(
    r"volt\s*city|ボルトシティ|볼트\s*시티|वोल्ट\s*सिटी|wattville|power\s*grid\s*(board|the\s+board)|funkenschlag|"
    r"brettspielwelt|rio\s+grande|energy\s+manager\s*-|national\s+grid|con\s*ed\b|edison|duke\s+energy|\bedf\b|"
    r"iberdrola|\benel\b|engie|vattenfall|\brwe\b|tepco|東京電力|한국전력|\bercot\b|oncor|city\s*power|simcity|"
    r"#\s*1\b|number\s+one|\bno\.\s*1\b|best\s+game|1位|최고의|최고|最高", re.IGNORECASE)
URLS = ("https://www.coddykit.com/pages/privacy-policy", "https://www.coddykit.com/pages/terms-of-service")
AD_FREE = ("noads", "premium", "grid_pass", "bundle")

catalog = json.loads((GAME / "unity/Assets/Resources/data/iap.json").read_text(encoding="utf-8"))["products"]
fails = []


def check(cond, *msg):
    if not cond:
        fails.append(" ".join(str(m) for m in msg))


def units(s):
    return len(s.encode("utf-16-le")) // 2


files = {p.stem: p for p in (ROOT / "listing").glob("*.json")}
check(set(files) == set(LOCALES), "listing locale mismatch:", sorted(set(files) ^ set(LOCALES)))
print("locale | title | title / short / full (chars, UTF-16)")
for loc in sorted(files):
    lang = LOCALES.get(loc)
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
        check(not m, loc, key, "brand / ranking / codename:", m and m.group(0))
        check("Power Grid" not in v, loc, key, 'capitalised "Power Grid" (board-game / registered mark)')
        check("�" not in v and not any(ord(c) < 32 and c != "\n" for c in v), loc, key, "bad character")
        counts.append(f"{len(v)}/{units(v)}")
    check(doc.get("title", "").endswith(BRAND), loc, "title must end with", BRAND)
    full = doc.get("fullDescription", "")
    for pid in AD_FREE:
        name = catalog[pid]["title"][lang]
        check(name in full, loc, pid, "ad-removal product name missing:", name)
    for url in URLS:
        check(full.count(url) == 1, loc, url, "must appear once")
    print(loc, doc.get("title"), *counts, sep=" | ")

en = json.loads((ROOT / "screenshots/en.json").read_text(encoding="utf-8"))
ids = [s["id"] for s in en["shots"]]
check(len(ids) == 8, "screenshot plan must have 8 shots")
for lang in LOCALES.values():
    p = ROOT / "screenshots" / f"{lang}.json"
    if not p.exists():
        check(False, p, "missing")
        continue
    d = json.loads(p.read_text(encoding="utf-8"))
    check([s["id"] for s in d["shots"]] == ids, lang, "shot ids differ from en")
    hl, fl = (14, 16) if lang in CJK else (28, 32)
    for s in d["shots"]:
        for k in ("headline", "chip"):
            v = s.get(k, "")
            check(v and len(v) <= hl, lang, s["id"], k, "length", len(v), "max", hl)
            check(not FORBIDDEN.search(v) and not BRANDS.search(v), lang, s["id"], k, "forbidden wording")
    check(d.get("feature") and len(d["feature"]) <= fl, lang, "feature tagline length")

if fails:
    print("\n".join("FAIL " + f for f in fails))
    print(f"STORE_LISTING_FAIL ({len(fails)})")
    sys.exit(1)
print("STORE_LISTING_PASS")
