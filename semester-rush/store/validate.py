#!/usr/bin/env python3
"""Offline store checks for College Tycoon: Semester Rush (semester-rush/) — Play listing + screenshot copy. No store / API access.
Called by unity/Tools/check.sh.

  python3 semester-rush/store/validate.py      # exit 0 = STORE_LISTING_PASS, else a FAIL list + exit 1

Thin config: all logic (limits, per-language banned wording in the 10 locales, title = "<keyword>: Semester Rush", ad-removal product
names from iap.json, privacy URL last, screenshot limits, Data safety CSV, store art sizes) is in
tools/store_templates/store_lint.py. Word lists below: banned_all = proper nouns (every locale), banned_en* = English only,
banned_lang = per-language lists. Plan section 3 originality list.
"""
import sys
from pathlib import Path

_here = Path(__file__).resolve()
sys.path.insert(0, str(next(p / "tools/store_templates" for p in _here.parents if (p / "tools/store_templates/store_lint.py").exists())))
import store_lint  # noqa: E402

def en_listing(ctx, full):
    for h in ("PLAN THE WEEK", "BEAT THE BELL", "HANDLE THE CHAOS", "GROW YOUR CAMPUS", "GOOD TO KNOW"):
        ctx.check("\n" + h + "\n" in full, "en-US section header missing:", h)
    ctx.check("Other purchases do not remove ads" in full, "en-US: the cheap-packs line (root CLAUDE.md section 3) is missing")


CFG = {
    "file": __file__,
    "brand": "Semester Rush",
    # reference game / competitors (plan sections 1 + 3), real universities / schemes / brands
    "banned_all": [
        r"codigames", r"university\s+empire", r"empire\s+tycoon", r"two[\s-]*point", r"idle\s+university", r"university\s+idle",
        r"harvard", r"oxford", r"cambridge", r"stanford", r"\byale\b", r"princeton", r"sorbonne", r"berkeley",
        r"columbia\s+university", r"ivy\s+league", r"oxbridge", r"heidelberg", r"tsinghua", r"peking\s+university", r"todai",
        r"東京大学", r"東大", r"서울대", r"seoul\s+national", r"boğaziçi", r"bogazici", r"\bodtü\b", r"\bmetu\b", r"hogwarts",
        r"erasmus", r"fulbright", r"pell\s+grant", r"fafsa", r"rhodes\s+scholar",
    ],
    # short university acronyms, case-sensitive and English only (German "mit", Turkish / Spanish / French words collide)
    "banned_en_cs": [r"\b(?:MIT|UCLA|NYU|LSE|ETH|IIT|EPFL|KAIST|CALTECH|Caltech)\b"],
    "en_listing": en_listing,
}

if __name__ == "__main__":
    store_lint.run(CFG)
