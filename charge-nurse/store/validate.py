#!/usr/bin/env python3
"""Offline store checks for Hospital Manager: Charge Nurse (charge-nurse/) — Play listing + screenshot copy. No store / API access.

  python3 charge-nurse/store/validate.py      # exit 0 = STORE_LISTING_PASS

Checks: en-US listing present (the other 9 locales come from the central translation run and are checked the same way when
they land: tr de es fr pt-BR ru ja ko hi), Play limits (chars AND UTF-16 units), title = keyword first + ": Charge Nurse",
no trial / free / price / ranking wording, no real hospital / medicine / brand / emblem / reference-game / competitor names
(plan §1-§3, §11, §13 originality list), no medical claims, nothing graphic, the ad-removal products named exactly as
iap.json `ad_free_products` (CLAUDE.md §8), privacy policy URL once and last, screenshot plan = 8 shots with length limits
(other languages: same ids), Data safety CSV = the fleet template's shape.
"""
import csv
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
GAME = ROOT.parent
LOCALES = {"en-US": "en", "tr-TR": "tr", "de-DE": "de", "es-ES": "es", "fr-FR": "fr", "pt-BR": "pt", "ru-RU": "ru",
           "ja-JP": "ja", "ko-KR": "ko", "hi-IN": "hi"}
REQUIRED = {"en-US"}  # now; every locale that exists is checked
LIMITS = {"title": 30, "shortDescription": 80, "fullDescription": 4000}
BRAND = ": Charge Nurse"  # owner ASO rule: keyword first, brand last
CJK = {"ja", "ko"}
PRIVACY = "https://www.coddykit.com/pages/privacy-policy"
FORBIDDEN = re.compile(
    r"\btrial\b|\bfree\b|\bfreely\b|try\s+free|\bgift\b|\$\s?\d|€\s?\d|\bprice\b|\bpricing\b|\bdiscount\b|"
    r"ücretsiz|bedava|\bdeneme\b|kostenlos\w*|gratis|gratuit\w*|testversion|probeabo|prueba\s+gratuita|essai|"
    r"grátis|teste\s+gr[aá]tis|avaliação\s+gratuita|бесплатн\w*|пробн\w*|"
    r"無料|体験版|お試し|무료|체험|मुफ़्त|मुफ्त|फ्री|ट्रायल|निःशुल्क", re.IGNORECASE)
# reference game + publisher, store competitors named in the plan, real hospitals / services / emblems, medicine brands,
# TV shows, ranking claims
BRANDS = re.compile(
    r"codigames|hospital\s+empire|empire\s+tycoon|\bidle\s+hospital|the\s+ward\b|er\s+hospital\s+drama|happy\s+clinic|"
    r"nordcurrent|pocket\s+hospital|operate\s+now|dream\s+hospital|hospital\s+rush|two\s+point|theme\s+hospital|"
    r"project\s+hospital|grey.?s\s+anatomy|\bhouse\s+m\.?d\b|\ber\s+tv|mayo\s+clinic|cleveland\s+clinic|"
    r"johns\s+hopkins|charit[ée]\b|\bnhs\b|red\s+cross|red\s+crescent|kızılay|croix.rouge|cruz\s+roja|rotes\s+kreuz|"
    r"tylenol|aspirin|ibuprofen|paracetamol|advil|panadol|pfizer|bayer|novartis|"
    r"#\s*1\b|number\s+one|\bno\.\s*1\b|best\s+game|1位|최고의|최고|最高", re.IGNORECASE)
# store text makes no medical claims and stays non-graphic (plan §5 tone, §10 health-ads note)
MEDICAL = re.compile(
    r"\bcures?\b|\bheal(s|ing)?\s+(you|real)|\bdiagnose\s+you|\breal\s+doctors?\b|medically\s+accurate|clinically|"
    r"\bblood\b|\bbleed\w*|\bgore\b|\bcorpse|\bdie\b|\bdies\b|\bdied\b|\bdeath\b|\bdead\b|\bsurgery\s+simulator\b", re.IGNORECASE)

catalog = json.loads((GAME / "unity/Assets/Resources/data/iap.json").read_text(encoding="utf-8"))
AD_FREE = [p for p in catalog["ad_free_products"] if p in catalog["products"]]
fails = []


