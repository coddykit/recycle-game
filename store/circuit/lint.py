#!/usr/bin/env python3
"""Borderline Empire circuit store-copy lint (port of games/driftwood/tools/store/lint.py): an optional IP denylist
(tests/ip_denylist.txt when the game has one; lower case, accent fold, leetspeak, separators folded), the no-trial / no-"free"
wording in all ten languages, no price, no fake claims (rank, awards, ratings, download counts), no competitor titles.

  from lint import problems            problems(text, lang) -> ["ip:<entry>", "banned:<word>", ...]  ([] = clean)
"""
from pathlib import Path
import re
import unicodedata

GAME = Path(__file__).resolve().parents[2]  # games/smuggler (store/circuit/lint.py)
LEET = {"0": "o", "3": "e", "4": "a", "@": "a", "$": "s", "5": "s", "7": "t"}
SPECIAL = {"ı": "i", "ł": "l", "ø": "o", "đ": "d", "ð": "d", "ħ": "h", "ŧ": "t", "þ": "th", "æ": "ae", "œ": "oe", "ß": "ss"}


def normalize(text, keep_regex=False):
    out = []
    for ch in text.lower():
        code = ord(ch)
        if 0xFF01 <= code <= 0xFF5E:
            ch = chr(code - 0xFEE0).lower()
        if ch in SPECIAL:
            out.append(SPECIAL[ch])
            continue
        if "a" <= ch <= "z" or code < 0x80:
            out.append(LEET.get(ch, ch))
            continue
        base = unicodedata.normalize("NFKD", ch)
        # fold only decomposable Latin letters (é -> e); keep kana/hangul/cyrillic as they are (Godot's table fold)
        if base and base[0].isascii() and base[0].isalpha() and all(unicodedata.combining(c) for c in base[1:]):
            out.append(base[0])
        elif unicodedata.combining(ch):
            continue
        else:
            out.append(ch)
    return re.sub(r"[\s_\-/·]+" if keep_regex else r"[\s_.\-/\\·]+", " ", "".join(out))


def _entries():
    out = []
    deny = GAME / "tests/ip_denylist.txt"
    if not deny.exists():
        return out
    for line in deny.read_text(encoding="utf-8").split("\n"):
        e = line.strip()
        if not e or e.startswith("#"):
            continue
        if e.startswith("~"):
            out.append((e, normalize(e[1:]), None))
        else:
            out.append((e, None, re.compile(r"\b(?:" + normalize(e, True) + r")\b")))
    return out


ENTRIES = _entries()

# Store copy promises nothing the build does not do (games/CLAUDE.md, NEW_GAME_PLAYBOOK §20): no trial, no "free",
# no discount/offer, no price, no rank/award/rating/download claim, no competitor title.
BANNED = {
    "*": [r"\$\s?\d", r"\d\s?\$", "€", "₺", "₽", "₩", "¥", "£", r"#\s?1\b", r"\bno\.?\s?1\b", "№ ?1", r"\b100\s?%",
          r"\bsurvivor\b ?(?:game )?of the year"],
    "en": [r"\bfree\b", r"\btrial\b", r"\bdiscount", r"\bsale\b", r"\bbest\b", r"\btop[- ]rated\b", r"\baward",
           r"\bnumber one\b", r"\bmillions? of\b", r"\bdownloads?\b", r"\bstars?\b", r"\brated\b", r"\bofficial\b",
           r"\blicensed\b", r"\bfor a limited time\b"],
    "tr": [r"ücretsiz", r"bedava", r"deneme sürümü", r"\bdeneme\b", r"indirim", r"\ben iyi (?:yarış )?oyun", r"bir numara", r"resmi",
           r"lisanslı", r"milyon"],
    "de": [r"kostenlos", r"gratis", r"testversion", r"probe(?:abo|version|zeitraum)", r"rabatt", r"\bbeste[nrs]? (?:renn)?spiel",
           r"nummer ?1", r"offiziell", r"lizenziert", r"millionen"],
    "es": [r"gratis", r"gratuit", r"prueba gratuita", r"per[ií]odo de prueba", r"versi[oó]n de prueba", r"descuento",
           r"mejor juego", r"n[uú]mero 1", r"oficial", r"licencia", r"millones"],
    "fr": [r"gratuit", r"essai gratuit", r"p[ée]riode d'essai", r"version d'essai", r"r[ée]duction", r"\bpromo",
           r"meilleur jeu", r"n° ?1", r"officiel", r"sous licence", r"millions"],
    "it": [r"gratis", r"gratuit", r"prova gratuita", r"periodo di prova", r"sconto", r"miglior(?:e)? gioco",
           r"numero 1", r"ufficial", r"licenza", r"milioni"],
    "pt": [r"gr[aá]tis", r"gratuit", r"teste gr[aá]tis", r"avalia[cç][aã]o gratuita", r"per[ií]odo de teste",
           r"desconto", r"melhor jogo", r"n[uú]mero 1", r"oficial", r"licenciad", r"milh[oõ]es"],
    "ru": [r"бесплатн", r"пробн", r"скидк", r"лучш\w* игр", r"номер ?1", r"официальн", r"лицензи", r"миллион"],
    "ja": [r"無料", r"体験版", r"お試し", r"割引", r"セール", r"最高のゲーム", r"ナンバーワン", r"公式", r"ライセンス", r"万人", r"ダウンロード数"],
    "ko": [r"무료", r"체험", r"할인", r"세일", r"최고의 게임", r"1위", r"공식", r"라이선스", r"만 명", r"다운로드 수"],
    "zh": [r"免费", r"试用", r"折扣", r"促销", r"最好的游戏", r"第一名", r"官方", r"授权", r"百万", r"下载量"],
    "hi": [r"मुफ़्त", r"मुफ्त", r"निःशुल्क", r"ट्रायल", r"आज़माइश", r"छूट", r"डिस्काउंट", r"सेल", r"नंबर 1", r"आधिकारिक", r"लाइसेंस", r"करोड़"],
}
_BANNED = {k: [re.compile(p, re.I) for p in v] for k, v in BANNED.items()}


