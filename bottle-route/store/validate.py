#!/usr/bin/env python3
"""Offline checks for the Recycling Tycoon: Bottle Route (bottle-route) Play listing + screenshot copy. No store / API access.

  python3 bottle-route/store/validate.py      # exit 0 = STORE_LISTING_PASS

Checks: en-US present (the other 9 locales are translated centrally later and are accepted / checked when they appear),
Play limits (chars AND UTF-16 units), title = keyword first + brand after a colon, no trial / free / price / ranking
wording, no real scheme / agency / retailer names (plan originality rule), no competitor or reference-game titles, the four
ad-removal products named exactly as iap.json (CLAUDE.md §8), the privacy policy URL once and last, screenshot copy
(8 shots, headline ≤ 30, sub ≤ 50, scene, feature tagline ≤ 40; CJK halves) with the same ids in every language,
play-data-safety.csv present and unchanged in shape, store art sizes (feature graphic 1024 × 500, icon candidates 512²).
"""
import json
import re
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
GAME = ROOT.parent
LOCALES = {"en-US": "en", "tr-TR": "tr", "de-DE": "de", "es-ES": "es", "fr-FR": "fr", "pt-BR": "pt", "ru-RU": "ru",
           "ja-JP": "ja", "ko-KR": "ko", "hi-IN": "hi"}
REQUIRED = {"en-US"}
LIMITS = {"title": 30, "shortDescription": 80, "fullDescription": 4000}
BRAND = "Bottle Route"   # owner ASO rule: keyword first, brand after the colon ("Recycling Tycoon: Bottle Route")
CJK = {"ja", "ko"}
FORBIDDEN = re.compile(
    r"\btrial\b|\bfree\b|try\s+free|\bgift\b|\$\s?\d|€\s?\d|\bprice\s+tag\b|\bpricing\b|\bsale\b|\bdiscount\b|"
    r"ücretsiz|bedava|\bdeneme\b|kostenlos\w*|gratis|gratuit\w*|testversion|probeabo|prueba\s+gratuita|essai|"
    r"grátis|teste\s+gr[aá]tis|avaliação\s+gratuita|бесплатн\w*|пробн\w*|"
    r"無料|体験版|お試し|무료|체험|मुफ़्त|मुफ्त|फ्री|ट्रायल|निःशुल्क", re.IGNORECASE)
# originality list (RECYCLING_GAME_PLAN.md header + §3 + §11): real scheme / agency / ministry / retailer names, competitor
# and reference titles, ranking claims. "DOA" is matched case-sensitively below (Portuguese "doa" is an ordinary word).
BRANDS = re.compile(
    r"TÜÇA|TUCA\b|Türkiye\s+Çevre\s+Ajans\w*|Environment\s+Agency|\bministry\b|\bbakanl\w*|sıfır\s*atık|zero\s+waste\s+mark|"
    r"\bBİM\b|\bBIM\b|carrefour|\bA101\b|\bŞOK\b|migros|return\s+and\s+earn|return\s*&\s*earn|tomra|deutsche\s+pfand\w*|\bDPG\b|"
    r"trash\s+tycoon|recycle\s+tycoon|recycle\s+empire|recyclings?\s+run|garbage\s+truck\s+simulator|borderline\s+empire|"
    r"#\s*1\b|number\s+one|\bno\.\s*1\b|best\s+game|\btop\s+game|1位|최고의|最高", re.IGNORECASE)
SCHEME = re.compile(r"\bDOA\b|\bD\.O\.A\b")
PRIVACY = "https://www.coddykit.com/pages/privacy-policy"
AD_FREE = ("noads", "premium", "route_pass", "bundle")

catalog = json.loads((GAME / "unity/Assets/Resources/data/iap.json").read_text(encoding="utf-8"))["products"]
fails = []


def check(cond, *msg):
    if not cond:
        fails.append(" ".join(str(m) for m in msg))


def units(s):
    return len(s.encode("utf-16-le")) // 2


def text_ok(where, v):
    m = FORBIDDEN.search(v)
    check(not m, where, "offer / price wording:", m and m.group(0))
    m = BRANDS.search(v) or SCHEME.search(v)
    check(not m, where, "real brand / scheme / competitor / ranking:", m and m.group(0))


def png_size(p):
    b = p.read_bytes()[:24]
    return struct.unpack(">II", b[16:24]) if b[:8] == b"\x89PNG\r\n\x1a\n" else (0, 0)


files = {p.stem: p for p in (ROOT / "listing").glob("*.json")}
check(REQUIRED <= set(files), "listing: missing", sorted(REQUIRED - set(files)))
check(set(files) <= set(LOCALES), "listing: unknown locale files", sorted(set(files) - set(LOCALES)))
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
        text_ok(f"{loc} {key}", v)
        check("�" not in v and not any(ord(c) < 32 and c != "\n" for c in v), loc, key, "bad character")
        counts.append(f"{len(v)}/{units(v)}")
    title = doc.get("title", "")
    check(title.endswith(": " + BRAND), loc, "title must be '<keyword>: Bottle Route' (keyword first, brand last):", title)
    full = doc.get("fullDescription", "")
    for pid in AD_FREE:
        name = catalog[pid]["title"].get(lang) or catalog[pid]["title"]["en"]
        check(name in full, loc, pid, "ad-removal product name missing (exact iap.json title):", name)
    check(full.count(PRIVACY) == 1, loc, "privacy policy URL must appear once")
    check(full.rstrip().endswith(PRIVACY), loc, "the listing must end with the privacy policy URL")
    if lang == "en":
        check(len(re.findall(r"^[A-Z][A-Z &'\-]{3,}$", full, re.M)) >= 4, loc, "fullDescription needs CAPS section headers")
        check(len(re.findall(r"^- ", full, re.M)) >= 8, loc, "fullDescription needs '-' bullets")
    print(loc, title, *counts, sep=" | ")

shots_dir = ROOT / "screenshots"
en = json.loads((shots_dir / "en.json").read_text(encoding="utf-8"))
ids = [s["id"] for s in en["shots"]]
check(ids == [f"s{i}" for i in range(1, 9)], "screenshots/en.json: need 8 shots s1..s8, got", ids)
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
            text_ok(f"{lang} {s.get('id')} {k}", v)
        if lang == "en":
            check(len(s.get("scene", "")) >= 20, lang, s.get("id"), "scene: say what the frame shows")
    f = d.get("feature", "")
    check(f and len(f) <= fl, lang, "feature tagline length", len(f), "max", fl)
    text_ok(f"{lang} feature", f)

csv = ROOT / "play-data-safety.csv"
check(csv.exists() and csv.read_text(encoding="utf-8").startswith("Question ID (machine readable)"), "play-data-safety.csv missing / not the Play CSV")
fg = ROOT / "feature-graphic.png"
check(fg.exists() and png_size(fg) == (1024, 500), "feature-graphic.png must be a 1024 × 500 PNG")
for i in (1, 2, 3):
    ic = ROOT / "icon-candidates" / f"c{i}.png"
    check(ic.exists() and png_size(ic) == (512, 512), ic.name, "icon candidate must be a 512 × 512 PNG")

if fails:
    print("\n".join("FAIL " + f for f in fails))
    print(f"STORE_LISTING_FAIL ({len(fails)})")
    sys.exit(1)
print("STORE_LISTING_PASS")