def check(cond, *msg):
    if not cond:
        fails.append(" ".join(str(m) for m in msg))


def units(s):
    return len(s.encode("utf-16-le")) // 2


def text_checks(where, v):
    m = FORBIDDEN.search(v)
    check(not m, where, "offer / price wording:", m and m.group(0))
    m = BRANDS.search(v)
    check(not m, where, "brand / reference / ranking name:", m and m.group(0))
    m = MEDICAL.search(v)
    check(not m, where, "medical claim / graphic wording:", m and m.group(0))
    check("�" not in v and not any(ord(c) < 32 and c != "\n" for c in v), where, "bad character")


check(set(catalog["ad_free_products"]) >= {"noads", "premium", "bundle"}, "iap.json ad_free_products must hold noads, premium, bundle")
for pid, p in catalog["products"].items():
    check(bool(p.get("ad_free")) == (pid in catalog["ad_free_products"]), pid, "ad_free flag disagrees with ad_free_products")

files = {p.stem: p for p in (ROOT / "listing").glob("*.json")}
check(set(files) <= set(LOCALES), "unknown listing locale:", sorted(set(files) - set(LOCALES)))
check(REQUIRED <= set(files), "missing listing:", sorted(REQUIRED - set(files)))
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
        text_checks(f"{loc} {key}", v)
        counts.append(f"{len(v)}/{units(v)}")
    check(doc.get("title", "").endswith(BRAND), loc, "title must end with", repr(BRAND), "(keyword first, brand last)")
    full = doc.get("fullDescription", "")
    if loc == "en-US":
        check(re.search(r"^[A-Z][A-Z ,&'-]{3,}$", full, re.M), loc, "fullDescription needs CAPS section headers")
        check(re.search(r"^- ", full, re.M), loc, "fullDescription needs '- ' bullets")
    for pid in AD_FREE:
        name = catalog["products"][pid]["title"].get(lang, "")
        check(name and name in full, loc, pid, "ad-removal product name missing:", name)
    check(full.count(PRIVACY) == 1, loc, "privacy policy URL must appear once")
    check(full.rstrip().endswith(PRIVACY), loc, "fullDescription must end with the privacy policy URL")
    print(loc, doc.get("title"), *counts, sep=" | ")

shots_dir = ROOT / "screenshots"
en = json.loads((shots_dir / "en.json").read_text(encoding="utf-8"))
ids = [s.get("id") for s in en.get("shots", [])]
check(len(ids) == 8 and len(set(ids)) == 8, "screenshot plan must have 8 distinct shots")
for p in sorted(shots_dir.glob("*.json")):
    lang = p.stem
    check(lang in LOCALES.values(), p.name, "unknown screenshot language")
    d = json.loads(p.read_text(encoding="utf-8"))
    check([s.get("id") for s in d.get("shots", [])] == ids, lang, "shot ids differ from en")
    hl, sl, fl = (15, 25, 20) if lang in CJK else (30, 50, 40)
    for s in d.get("shots", []):
        for k, lim in (("headline", hl), ("sub", sl)):
            v = s.get(k, "")
            check(v and len(v) <= lim, lang, s.get("id"), k, "length", len(v), "max", lim)
            text_checks(f"{lang} {s.get('id')} {k}", v)
        if lang == "en":
            check(len(s.get("scene", "")) >= 20, "en", s.get("id"), "scene must say what the frame shows")
    f = d.get("feature", "")
    check(f and len(f) <= fl, lang, "feature tagline length", len(f), "max", fl)
    text_checks(f"{lang} feature", f)

ds = ROOT / "play-data-safety.csv"
check(ds.exists(), "play-data-safety.csv missing")
if ds.exists():
    rows = list(csv.reader(ds.open(encoding="utf-8")))
    check(rows and rows[0][:3] == ["Question ID (machine readable)", "Response ID (machine readable)", "Response value"],
          "play-data-safety.csv header changed")
    check(len(rows) > 700, "play-data-safety.csv looks truncated", len(rows))

if fails:
    print("\n".join("FAIL " + f for f in fails))
    print(f"STORE_LISTING_FAIL ({len(fails)})")
    sys.exit(1)
print("STORE_LISTING_PASS")
