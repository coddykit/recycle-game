#!/usr/bin/env python3
"""Offline store checks for Law Firm Tycoon: Docket Day (docket-day/) — Play listing + screenshot copy. No store / API access.
Called by unity/Tools/check.sh.

  python3 docket-day/store/validate.py      # exit 0 = STORE_LISTING_PASS, else a FAIL list + exit 1

Thin config: all logic (limits, per-language banned wording in the 10 locales, title = "<keyword>: Docket Day", ad-removal product
names from iap.json, privacy URL last, screenshot limits, Data safety CSV, store art sizes) is in
tools/store_templates/store_lint.py. Word lists below: banned_all = proper nouns (every locale), banned_en* = English only,
banned_lang = per-language lists. "trial" is ordinary courtroom vocabulary here: only offer phrasings ("free trial", "trial version" ...) are banned, in every language.
"""
import re
import sys
from pathlib import Path

_here = Path(__file__).resolve()
sys.path.insert(0, str(next(p / "tools/store_templates" for p in _here.parents if (p / "tools/store_templates/store_lint.py").exists())))
import store_lint  # noqa: E402

def en_listing(ctx, full):
    ctx.check(re.search(r"^[A-Z][A-Z ,&'-]{3,}$", full, re.M) is not None, "en-US CAPS section headers missing")
    ctx.check(full.count("\n- ") >= 8, "en-US '-' bullets missing")


CFG = {
    "file": __file__,
    "brand": "Docket Day",
    # plan sections 1 / 11 / 13 originality list (reference game, its publisher, other law / court games) + real courts / firms / TV
    "banned_all": [
        r"codigames", r"law\s*empire", r"justice\s*empire", r"idle\s*law\s*firm", r"ace\s*attorney", r"phoenix\s*wright",
        r"devil'?s\s*attorney", r"icivics", r"lawyer\s*life", r"judge\s*3d", r"court\s*affairs", r"guilty!",
        r"choose\s+the\s+justice", r"supreme\s*court", r"baker\s*mckenzie", r"kirkland", r"skadden", r"latham",
        r"clifford\s*chance", r"freshfields", r"linklaters", r"dentons", r"allen\s*&\s*overy", r"judge\s*judy",
        r"better\s*call\s*saul", r"perry\s*mason",
    ],
    "generic_off": ("trial_word",),
    "en_listing": en_listing,
}

if __name__ == "__main__":
    store_lint.run(CFG)
