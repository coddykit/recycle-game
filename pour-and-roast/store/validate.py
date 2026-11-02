#!/usr/bin/env python3
"""Offline store checks for Cafe Manager: Pour & Roast (pour-and-roast/) — Play listing + screenshot copy. No store / API access.
Called by unity/Tools/check.sh.

  python3 pour-and-roast/store/validate.py      # exit 0 = STORE_LISTING_PASS, else a FAIL list + exit 1

Thin config: all logic (limits, per-language banned wording in the 10 locales, title = "<keyword>: Pour & Roast", ad-removal product
names from iap.json, privacy URL last, screenshot limits, Data safety CSV, store art sizes) is in
tools/store_templates/store_lint.py. Word lists below: banned_all = proper nouns (every locale), banned_en* = English only,
banned_lang = per-language lists. Plan 1, 12, 13 (risk 6). `--all` = release gate, all 10 locales (now the default).
"""
import sys
from pathlib import Path

_here = Path(__file__).resolve()
sys.path.insert(0, str(next(p / "tools/store_templates" for p in _here.parents if (p / "tools/store_templates/store_lint.py").exists())))
import store_lint  # noqa: E402

CFG = {
    "file": __file__,
    "brand": "Pour & Roast",
    # real coffee chains / roasters, the reference game and its publisher, the market competitors named in the plan, sibling-game names
    "banned_all": [
        r"starbucks", r"costa\s+coffee", r"tim\s+hortons", r"dunkin", r"nescaf[eé]", r"nespresso", r"lavazza", r"\billy\b",
        r"peet'?s", r"blue\s+bottle", r"caribou\s+coffee", r"gloria\s+jean", r"second\s+cup", r"pret\s+a\s+manger", r"tchibo",
        r"juan\s+valdez", r"caf[eé]\s+coffee\s+day", r"kahve\s+d[üu]nyas[ıi]", r"segafredo", r"kimbo", r"jacobs", r"doutor",
        r"tully'?s", r"ediya", r"mega\s*coffee", r"\bpaik", r"codigames", r"idle\s+coffee\s+shop",
        r"coffee\s+shop\s+(?:empire|tycoon\s*-\s*idle)", r"empire\s+tycoon", r"coffee\s+break", r"good\s+coffee,?\s+great\s+coffee",
        r"my\s+coffee\s+shop", r"own\s+coffee\s+shop", r"coffee\s+craze", r"coffee\s+shop\s+simulator",
        r"coffee\s+roaster\s+empire", r"supercent", r"tapblaze", r"\btapps\b", r"owngames", r"five\s*amp", r"coffee\s+rush",
        r"rush\s+hour\s+caf", r"crema\s+co", r"\btapri\b", r"chaiwala",
    ],
    # the reference's signature words (orchard / garden / event cafe / oven task cards) and sibling-game wording (plan 12: no
    # "rush / stall" copy) — common English words, so English only
    "banned_en": [r"\brush\b", r"\bstall\b", r"orchard", r"\bgarden\b", r"event\s+caf", r"oven\s+task", r"\bprice\s*:", r"\bpricing\b"],
    "generic_off": ("price", "trial_word"),
}

if __name__ == "__main__":
    store_lint.run(CFG)
