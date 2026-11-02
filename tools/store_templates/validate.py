#!/usr/bin/env python3
"""Offline store checks for <GAME TITLE> (<game-dir>/) — Play listing + screenshot copy. No store / API access.

  python3 <game-dir>/store/validate.py      # exit 0 = STORE_LISTING_PASS (unity/Tools/check.sh runs it)

TEMPLATE for new games: copy to <game-dir>/store/validate.py, set BRAND and the game-specific lists below, nothing else.
All logic (limits, per-language banned wording for free / trial / price / discount / gift / ranking in the 10 locales, title =
"<keyword>: <brand>", ad-removal product names from iap.json, privacy URL last, screenshot limits, Data safety CSV, store art
sizes) lives in tools/store_templates/store_lint.py.

Where a word list goes (see store_lint.py docstring):
  banned_all / banned_all_cs  proper nouns (real brands, schemes, competitor + reference-game names): every locale
  banned_en / banned_en_cs    English-only patterns (common words, acronyms such as "MIT" that are ordinary words elsewhere)
  banned_lang                 {label: {lang: [fragments]}} per-language word lists (medical claims, child-directed words, ...)
  generic_off                 generic categories this game's copy legitimately needs: "price", "discount", "gift",
                              "trial_word" (bare "trial"), "symbols"
  required                    locales that must exist (default: all 10; use {"en-US"} until the central translation lands)
"""
import sys
from pathlib import Path

_here = Path(__file__).resolve()
sys.path.insert(0, str(next(p / "tools/store_templates" for p in _here.parents if (p / "tools/store_templates/store_lint.py").exists())))
import store_lint  # noqa: E402

CFG = {
    "file": __file__,
    "brand": "Your Brand",  # title must end with ": Your Brand" (owner ASO rule: keyword first, brand last)
    "banned_all": [
        # real brands / schemes / competitor + reference-game names from the plan's originality list
        r"example\s+brand",
    ],
    "banned_all_cs": [],
    "banned_en": [],
    "banned_en_cs": [],
    "banned_lang": {},
    "generic_off": (),
    # "required": {"en-US"},
    # "en_listing": lambda ctx, full: ctx.check(...),   # en-US-only style checks (CAPS headers, '- ' bullets ...)
    # "extra": lambda ctx: ...,                          # any further game-specific check
}

if __name__ == "__main__":
    store_lint.run(CFG)
