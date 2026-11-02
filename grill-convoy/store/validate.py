#!/usr/bin/env python3
"""Offline checks for the Grill Convoy: Burger Truck Manager (grill-convoy) Play listing + screenshot copy. No store / API.

  python3 grill-convoy/store/validate.py      # exit 0 = STORE_LISTING_PASS (unity/Tools/check.sh runs it)

Checks: en-US is present (the other 9 locales are translated centrally later; any file that exists must be one of the 10),
Play limits (chars AND UTF-16 units), title = keyword first + ": Grill Convoy" brand last, no trial / free / price / ranking
wording, no real burger chain, reference / competitor game or scheme names (plan BURGER_GAME_PLAN.md originality rule + §11),
the four ad-removal products named exactly as iap.json (CLAUDE.md §8), privacy policy URL once and last, screenshot plan
= 8 shots with the same ids in every language + length limits, Data safety CSV present.
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
GAME = ROOT.parent
LOCALES = {"en-US": "en", "tr-TR": "tr", "de-DE": "de", "es-ES": "es", "fr-FR": "fr", "pt-BR": "pt", "ru-RU": "ru",
           "ja-JP": "ja", "ko-KR": "ko", "hi-IN": "hi"}
REQUIRED = {"en-US"}
LIMITS = {"title": 30, "shortDescription": 80, "fullDescription": 4000}
BRAND = ": Grill Convoy"  # owner ASO rule: keyword first, brand last
CJK = {"ja", "ko"}
FORBIDDEN = re.compile(
    r"\btrial\b|\bfree\b|try\s+free|\bgift\b|\$\s?\d|\d\s?\$|\bprice\b|\bpricing\b|\bdiscount\b|\bsale\s+price\b|"
    r"ücretsiz|bedava|\bdeneme\b|kostenlos\w*|gratis|gratuit\w*|testversion|probeabo|prueba\s+gratuita|essai|"
    r"grátis|teste\s+gr[aá]tis|avaliação\s+gratuita|бесплатн\w*|пробн\w*|"
    r"無料|体験版|お試し|무료|체험|मुफ़्त|मुफ्त|फ्री|ट्रायल|निःशुल्क", re.IGNORECASE)
# real chains / marks, the reference game + its developer, competitor titles named in the plan, ranking claims
BRANDS = re.compile(
    r"mc\s*donald'?s|burger\s*king|wendy'?s|five\s*guys|shake\s*shack|in-n-out|whopper|big\s*mac|happy\s*meal|"
    r"carl'?s\s*jr|hardee'?s|jack\s+in\s+the\s+box|sonic\s+drive|white\s*castle|smashburger|fatburger|culver'?s|"
    r"kfc|subway|domino'?s|pizza\s*hut|taco\s*bell|"
    r"マクドナルド|バーガーキング|맥도날드|버거킹|मैकडॉनल्ड|बर्गर\s*किंग|макдоналдс|бургер\s*кинг|"
    r"codigames|idle\s+burger\s+empire|burger\s+empire\s+tycoon|cooking\s+fever|food\s+truck\s+chef|"
    r"burger\s+truck\s+chicago|burger\s+rush|patty\s+rush|good\s+pizza|overcooked|diner\s+dash|"
    r"#\s*1\b|number\s+one|\bno\.\s*1\b|best\s+game|top\s+game|1位|최고의|최고|最高", re.IGNORECASE)
PRIVACY = "https://www.coddykit.com/pages/privacy-policy"
AD_FREE = ("noads", "premium", "convoy_pass", "bundle")
SHOT_KEYS = {"id", "headline", "sub", "scene"}

catalog = json.loads((GAME / "unity/Assets/Resources/data/iap.json").read_text(encoding="utf-8"))
products = catalog["products"]
fails = []


def check(cond, *msg):
    if not cond:
        fails.append(" ".join(str(m) for m in msg))


def units(s):
    return len(s.encode("utf-16-le")) // 2


check(sorted(catalog.get("ad_free_products", [])) == sorted(AD_FREE), "iap.json ad_free_products differ from", AD_FREE)
files = {p.stem: p for p in (ROOT / "listing").glob("*.json")}
check(REQUIRED <= set(files), "listing missing:", sorted(REQUIRED - set(files)))
check(set(files) <= set(LOCALES), "unknown listing locale:", sorted(set(files) - set(LOCALES)))
print("locale | title | title / short / full (chars, UTF-16)")
for loc in sorted(files):
    lang = LOCALES.get(loc, "en")
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
        check(not m, loc, key, "brand / reference game / ranking:", m and m.group(0))
        check("�" not in v and not any(ord(c) < 32 and c != "\n" for c in v), loc, key, "bad character")
        counts.append(f"{len(v)}/{units(v)}")
    check(doc.get("title", "").endswith(BRAND), loc, "title must end with", BRAND)
    full = doc.get("fullDescription", "")
    for pid in AD_FREE:
        name = products[pid]["title"][lang]
        check(name in full, loc, pid, "ad-removal product name missing:", name)
    check(full.count(PRIVACY) == 1, loc, "privacy policy URL must appear once")
    check(full.rstrip().endswith(PRIVACY), loc, "the privacy policy URL must be the last line")
    check(len(re.findall(r"^[A-Z0-9][A-Z0-9 ,&'-]{3,}$", full, re.M)) >= 3 or lang != "en", loc, "needs >= 3 CAPS section headers")
    print(loc, doc.get("title"), *counts, sep=" | ")

shots_dir = ROOT / "screenshots"
en = json.loads((shots_dir / "en.json").read_text(encoding="utf-8"))
ids = [s["id"] for s in en["shots"]]
check(len(ids) == 8 and len(set(ids)) == 8, "screenshot plan must have 8 distinct shots")
for p in sorted(shots_dir.glob("*.json")):
    lang = p.stem
    check(lang in LOCALES.values(), p.name, "unknown screenshot language")
    d = json.loads(p.read_text(encoding="utf-8"))
    check([s["id"] for s in d["shots"]] == ids, lang, "shot ids differ from en")
    hl, sl, fl = (15, 25, 20) if lang in CJK else (30, 50, 40)
    for s in d["shots"]:
        check(set(s) == SHOT_KEYS, lang, s.get("id"), "keys must be", sorted(SHOT_KEYS))
        for k, lim in (("headline", hl), ("sub", sl)):
            v = s.get(k, "")
            check(v and len(v) <= lim, lang, s["id"], k, "length", len(v), "max", lim)
            check(not FORBIDDEN.search(v) and not BRANDS.search(v), lang, s["id"], k, "forbidden wording")
        check(s.get("scene") and not BRANDS.search(s["scene"]), lang, s["id"], "scene empty / names a brand")
    f = d.get("feature", "")
    check(f and len(f) <= fl and not FORBIDDEN.search(f) and not BRANDS.search(f), lang, "feature tagline", len(f), "max", fl)

csv = ROOT / "play-data-safety.csv"
check(csv.exists() and csv.read_text(encoding="utf-8").startswith("Question ID"), "play-data-safety.csv missing / not the Play format")

if fails:
    print("\n".join("FAIL " + f for f in fails))
    print(f"STORE_LISTING_FAIL ({len(fails)})")
    sys.exit(1)
print("STORE_LISTING_PASS")
