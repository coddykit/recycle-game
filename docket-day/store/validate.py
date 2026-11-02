#!/usr/bin/env python3
"""Offline checks for the Law Firm Tycoon: Docket Day (docket-day) Play listing + screenshot copy. No store / API access.

  python3 docket-day/store/validate.py      # exit 0 = STORE_LISTING_PASS   (called by unity/Tools/check.sh)

Checks: en-US is present (the other 9 locales are translated centrally later; any locale file that exists must be one
of the 10 and is checked the same way), Play limits (chars AND UTF-16 units), title = keyword first + ": Docket Day",
no trial / free / price / ranking wording, no real firm / court / brand names and none of the plan's originality list
(reference game, its publisher, other law games), the four ad-removal products named exactly as iap.json
(games/CLAUDE.md §8), the privacy policy URL once and last, screenshot copy = 8 shots, same ids as en, length limits.

Note: "trial" is ordinary courtroom vocabulary in this game, so only offer phrasings ("free trial", "trial version",
"trial period") are banned in English; the other languages keep the fleet's offer words.
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
BRAND = ": Docket Day"  # owner ASO rule: keyword first, brand after the colon
CJK = {"ja", "ko"}
FORBIDDEN = re.compile(
    r"free\s+trial|trial\s+(version|period|run)|\bfree\b|try\s+free|\bgift\b|\$\s?\d|€\s?\d|\bprice\b|\bpricing\b|"
    r"\bdiscount\b|\bsale\b|ücretsiz|bedava|kostenlos\w*|gratis|gratuit\w*|testversion|probeabo|prueba\s+gratuita|"
    r"essai\s+gratuit|grátis|teste\s+gr[aá]tis|avaliação\s+gratuita|бесплатн\w*|пробн\w*|"
    r"無料|体験版|お試し|무료|체험판|मुफ़्त|मुफ्त|फ्री|ट्रायल|निःशुल्क", re.IGNORECASE)
BRANDS = re.compile(
    # plan §1/§11/§13 originality list: the reference game, its publisher and other law / court games
    r"codigames|law\s*empire|justice\s*empire|idle\s*law\s*firm|ace\s*attorney|phoenix\s*wright|devil'?s\s*attorney|"
    r"icivics|lawyer\s*life|judge\s*3d|court\s*affairs|guilty!|choose\s+the\s+justice|"
    # real courts, firms and TV / brand names
    r"supreme\s*court|baker\s*mckenzie|kirkland|skadden|latham|clifford\s*chance|freshfields|linklaters|dentons|"
    r"allen\s*&\s*overy|judge\s*judy|better\s*call\s*saul|perry\s*mason|"
    # ranking / chart claims
    r"#\s*1\b|number\s+one|\bno\.\s*1\b|best\s+game|top\s+\d+|award[- ]winning|1位|최고의|最高", re.IGNORECASE)
PRIVACY = "https://www.coddykit.com/pages/privacy-policy"
AD_FREE = ("noads", "premium", "partner_pass", "bundle")
SHOT_KEYS = ("headline", "sub")

catalog = json.loads((GAME / "unity/Assets/Resources/data/iap.json").read_text(encoding="utf-8"))["products"]
fails = []


def check(cond, *msg):
    if not cond:
        fails.append(" ".join(str(m) for m in msg))


def units(s):
    return len(s.encode("utf-16-le")) // 2


def words(loc, key, v):
    m = FORBIDDEN.search(v)
    check(not m, loc, key, "offer / price wording:", m and m.group(0))
    m = BRANDS.search(v)
    check(not m, loc, key, "brand / reference / ranking:", m and m.group(0))
    check("�" not in v and not any(ord(c) < 32 and c != "\n" for c in v), loc, key, "bad character")


files = {p.stem: p for p in (ROOT / "listing").glob("*.json")}
check(REQUIRED <= set(files), "listing: missing", sorted(REQUIRED - set(files)))
check(set(files) <= set(LOCALES), "listing: unknown locale(s)", sorted(set(files) - set(LOCALES)))
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
        words(loc, key, v)
        counts.append(f"{len(v)}/{units(v)}")
    title = doc.get("title", "")
    check(title.endswith(BRAND) and len(title) > len(BRAND), loc, "title must be '<keyword>" + BRAND + "'")
    full = doc.get("fullDescription", "")
    for pid in AD_FREE:
        name = catalog[pid]["title"].get(lang) or catalog[pid]["title"]["en"]
        check(name in full, loc, pid, "ad-removal product name missing:", name)
    check(full.count(PRIVACY) == 1 and full.endswith(PRIVACY), loc, "privacy policy URL must appear once, at the end")
    if lang == "en":
        check(re.search(r"^[A-Z][A-Z ,&'-]{3,}$", full, re.M) is not None, loc, "CAPS section headers missing")
        check(full.count("\n- ") >= 8, loc, "'-' bullets missing")
    print(loc, title, *counts, sep=" | ")

shots_dir = ROOT / "screenshots"
en_path = shots_dir / "en.json"
check(en_path.exists(), en_path, "missing")
if en_path.exists():
    en = json.loads(en_path.read_text(encoding="utf-8"))
    ids = [s.get("id") for s in en.get("shots", [])]
    check(len(ids) == 8 and len(set(ids)) == 8, "screenshot plan must have 8 distinct shots")
    for s in en["shots"]:
        check(s.get("scene"), "en", s.get("id"), "scene missing")
    for p in sorted(shots_dir.glob("*.json")):
        lang = p.stem
        check(lang in LOCALES.values(), p.name, "unknown language")
        d = json.loads(p.read_text(encoding="utf-8"))
        check([s.get("id") for s in d.get("shots", [])] == ids, lang, "shot ids differ from en")
        hl, sl, fl = (15, 25, 20) if lang in CJK else (30, 50, 40)
        for s in d.get("shots", []):
            for k, lim in zip(SHOT_KEYS, (hl, sl)):
                v = s.get(k, "")
                check(v and len(v) <= lim, lang, s.get("id"), k, "length", len(v), "max", lim)
                words(lang, f"{s.get('id')}.{k}", v)
        f = d.get("feature", "")
        check(f and len(f) <= fl, lang, "feature tagline length", len(f), "max", fl)
        words(lang, "feature", f)

for f in ("feature-graphic.png", "icon-candidates/c1.png", "icon-candidates/c2.png", "icon-candidates/c3.png"):
    check((ROOT / f).exists(), "missing", f, "(tools/art/render_store.py)")
check((ROOT / "play-data-safety.csv").exists(), "missing play-data-safety.csv")

if fails:
    print("\n".join("FAIL " + f for f in fails))
    print(f"STORE_LISTING_FAIL ({len(fails)})")
    sys.exit(1)
print("STORE_LISTING_PASS")
