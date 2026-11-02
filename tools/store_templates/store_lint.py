"""Shared, language-aware offline checks for the Play store listing + screenshot copy of a game (<game>/store/).

Each game keeps a thin `<game>/store/validate.py` (config + `store_lint.run(cfg)`). Template: tools/store_templates/validate.py.

What is checked (per locale, in the locale's own language):
  * length limits (chars AND UTF-16 units), trimmed, no control / replacement characters, exactly the 3 listing keys
  * generic banned wording in all 10 languages (GENERIC below): free / trial / price / discount / gift / ranking claims
  * the game's own lists (config): proper-noun lists (brands, schemes, competitor + reference-game names) apply to EVERY
    locale; English-only patterns (banned_en*) apply to en-US only; `banned_lang` holds per-language word lists for
    categories that are not proper nouns (medical claims, child-directed words ...)
  * title = "<keyword>: <brand>", ad-removal product names (iap.json ad_free_products, exact title of that language),
    privacy URL once and last, 'en' style checks (CAPS section headers, '- ' bullets) only on en-US
  * screenshot copy: same ids as en, headline <= 30 / sub <= 50 / feature <= 40 (ja/ko: 15 / 25 / 20, full-width glyphs)
  * Data safety CSV shape and store art PNG sizes

Matching: Latin / Cyrillic / Greek scripts use word-boundary matching ((?<!\\w)..(?!\\w)); ja / ko / hi (no reliable word
boundaries in these scripts for Python's \\w) use plain substring matching. Fragments are regexes; add `\\w*` for a suffix.
"""
import csv
import json
import re
import struct
import sys
import unicodedata
from pathlib import Path

LOCALES = {"en-US": "en", "tr-TR": "tr", "de-DE": "de", "es-ES": "es", "fr-FR": "fr", "pt-BR": "pt", "ru-RU": "ru",
           "ja-JP": "ja", "ko-KR": "ko", "hi-IN": "hi"}
LIMITS = {"title": 30, "shortDescription": 80, "fullDescription": 4000}
SHOT_LIMITS = {"headline": 30, "sub": 50, "feature": 40}
CJK = {"ja", "ko"}  # screenshot copy limits are halved (full-width glyphs): 15 / 25 / 20
SUBSTRING_LANGS = {"ja", "ko", "hi"}
PRIVACY = "https://www.coddykit.com/pages/privacy-policy"

# Currency amounts: language independent, matched raw (no word boundaries).
SYMBOLS = r"\$\s?\d|\d\s?\$|€\s?\d|\d\s?€|£\s?\d|₺\s?\d|\d\s?₺|₹\s?\d|\d\s?₹|\d\s?TL\b|\d+\s?(?:円|원)|\d+\s?%\s?(?:off|rabatt|de\s+r[ée]duction|desc\w*|indirim|скидк\w*|オフ|할인)"

