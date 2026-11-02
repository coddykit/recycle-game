#!/usr/bin/env python3
"""Offline store checks for Dino Park: Ridgeback Rangers (ridgeback-rangers/) — Play listing + screenshot copy. No store / API access.
Called by unity/Tools/check.sh.

  python3 ridgeback-rangers/store/validate.py      # exit 0 = STORE_LISTING_PASS, else a FAIL list + exit 1

Thin config: all logic (limits, per-language banned wording in the 10 locales, title = "<keyword>: Ridgeback Rangers", ad-removal product
names from iap.json, privacy URL last, screenshot limits, Data safety CSV, store art sizes) is in
tools/store_templates/store_lint.py. Word lists below: banned_all = proper nouns (every locale), banned_en* = English only,
banned_lang = per-language lists. Plan #184 originality list; 13+ page, no child-directed wording.
"""
import re
import sys
from pathlib import Path

_here = Path(__file__).resolve()
sys.path.insert(0, str(next(p / "tools/store_templates" for p in _here.parents if (p / "tools/store_templates/store_lint.py").exists())))
import store_lint  # noqa: E402

def en_listing(ctx, full):
    ctx.check(re.search(r"^[A-Z][A-Z ,&'-]{5,}$", full, re.M) is not None, "en-US: no CAPS section headers")
    ctx.check(re.search(r"^- ", full, re.M) is not None, "en-US: no '- ' bullets")


# 13+ store page: no child-directed marketing (plan R4); dinosaurs already attract children, the copy must not invite them
CHILD = {
    "en": [r"kids?", r"child(?:ren)?", r"toddlers?", r"preschool\w*", r"cute", r"cutest", r"bab(?:y|ies)", r"little\s+ones",
           r"for\s+all\s+ages", r"family[\s-]friendly"],
    "tr": [r"çocuk\w*", r"bebek\w*", r"küçükler\w*", r"minik\w*", r"sevimli", r"her\s+yaş\w*"],
    "de": [r"kinder\w*", r"kind", r"kleinkind\w*", r"niedlich\w*", r"baby\w*", r"für\s+alle\s+altersgruppen", r"familienfreundlich"],
    "es": [r"ni[ñn][oa]s?", r"infantil\w*", r"beb[ée]s?", r"para\s+todas\s+las\s+edades"],
    "fr": [r"enfants?", r"b[ée]b[ée]s?", r"mignon\w*", r"tout-petits", r"pour\s+tous\s+les\s+[âa]ges"],
    "pt": [r"crian[çc]as?", r"infantil\w*", r"beb[êe]s?", r"fofo\w*", r"para\s+todas\s+as\s+idades"],
    "ru": [r"дет(?:и|ей|ям|ьми|ях|ск\w*)", r"ребён\w*", r"ребен\w*", r"малыш\w*", r"мил(?:ый|ые|ая)"],
    "ja": [r"子ども", r"子供", r"こども", r"キッズ", r"赤ちゃん", r"幼児", r"かわいい", r"可愛い", r"全年齢"],
    "ko": [r"어린이", r"아이들", r"아기", r"유아", r"키즈", r"귀여운", r"전\s*연령"],
    "hi": [r"बच्च\w*", r"बेबी", r"प्यारा", r"प्यारे", r"सभी\s+उम्र"],
}

CFG = {
    "file": __file__,
    "brand": "Ridgeback Rangers",
    # plan #184 originality list + reference / competitor games + real brands
    "banned_all": [
        r"jurass\w*", r"jurás\w*", r"juráss\w*", r"юрск\w*", r"ジュラ", r"쥬라기", r"쥐라기", r"जुरासिक", r"spared\s+no\s+expense",
        r"life\s+finds\s+a\s+way", r"isla\s+nublar", r"isla\s+sorna", r"\bingen\b", r"hammond", r"codigames", r"saurian",
        r"\bdino\s+park\s+land\b", r"idle\s+dinosaur", r"dino\s+tycoon\b", r"planet\s+zoo", r"zoo\s+tycoon",
        r"prehistoric\s+kingdom", r"parkasaurus", r"zoo\s+craft", r"zoo\s+life", r"idle\s+zoo", r"critter\s+crew", r"\bjeep\b",
        r"land\s+rover", r"national\s+geographic", r"nat\s*geo", r"discovery\s+channel", r"smithsonian",
        r"natural\s+history\s+museum", r"\bdisney\b", r"universal\s+studios",
    ],
    "banned_lang": {"child-directed wording (13+ page)": CHILD},
    "generic_off": ("gift",),  # only "gift card" is offer wording here
    "banned_en": [r"\bgift\s+cards?\b"],
    "en_listing": en_listing,
}

if __name__ == "__main__":
    store_lint.run(CFG)
