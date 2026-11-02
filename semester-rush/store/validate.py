#!/usr/bin/env python3
"""Offline store checks for College Tycoon: Semester Rush (semester-rush) — Play listing + screenshot copy. No store / API
access. Called by unity/Tools/check.sh.

  python3 semester-rush/store/validate.py      # exit 0 = STORE_LISTING_PASS

Checks: en-US listing + en screenshot plan present (the other 9 locales are translated centrally later and are accepted
and checked the same way as soon as they exist), Play limits (chars AND UTF-16 units), title = keyword first, brand last
(": Semester Rush"), no trial / free / price / ranking wording, no real universities, schemes, brands, competitor or
reference-game names (plan section 3 originality list), the ad-removal products named exactly as iap.json
(ad_free_products, root CLAUDE.md section 8), the privacy policy URL once at the end, screenshot copy = 8 shots with the
same ids as en and length limits, the Data safety CSV and the store art files present with the right sizes.
"""
import csv
import json
import re
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
GAME = ROOT.parent
LOCALES = {"en-US": "en", "tr-TR": "tr", "de-DE": "de", "es-ES": "es", "fr-FR": "fr", "pt-BR": "pt", "ru-RU": "ru",
           "ja-JP": "ja", "ko-KR": "ko", "hi-IN": "hi"}
REQUIRED = {"en-US"}  # the 9 others arrive from the central translation pass
LIMITS = {"title": 30, "shortDescription": 80, "fullDescription": 4000}
BRAND = ": Semester Rush"  # owner ASO rule: keyword first, brand last
CJK = {"ja", "ko"}
FORBIDDEN = re.compile(
    r"\btrial\b|\bfree\b|try\s+free|\bgift\b|\$\s?\d|\d\s?\$|€\s?\d|\bprice\b|\bpricing\b|\bdiscount|\bsale\b|% off|"
    r"ücretsiz|bedava|\bdeneme\b|kostenlos\w*|gratis|gratuit\w*|testversion|probeabo|prueba\s+gratuita|essai|"
    r"grátis|gratuit[oa]|teste\s+gr[aá]tis|avaliação\s+gratuita|бесплатн\w*|пробн\w*|"
    r"無料|体験版|お試し|무료|체험|मुफ़्त|मुफ्त|फ्री|ट्रायल|निःशुल्क", re.IGNORECASE)
# reference game / competitors (plan sections 1 + 3), real universities / schemes / brands, store-ranking claims
BRANDS = re.compile(
    r"codigames|university\s+empire|empire\s+tycoon|two[\s-]*point|idle\s+university|university\s+idle|"
    r"harvard|oxford|cambridge|stanford|\byale\b|princeton|sorbonne|berkeley|columbia\s+university|ivy\s+league|"
    r"oxbridge|heidelberg|tsinghua|peking\s+university|todai|東京大学|東大|서울대|seoul\s+national|boğaziçi|bogazici|"
    r"\bodtü\b|\bmetu\b|hogwarts|erasmus|fulbright|pell\s+grant|fafsa|rhodes\s+scholar|"
    r"#\s*1\b|number\s+one|\bno\.\s*1\b|best\s+game|top[\s-]rated|\btop\s+\d+|editor'?s\s+choice|"
    r"1位|최고의|최고|最高", re.IGNORECASE)
ACRONYMS = re.compile(r"\b(MIT|UCLA|NYU|LSE|ETH|IIT|EPFL|KAIST|CALTECH|Caltech)\b")  # case-sensitive (German "mit")
PRIVACY = "https://www.coddykit.com/pages/privacy-policy"

iap = json.loads((GAME / "unity/Assets/Resources/data/iap.json").read_text(encoding="utf-8"))
catalog, ad_free = iap["products"], iap["ad_free_products"]
fails = []


def check(cond, *msg):
    if not cond:
        fails.append(" ".join(str(m) for m in msg))


def units(s):
    return len(s.encode("utf-16-le")) // 2