# category -> language -> regex fragments (OR-ed, wrapped with word boundaries for boundary-script languages).
GENERIC = {
    "free": {
        "en": [r"free", r"freely", r"complimentary", r"no\s+cost", r"at\s+no\s+charge"],
        "tr": [r"[üu]cretsiz\w*", r"bedava\w*", r"parasız\w*"],
        "de": [r"kostenlos\w*", r"gratis\w*", r"umsonst", r"kostenfrei\w*", r"gebührenfrei"],
        "es": [r"gratis", r"gratuit[oa]s?", r"gratuitamente", r"sin\s+cost[eo]"],
        "fr": [r"gratuit\w*", r"gratis"],
        "pt": [r"gr[aá]tis", r"gr[aá]tuit[oa]s?", r"gratuitamente", r"sem\s+custo"],
        "ru": [r"бесплатн\w*", r"даром", r"халяв\w*", r"безвозмездн\w*"],
        "ja": [r"無料", r"タダ", r"0円", r"ゼロ円"],
        "ko": [r"무료", r"공짜"],
        "hi": [r"मुफ़्त", r"मुफ्त", r"फ्री", r"निःशुल्क", r"निशुल्क", r"बिना\s+शुल्क"],
    },
    # offer phrasings of a trial (a bare "trial" is category trial_word, which a game may switch off)
    "trial": {
        "en": [r"free\s+trials?", r"trial\s+(?:version|period|run|mode|offer)", r"try\s+(?:it\s+)?(?:for\s+)?free"],
        "tr": [r"deneme\s+(?:s[üu]r[üu]m|s[üu]re|d[öo]nem|paket)\w*", r"\d+\s*g[üu]n(?:l[üu]k)?\s+deneme"],
        "de": [r"testversion\w*", r"probeabo\w*", r"probeversion\w*", r"probezeit", r"testphase", r"testzeitraum",
               r"testabo\w*", r"probemonat\w*", r"testzugang"],
        "es": [r"prueba\s+gratu\w+", r"versi[óo]n\s+de\s+prueba", r"per[ií]odo\s+de\s+prueba",
               r"prueba\s+de\s+\d+\s+d[ií]as", r"\d+\s+d[ií]as\s+de\s+prueba"],
        "fr": [r"essai\s+gratuit\w*", r"version\s+d.essai", r"p[ée]riode\s+d.essai", r"essai\s+de\s+\d+",
               r"\d+\s+jours?\s+d.essai", r"version\s+de\s+test", r"p[ée]riode\s+de\s+test"],
        "pt": [r"teste\s+gr[aá]\w*", r"avalia[cç][aã]o\s+gratuita", r"per[ií]odo\s+de\s+(?:teste|avalia[cç][aã]o)",
               r"vers[aã]o\s+(?:de\s+)?(?:teste|avalia[cç][aã]o|experimental)", r"\d+\s+dias\s+de\s+(?:teste|avalia\w+)"],
        "ru": [r"пробн\w*", r"тестов\w+\s+(?:период|версия|доступ)", r"триал\w*", r"демо-?версия"],
        "ja": [r"体験版", r"お試し", r"試用", r"トライアル", r"無料体験", r"体験期間"],
        "ko": [r"체험판", r"체험\s*(?:기간|버전)", r"무료\s*체험", r"트라이얼", r"시험\s*사용", r"평가판"],
        "hi": [r"ट्रायल", r"परीक्षण\s*(?:अवधि|संस्करण|वर्जन)"],
    },
    "trial_word": {
        "en": [r"trials?"],
    },
    "price": {
        "en": [r"prices?", r"pricing", r"priced"],
        "tr": [r"fiyat\w*"],
        "de": [r"preis\w*"],
        "es": [r"precios?"],
        "fr": [r"prix", r"tarifs?"],
        "pt": [r"pre[çc]os?"],
        "ru": [r"цена", r"цены", r"цену", r"ценой", r"ценам", r"ценах", r"стоимост\w*"],
        "ja": [r"価格", r"値段", r"料金"],
        "ko": [r"가격", r"요금"],
        "hi": [r"कीमत", r"क़ीमत"],
    },
    "discount": {
        "en": [r"discounts?", r"discounted", r"sale", r"coupons?", r"promo\s*code", r"special\s+offer",
               r"limited[-\s]time\s+(?:offer|deal)"],
        "tr": [r"indirim\w*", r"kupon\w*"],
        "de": [r"rabatt\w*", r"sonderangebot\w*", r"gutschein\w*", r"ausverkauf"],
        "es": [r"descuentos?", r"rebajas?", r"oferta\s+(?:especial|limitada)", r"cup[óo]n(?:es)?", r"liquidaci[óo]n"],
        "fr": [r"prix\s+r[ée]duits?", r"soldes?", r"code\s+promo\w*", r"bons?\s+de\s+r[ée]duction"],
        "pt": [r"descontos?", r"promo[cç][aã]o\s+(?:especial|rel[aâ]mpago)", r"cupom", r"cupons", r"liquida[cç][aã]o"],
        "ru": [r"скидк\w*", r"промокод\w*"],
        "ja": [r"割引", r"セール", r"クーポン"],
        "ko": [r"할인", r"세일", r"쿠폰"],
        "hi": [r"डिस्काउंट", r"छूट(?![\u0900-\u0903\u093e-\u094c])", r"सेल", r"कूपन"],  # not छूटा / छूटी (missed)
    },
    "gift": {
        "en": [r"gifts?"],
        "tr": [r"hediye\w*"],
        "de": [r"geschenk\w*"],
        "es": [r"regalos?", r"regalamos"],
        "fr": [r"cadeau\w*", r"cadeaux"],
        "pt": [r"brinde\w*"],
        "ru": [r"подар\w*"],
        "ja": [r"ギフト", r"プレゼント", r"贈り物"],
        "ko": [r"선물"],
        "hi": [r"उपहार", r"तोहफ\w*"],
    },
    "ranking claim": {
        "en": [r"#\s*1", r"number\s+one", r"no\.\s*1", r"best\s+game", r"top\s+game", r"top[-\s]rated", r"top\s+\d+",
               r"award[-\s]winning", r"editor.?s\s+choice"],
        "tr": [r"bir\s+numara\w*", r"en\s+iyi\s+oyun\w*", r"numara\s*1"],
        "de": [r"nummer\s+(?:eins|1)", r"(?:das\s+)?beste\s+spiel", r"bestes\s+spiel"],
        "es": [r"n[úu]mero\s+(?:uno|1)", r"mejor\s+juego", r"n[ºo°]\s?1"],
        "fr": [r"num[ée]ro\s+(?:un|1)", r"meilleur\s+jeu", r"n°\s?1"],
        "pt": [r"n[úu]mero\s+(?:um|1)", r"melhor\s+jogo", r"n[ºo°]\s?1"],
        "ru": [r"номер\s+(?:один|1)", r"лучшая\s+игра", r"№\s?1", r"топ-?1"],
        "ja": [r"1位", r"第1位", r"ナンバー(?:1|ワン)", r"最高の"],
        "ko": [r"1위", r"넘버\s?(?:원|1)", r"최고의"],
        "hi": [r"नंबर\s*(?:1|वन|एक)", r"बेस्ट\s*गेम", r"सर्वश्रेष्ठ\s*गेम"],
    },
}


