#!/usr/bin/env python3
"""Offline store checks for Dino Park: Ridgeback Rangers (ridgeback-rangers/) — Play listing + screenshot copy. No store / API
access. Called by unity/Tools/check.sh.

  python3 ridgeback-rangers/store/validate.py      # exit 0 = STORE_LISTING_PASS

Checks: en-US present (the other 9 locales are translated centrally later and are accepted / checked the same way when they
land), Play limits (chars AND UTF-16 units), title = keyword first, brand last; no trial / free / price / ranking wording; no
film-franchise, reference-game, competitor or real-brand names (plan #184 §3 / R6); no child-directed marketing words (the game
is 13+, Families decision pending with the owner — plan R4); the four ad-removal products named exactly as iap.json (fleet
CLAUDE.md §8); policy URLs once, the privacy policy URL last; screenshot plan = 8 shots with the same ids in every language.
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
GAME = ROOT.parent
LOCALES = {"en-US": "en", "tr-TR": "tr", "de-DE": "de", "es-ES": "es", "fr-FR": "fr", "pt-BR": "pt", "ru-RU": "ru",
           "ja-JP": "ja", "ko-KR": "ko", "hi-IN": "hi"}
REQUIRED = {"en-US"}  # 1 locale now; the rest come from the central translation run
LIMITS = {"title": 30, "shortDescription": 80, "fullDescription": 4000}
BRAND = ": Ridgeback Rangers"  # owner ASO rule: keyword first, brand last
CJK = {"ja", "ko"}
FORBIDDEN = re.compile(
    r"\btrial\b|\bfree\b|try\s+free|\bgift\s+card\b|\$\s?\d|€\s?\d|\bprice\b|\bpricing\b|\bsale\b|\bdiscount\b|"
    r"ücretsiz|bedava|\bdeneme\b|kostenlos\w*|gratis|gratuit\w*|testversion|probeabo|prueba\s+gratuita|essai|"
    r"grátis|gratuit[oa]|teste\s+gr[aá]tis|avaliação\s+gratuita|бесплатн\w*|пробн\w*|"
    r"無料|体験版|お試し|무료|체험|मुफ़्त|मुफ्त|फ्री|ट्रायल|निःशुल्क", re.IGNORECASE)
# plan #184 originality list + reference / competitor games + real brands + ranking claims
BRANDS = re.compile(
    r"jurass\w*|jurás\w*|juráss\w*|юрск\w*|ジュラ|쥬라기|쥐라기|जुरासिक|spared\s+no\s+expense|life\s+finds\s+a\s+way|"
    r"isla\s+nublar|isla\s+sorna|\bingen\b|hammond|codigames|saurian|\bdino\s+park\s+land\b|idle\s+dinosaur|"
    r"dino\s+tycoon\b|planet\s+zoo|zoo\s+tycoon|prehistoric\s+kingdom|parkasaurus|"
    r"zoo\s+craft|zoo\s+life|idle\s+zoo|critter\s+crew|\bjeep\b|land\s+rover|national\s+geographic|nat\s*geo|"
    r"discovery\s+channel|smithsonian|natural\s+history\s+museum|\bdisney\b|universal\s+studios|"
    r"#\s*1\b|number\s+one|\bno\.\s*1\b|best\s+game|top\s+rated|1位|최고의|최고|最高", re.IGNORECASE)
# 13+ store page: no child-directed marketing (plan R4); dinosaurs already attract children, the copy must not invite them
CHILDISH = re.compile(
    r"\bkids?\b|\bchild(ren)?\b|toddler|preschool|\bcute\b|\bcutest\b|\bbab(y|ies)\b|little\s+ones|for\s+all\s+ages|"
    r"family[\s-]friendly|\bkinder\b|\bniñ[oa]s\b|enfants|crianças|детей|детск\w*|子ども|子供|어린이|아이들|बच्च\w*|çocuk\w*",
    re.IGNORECASE)
PRIVACY = "https://www.coddykit.com/pages/privacy-policy"
URLS = (PRIVACY, "https://www.coddykit.com/pages/terms-of-service")
AD_FREE = ("noads", "premium", "ranger_pass", "bundle")

iap = json.loads((GAME / "unity/Assets/Resources/data/iap.json").read_text(encoding="utf-8"))
catalog = iap["products"]
fails = []


def check(cond, *msg):
    if not cond:
        fails.append(" ".join(str(m) for m in msg))


def units(s):
    return len(s.encode("utf-16-le")) // 2


def text_ok(where, v):
    m = FORBIDDEN.search(v)
    check(not m, where, "offer / price wording:", m and m.group(0))
    m = BRANDS.search(v)
    check(not m, where, "franchise / reference / brand / ranking:", m and m.group(0))
    m = CHILDISH.search(v)
    check(not m, where, "child-directed wording (13+ page):", m and m.group(0))
    check("�" not in v and not any(ord(c) < 32 and c != "\n" for c in v), where, "bad character")


check(sorted(set(AD_FREE)) == sorted(iap.get("ad_free_products", [])), "iap.json ad_free_products differ from", AD_FREE)
files = {p.stem: p for p in (ROOT / "listing").glob("*.json")}
check(REQUIRED <= set(files), "missing listing locale(s):", sorted(REQUIRED - set(files)))
check(set(files) <= set(LOCALES), "unknown listing locale(s):", sorted(set(files) - set(LOCALES)))
print("locale | title | title / short / full (chars, UTF-16)")
for loc in sorted(set(files) & set(LOCALES)):
    lang = LOCALES[loc]
    doc = json.loads(files[loc].read_text(encoding="utf-8"))
    check(set(doc) == set(LIMITS), loc, "unexpected keys", sorted(doc))
    counts = []
    for key, lim in LIMITS.items():
        v = doc.get(key, "")
        check(isinstance(v, str) and v and v.strip() == v, loc, key, "empty / untrimmed")
        check(len(v) <= lim and units(v) <= lim, loc, key, "too long", len(v), units(v), lim)
        text_ok(f"{loc} {key}", v)
        counts.append(f"{len(v)}/{units(v)}")
    title = doc.get("title", "")
    check(title.endswith(BRAND) and len(title) > len(BRAND), loc, "title must be '<keyword>" + BRAND + "'")
    full = doc.get("fullDescription", "")
    for pid in AD_FREE:
        name = catalog[pid]["title"][lang]
        check(name in full, loc, pid, "ad-removal product name missing:", name)
    for url in URLS:
        check(full.count(url) == 1, loc, url, "must appear once")
    check(full.endswith(PRIVACY), loc, "fullDescription must end with the privacy policy URL")
    if lang == "en":
        check(re.search(r"^[A-Z][A-Z ,&'-]{5,}$", full, re.M) is not None, loc, "no CAPS section headers")
        check(re.search(r"^- ", full, re.M) is not None, loc, "no '- ' bullets")
    print(loc, title, *counts, sep=" | ")

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
        for k, lim in (("headline", hl), ("sub", sl)):
            v = s.get(k, "")
            check(v and len(v) <= lim, lang, s["id"], k, "length", len(v), "max", lim)
            text_ok(f"{lang} {s['id']} {k}", v)
        if lang == "en":
            check(len(s.get("scene", "")) > 20, lang, s["id"], "scene must describe the real frame")
    f = d.get("feature", "")
    check(f and len(f) <= fl, lang, "feature tagline length", len(f), "max", fl)
    text_ok(f"{lang} feature", f)

for name, size in (("feature-graphic.png", (1024, 500)), ("icon-candidates/c1.png", (512, 512)),
                   ("icon-candidates/c2.png", (512, 512)), ("icon-candidates/c3.png", (512, 512))):
    p = ROOT / name
    if not p.exists():
        check(False, name, "missing (tools/art/store_art.py)")
        continue
    head = p.read_bytes()[:24]
    check(head[:8] == b"\x89PNG\r\n\x1a\n", name, "not a PNG")
    w, h = int.from_bytes(head[16:20], "big"), int.from_bytes(head[20:24], "big")
    check((w, h) == size, name, "size", (w, h), "expected", size)

csv = (ROOT / "play-data-safety.csv").read_text(encoding="utf-8")
check(csv.startswith("Question ID (machine readable)"), "play-data-safety.csv header")

if fails:
    print("\n".join("FAIL " + f for f in fails))
    print(f"STORE_LISTING_FAIL ({len(fails)})")
    sys.exit(1)
print("STORE_LISTING_PASS")