def clean(where, v):
    m = FORBIDDEN.search(v)
    check(not m, where, "offer / price wording:", m and m.group(0))
    m = BRANDS.search(v) or ACRONYMS.search(v)
    check(not m, where, "brand / reference / ranking name:", m and m.group(0))
    check("�" not in v and not any(ord(c) < 32 and c != "\n" for c in v), where, "bad character")


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
        clean(loc + " " + key, v)
        counts.append(f"{len(v)}/{units(v)}")
    title = doc.get("title", "")
    check(title.endswith(BRAND) and len(title) > len(BRAND), loc, "title must be '<keyword>" + BRAND + "'")
    full = doc.get("fullDescription", "")
    for pid in ad_free:
        name = catalog[pid]["title"].get(lang, catalog[pid]["title"]["en"])
        check(name in full, loc, pid, "ad-removal product name missing:", name)
    check(full.count(PRIVACY) == 1 and full.rstrip().endswith(PRIVACY), loc, "privacy URL must appear once, at the end")
    if loc == "en-US":
        for h in ("PLAN THE WEEK", "BEAT THE BELL", "HANDLE THE CHAOS", "GROW YOUR CAMPUS", "GOOD TO KNOW"):
            check("\n" + h + "\n" in full, loc, "section header missing:", h)
        check("Other purchases do not remove ads" in full, loc, "the cheap-packs line (root CLAUDE.md section 3)")
    print(loc, title, *counts, sep=" | ")

shots_dir = ROOT / "screenshots"
en_path = shots_dir / "en.json"
check(en_path.exists(), en_path, "missing")
ids = []
if en_path.exists():
    ids = [s["id"] for s in json.loads(en_path.read_text(encoding="utf-8"))["shots"]]
    check(len(ids) == 8 and len(set(ids)) == 8, "screenshot plan must have 8 distinct shots")
for p in sorted(shots_dir.glob("*.json")):
    lang = p.stem
    check(lang in LOCALES.values(), p.name, "unknown language")
    d = json.loads(p.read_text(encoding="utf-8"))
    check([s.get("id") for s in d.get("shots", [])] == ids, lang, "shot ids differ from en")
    hl, sl, fl = (15, 25, 20) if lang in CJK else (30, 50, 40)
    for s in d.get("shots", []):
        for k, lim in (("headline", hl), ("sub", sl)):
            v = s.get(k, "")
            check(v and len(v) <= lim, lang, s.get("id"), k, "length", len(v), "max", lim)
            clean(f"{lang} {s.get('id')} {k}", v)
        check(lang != "en" or s.get("scene"), lang, s.get("id"), "scene description missing")
    f = d.get("feature", "")
    check(f and len(f) <= fl, lang, "feature tagline length", len(f), "max", fl)
    clean(lang + " feature", f)

# Data safety (same SDK stack as the fleet template: AdMob rewarded, RevenueCat, Firebase Analytics; not a kids game)
ds = ROOT / "play-data-safety.csv"
check(ds.exists(), ds, "missing")
if ds.exists():
    rows = list(csv.reader(ds.open(encoding="utf-8")))
    check(rows and rows[0][0] == "Question ID (machine readable)" and len(rows) > 700, ds.name, "not the Play Data safety export")


def png_size(p):
    b = p.read_bytes()[:24]
    return struct.unpack(">II", b[16:24]) if b[:8] == b"\x89PNG\r\n\x1a\n" else None


for rel, size in [("feature-graphic.png", (1024, 500))] + [(f"icon-candidates/c{i}.png", (512, 512)) for i in (1, 2, 3)]:
    p = ROOT / rel
    check(p.exists() and png_size(p) == size, rel, "missing or not", size, png_size(p) if p.exists() else "")

if fails:
    print("\n".join("FAIL " + f for f in fails))
    print(f"STORE_LISTING_FAIL ({len(fails)})")
    sys.exit(1)
print("STORE_LISTING_PASS")