def lang_of(locale):
    return locale.replace("_", "-").split("-")[0].lower()


def ip_hits(text):
    hits = []
    for line in text.split("\n"):
        norm = normalize(line)
        for raw, sub, rx in ENTRIES:
            if (sub is not None and sub in norm) or (rx is not None and rx.search(norm)):
                hits.append("ip:" + raw)
    return hits


def banned_hits(text, lang):
    hits = []
    for rx in _BANNED["*"] + _BANNED.get(lang, []):
        m = rx.search(text)
        if m:
            hits.append("banned:" + m.group(0))
    return hits


def problems(text, locale):
    return ip_hits(text) + banned_hits(text, lang_of(locale))


def self_test():
    """The lint must catch planted words (separator, accent, leetspeak, non-Latin, trial, price, claim)."""
    # banned words are assembled at run time so this file stays clean for tests/ip_denylist.gd
    planted = [("en", "Start your free " + "trial"), ("de", "Jetzt kostenlos"), ("en", "Only $0.99"),
               ("en", "The #1 tycoon game"), ("fr", "Essai gratuit"), ("ru", "Бесплатно"), ("ko", "무료 게임"), ("zh", "免费" + "试玩")]
    innocent = [("en", "From one hatchback to an empire"), ("fr", "Du garage au QG souterrain"),
                ("es", "Elige tu ruta"), ("tr", "Tek arabadan imparatorluğa"), ("zh", "选择路线")]
    missed = [t for l, t in planted if not problems(t, l)]
    false = [t for l, t in innocent if problems(t, l)]
    return missed, false


if __name__ == "__main__":
    m, f = self_test()
    print("LINT_SELFTEST", "PASS" if not m and not f else f"FAIL missed={m} false={f}")
    if m or f:
        raise SystemExit(1)


# ---------------------------------------------------------------------------------------------------- slide copy
CJK = ("ja", "ko", "zh")
# rendered-length budgets (characters): pill tag, each headline line, benefit chip; CJK glyphs are ~2x wide
COPY_LIMITS = {"tag": 26, "line": 18, "benefit": 40}
SHOT_IDS = ["01-empire", "02-dispatch", "03-event", "04-fleet", "05-upgrades", "06-map", "07-contracts", "08-frost"]


def copy_problems(words, locale):
    lang = lang_of(locale)
    k = 2 if lang in CJK else 1
    errs = []
    if set(words) != {"category", "feature", "slides"} or len(words["slides"]) != len(SHOT_IDS) or len(words["feature"]) != 2:
        return ["structure"]
    if any(len(s) != 3 for s in words["slides"]):
        return ["slide structure"]
    tags = [words["category"]] + [s[0] for s in words["slides"]]
    heads = [words["feature"][0]] + [s[1] for s in words["slides"]]
    bens = [words["feature"][1]] + [s[2] for s in words["slides"]]
    for t in tags:
        if len(t) * k > COPY_LIMITS["tag"] or "\n" in t:
            errs.append(f"tag too long: {t}")
    for h in heads:
        lines = h.split("\n")
        if len(lines) != 2 or any(not x.strip() for x in lines):
            errs.append(f"headline needs exactly two lines: {h!r}")
        for x in lines:
            if len(x) * k > COPY_LIMITS["line"]:
                errs.append(f"headline line too long: {x}")
            if lang in CJK and len(x) < 2:
                errs.append(f"orphan CJK line: {x}")
    for b in bens:
        if len(b) * k > COPY_LIMITS["benefit"] or "\n" in b:
            errs.append(f"benefit too long: {b}")
    errs += problems("\n".join(tags + heads + bens), locale)
    return errs


if __name__ == "__main__" and len(__import__("sys").argv) > 2:
    import json as _j
    import sys as _s
    _f, _loc = _s.argv[1], _s.argv[2]
    _e = copy_problems(_j.loads(Path(_f).read_text(encoding="utf-8")), _loc)
    print(("COPY_FAIL " + _loc + ": " + "; ".join(_e)) if _e else f"COPY_OK {_loc}")