def _nfc(s):
    return unicodedata.normalize("NFC", s)


def _compile(lang, frags, flags=re.IGNORECASE):
    """One regex for a language's fragments: word-bounded, or plain substrings for ja / ko / hi."""
    if not frags:
        return None
    body = "(?:" + "|".join(_nfc(f) for f in frags) + ")"
    if lang not in SUBSTRING_LANGS:
        body = r"(?<!\w)" + body + r"(?!\w)"
    return re.compile(body, flags)


def _raw(patterns, flags=re.IGNORECASE):
    return re.compile("|".join(_nfc(p) for p in patterns), flags) if patterns else None


class Matcher:
    """All banned-wording rules of one game, resolved per language."""

    def __init__(self, cfg):
        off = set(cfg.get("generic_off", ()))
        self.cfg = cfg
        self.symbols = None if "symbols" in off else re.compile(SYMBOLS, re.IGNORECASE)
        self.generic = {
            lang: [(cat, _compile(lang, tbl.get(lang))) for cat, tbl in GENERIC.items() if cat not in off and tbl.get(lang)]
            for lang in LOCALES.values()}
        self.proper = _raw(cfg.get("banned_all", ()))
        self.proper_cs = _raw(cfg.get("banned_all_cs", ()), 0)
        self.en_only = _raw(cfg.get("banned_en", ()))
        self.en_only_cs = _raw(cfg.get("banned_en_cs", ()), 0)
        self.lang_lists = {
            lang: [(label, _compile(lang, per_lang.get(lang))) for label, per_lang in cfg.get("banned_lang", {}).items()
                   if per_lang.get(lang)]
            for lang in LOCALES.values()}

    def find(self, lang, text):
        """First banned-wording hit in `text` written in `lang`, as (label, matched text), else None."""
        variants = [_nfc(text)]
        if lang == "tr":  # dotted / dotless I do not case-fold in Python's re
            variants.append(variants[0].replace("İ", "i").replace("I", "ı"))
        for v in variants:
            if self.symbols and (m := self.symbols.search(v)):
                return "offer / price amount", m.group(0)
            for cat, rx in self.generic[lang]:
                if m := rx.search(v):
                    return ("offer / price / ranking wording (" + cat + ")"), m.group(0)
            for label, rx in self.lang_lists[lang]:
                if m := rx.search(v):
                    return label, m.group(0)
            for rx, label in ((self.proper, "brand / reference / competitor name"),
                              (self.proper_cs, "brand / scheme name")):
                if rx and (m := rx.search(v)):
                    return label, m.group(0)
            if lang == "en":
                for rx, label in ((self.en_only, "banned English wording"), (self.en_only_cs, "banned English acronym / name")):
                    if rx and (m := rx.search(v)):
                        return label, m.group(0)
        return None


def units(s):
    return len(s.encode("utf-16-le")) // 2


def png_size(p):
    b = p.read_bytes()[:24]
    return struct.unpack(">II", b[16:24]) if b[:8] == b"\x89PNG\r\n\x1a\n" else None


class Ctx:
    def __init__(self, root, game, iap):
        self.root, self.game, self.iap, self.fails = root, game, iap, []

    def check(self, cond, *msg):
        if not cond:
            self.fails.append(" ".join(str(m) for m in msg))


