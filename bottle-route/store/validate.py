#!/usr/bin/env python3
"""Offline store checks for Recycling Tycoon: Bottle Route (bottle-route/) — Play listing + screenshot copy. No store / API access.
Called by unity/Tools/check.sh.

  python3 bottle-route/store/validate.py      # exit 0 = STORE_LISTING_PASS, else a FAIL list + exit 1

Thin config: all logic (limits, per-language banned wording in the 10 locales, title = "<keyword>: Bottle Route", ad-removal product
names from iap.json, privacy URL last, screenshot limits, Data safety CSV, store art sizes) is in
tools/store_templates/store_lint.py. Word lists below: banned_all = proper nouns (every locale), banned_en* = English only,
banned_lang = per-language lists. Plan originality rule: real schemes / agencies / retailers, competitor + reference titles.
"""
import json
import re
import sys
from pathlib import Path

_here = Path(__file__).resolve()
sys.path.insert(0, str(next(p / "tools/store_templates" for p in _here.parents if (p / "tools/store_templates/store_lint.py").exists())))
import store_lint  # noqa: E402

def en_listing(ctx, full):
    ctx.check(len(re.findall(r"^[A-Z][A-Z &'\-]{3,}$", full, re.M)) >= 4, "en-US fullDescription needs CAPS section headers")
    ctx.check(len(re.findall(r"^- ", full, re.M)) >= 8, "en-US fullDescription needs '-' bullets")


def extra(ctx):
    ids = [s.get("id") for s in json.loads((ctx.root / "screenshots/en.json").read_text(encoding="utf-8")).get("shots", [])]
    ctx.check(ids == [f"s{i}" for i in range(1, 9)], "screenshots/en.json: need 8 shots s1..s8, got", ids)


CFG = {
    "file": __file__,
    "brand": "Bottle Route",
    # real scheme / agency / retailer names, competitor + reference titles (RECYCLING_GAME_PLAN.md header + section 3 + 11)
    "banned_all": [
        r"TÜÇA", r"Türkiye\s+Çevre\s+Ajans\w*", r"sıfır\s*atık", r"carrefour", r"migros", r"return\s+and\s+earn",
        r"return\s*&\s*earn", r"tomra", r"deutsche\s+pfand\w*", r"trash\s+tycoon", r"recycle\s+tycoon", r"recycle\s+empire",
        r"recyclings?\s+run", r"garbage\s+truck\s+simulator", r"borderline\s+empire",
    ],
    # acronyms / short names that are ordinary words in other languages when lower-cased (Turkish "şok", Portuguese "doa")
    "banned_all_cs": [r"\bTUCA\b", r"\bB[İI]M\b", r"\bŞOK\b", r"\bA101\b", r"\bDOA\b", r"\bD\.O\.A\b", r"\bDPG\b"],
    "banned_en": [r"environment\s+agency", r"\bministry\b", r"zero\s+waste\s+mark", r"\bprice\s+tag\b", r"\bpricing\b"],
    "banned_lang": {"ministry wording": {"tr": [r"bakanl\w*"]}},
    "generic_off": ("price",),  # the copy may talk about prices in-game; only "price tag" / "pricing" are offer wording
    "en_listing": en_listing,
    "extra": extra,
}

if __name__ == "__main__":
    store_lint.run(CFG)
