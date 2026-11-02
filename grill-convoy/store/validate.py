#!/usr/bin/env python3
"""Offline store checks for Grill Convoy: Burger Truck Manager (grill-convoy/) — Play listing + screenshot copy. No store / API access.
Called by unity/Tools/check.sh.

  python3 grill-convoy/store/validate.py      # exit 0 = STORE_LISTING_PASS, else a FAIL list + exit 1

Thin config: all logic (limits, per-language banned wording in the 10 locales, title = "<keyword>: Grill Convoy", ad-removal product
names from iap.json, privacy URL last, screenshot limits, Data safety CSV, store art sizes) is in
tools/store_templates/store_lint.py. Word lists below: banned_all = proper nouns (every locale), banned_en* = English only,
banned_lang = per-language lists. Plan BURGER_GAME_PLAN.md originality rule + section 11.
"""
import re
import sys
from pathlib import Path

_here = Path(__file__).resolve()
sys.path.insert(0, str(next(p / "tools/store_templates" for p in _here.parents if (p / "tools/store_templates/store_lint.py").exists())))
import store_lint  # noqa: E402

def en_listing(ctx, full):
    ctx.check(len(re.findall(r"^[A-Z0-9][A-Z0-9 ,&'-]{3,}$", full, re.M)) >= 3, "en-US needs >= 3 CAPS section headers")


CFG = {
    "file": __file__,
    "brand": "Grill Convoy",
    # real chains / marks, the reference game + its developer, competitor titles named in the plan
    "banned_all": [
        r"mc\s*donald'?s", r"burger\s*king", r"wendy'?s", r"five\s*guys", r"shake\s*shack", r"in-n-out", r"whopper", r"big\s*mac",
        r"happy\s*meal", r"carl'?s\s*jr", r"hardee'?s", r"jack\s+in\s+the\s+box", r"sonic\s+drive", r"white\s*castle",
        r"smashburger", r"fatburger", r"culver'?s", r"kfc", r"subway", r"domino'?s", r"pizza\s*hut", r"taco\s*bell",
        r"マクドナルド", r"バーガーキング", r"맥도날드", r"버거킹", r"मैकडॉनल्ड", r"बर्गर\s*किंग", r"макдоналдс", r"бургер\s*кинг",
        r"codigames", r"idle\s+burger\s+empire", r"burger\s+empire\s+tycoon", r"cooking\s+fever", r"food\s+truck\s+chef",
        r"burger\s+truck\s+chicago", r"burger\s+rush", r"patty\s+rush", r"good\s+pizza", r"overcooked", r"diner\s+dash",
    ],
    "shot_keys": {"id", "headline", "sub", "scene"},
    "en_listing": en_listing,
}

if __name__ == "__main__":
    store_lint.run(CFG)