def run(cfg):
    """Run every check for one game and exit (0 = STORE_LISTING_PASS). cfg keys:
    file (validate.py __file__), brand (without ': '), banned_all, banned_all_cs, banned_en, banned_en_cs, banned_lang,
    generic_off (category names or 'symbols'), required (locales that must exist; default all 10), en_listing(ctx, full),
    extra(ctx), art (bool, default True), shot_keys (set | None), scene_required (default True)."""
    root = Path(cfg["file"]).resolve().parent
    game = root.parent
    iap = json.loads((game / "unity/Assets/Resources/data/iap.json").read_text(encoding="utf-8"))
    ctx = Ctx(root, game, iap)
    check = ctx.check
    matcher = Matcher(cfg)
    required = set(LOCALES) if ("--all" in sys.argv[1:] or "required" not in cfg) else set(cfg["required"])
    brand = ": " + cfg["brand"]
    catalog = iap["products"]
    ad_free = [p for p in iap.get("ad_free_products", []) if p in catalog]
    check({"noads", "premium", "bundle"} <= set(ad_free), "iap.json ad_free_products must hold noads, premium, bundle")
    for pid, p in catalog.items():
        check(bool(p.get("ad_free")) == (pid in iap.get("ad_free_products", [])), pid,
              "ad_free flag disagrees with ad_free_products")

    def text_ok(where, lang, v):
        hit = matcher.find(lang, v)
        check(not hit, where, hit and f"{hit[0]}: {hit[1]}")
        check("�" not in v and not any(ord(c) < 32 and c != "\n" for c in v), where, "bad character")

    files = {p.stem: p for p in (root / "listing").glob("*.json")}
    check(required <= set(files), "listing missing:", sorted(required - set(files)))
    check(set(files) <= set(LOCALES), "unknown listing locale:", sorted(set(files) - set(LOCALES)))
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
            text_ok(f"{loc} {key}", lang, v)
            counts.append(f"{len(v)}/{units(v)}")
        title = doc.get("title", "")
        check(title.endswith(brand) and len(title) > len(brand), loc, f"title must be '<keyword>{brand}':", title)
        full = doc.get("fullDescription", "")
        for pid in ad_free:
            name = catalog[pid]["title"].get(lang) or catalog[pid]["title"]["en"]
            check(name in full, loc, pid, "ad-removal product name missing (exact iap.json title):", name)
        check(full.count(PRIVACY) == 1, loc, "privacy policy URL must appear exactly once")
        check(full.rstrip().endswith(PRIVACY), loc, "the privacy policy URL must be the last line")
        if loc == "en-US" and cfg.get("en_listing"):
            cfg["en_listing"](ctx, full)
        print(loc, title, *counts, sep=" | ")

    shots_dir = root / "screenshots"
    en_path = shots_dir / "en.json"
    check(en_path.exists(), en_path, "missing")
    if en_path.exists():
        en = json.loads(en_path.read_text(encoding="utf-8"))
        ids = [s.get("id") for s in en.get("shots", [])]
        check(len(ids) == 8 and len(set(ids)) == 8, "screenshot plan must have 8 distinct shots")
        langs = set(LOCALES.values())
        check({LOCALES[l] for l in required} <= {p.stem for p in shots_dir.glob("*.json")}, "screenshots missing for",
              sorted({LOCALES[l] for l in required} - {p.stem for p in shots_dir.glob("*.json")}))
        for p in sorted(shots_dir.glob("*.json")):
            lang = p.stem
            check(lang in langs, p.name, "unknown screenshot language")
            if lang not in langs:
                continue
            d = json.loads(p.read_text(encoding="utf-8"))
            check([s.get("id") for s in d.get("shots", [])] == ids, lang, "shot ids differ from en")
            half = 2 if lang in CJK else 1
            for s in d.get("shots", []):
                if cfg.get("shot_keys"):
                    check(set(s) == set(cfg["shot_keys"]), lang, s.get("id"), "keys must be", sorted(cfg["shot_keys"]))
                for k in ("headline", "sub"):
                    v = s.get(k, "")
                    lim = SHOT_LIMITS[k] // half
                    check(v and len(v) <= lim, lang, s.get("id"), k, "length", len(v), "max", lim)
                    text_ok(f"{lang} {s.get('id')} {k}", lang, v)
                if cfg.get("scene_required", True) and lang == "en":
                    check(len(s.get("scene", "")) >= 20, lang, s.get("id"), "scene must say what the frame shows")
            f = d.get("feature", "")
            lim = SHOT_LIMITS["feature"] // half
            check(f and len(f) <= lim, lang, "feature tagline length", len(f), "max", lim)
            text_ok(f"{lang} feature", lang, f)

    ds = root / "play-data-safety.csv"
    check(ds.exists(), "play-data-safety.csv missing")
    if ds.exists():
        rows = list(csv.reader(ds.open(encoding="utf-8")))
        check(rows and rows[0][:3] == ["Question ID (machine readable)", "Response ID (machine readable)", "Response value"],
              "play-data-safety.csv header changed")
        check(len(rows) > 700, "play-data-safety.csv looks truncated", len(rows))
    if cfg.get("art", True):
        for rel, size in [("feature-graphic.png", (1024, 500))] + [(f"icon-candidates/c{i}.png", (512, 512)) for i in (1, 2, 3)]:
            p = root / rel
            check(p.exists() and png_size(p) == size, rel, "missing or not a PNG of", size)
    if cfg.get("extra"):
        cfg["extra"](ctx)

    if ctx.fails:
        print("\n".join("FAIL " + f for f in ctx.fails))
        print(f"STORE_LISTING_FAIL ({len(ctx.fails)})")
        sys.exit(1)
    print("STORE_LISTING_PASS")
    sys.exit(0)
