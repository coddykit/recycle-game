#!/usr/bin/env python3
"""Volt City copy gate: no "free" claim in player-facing text, in any of the 10 languages (fleet rule: no free / trial
wording; Patron #172 10-29: "free power when windy" slipped through). Scans the strings table (every column) and the IAP
catalogue's product names / descriptions. Allowed: "ad-free" (a product feature, not a price claim).

    python3 games/volt-city/unity/Tools/copy_gate.py      # exit 1 + every hit
"""
import csv
import json
import re
import sys
from pathlib import Path

UNITY = Path(__file__).resolve().parent.parent
BANNED = re.compile(
    r"\bfree\b|\bfor free\b|\btrial\b|ücretsiz|bedava|kostenlos|\bgratis\b|gratuit|grátis|gratuito|бесплатн|無料|무료|मुफ़्त|मुफ्त|मुफत",
    re.IGNORECASE)
ALLOWED = re.compile(r"\bad[- ]free\b", re.IGNORECASE)


def hits_in(text):
    return BANNED.findall(ALLOWED.sub("", re.sub(r"\{[a-z_]+\}", "", text or "")))


def main():
    bad = []
    table = UNITY / "Assets/Resources/i18n/strings.csv"
    rows = list(csv.reader(table.open(encoding="utf-8")))
    head = rows[0]
    for r in rows[1:]:
        for lang, text in zip(head[1:], r[1:]):
            h = hits_in(text)
            if h:
                bad.append(f"strings.csv {r[0]} [{lang}] {h}: {text[:80]}")
    iap = json.loads((UNITY / "Assets/Resources/data/iap.json").read_text(encoding="utf-8"))
    prods = iap.get("products", {})
    for slug, p in (prods.items() if isinstance(prods, dict) else [(x.get("slug"), x) for x in prods]):
        p = dict(p, slug=slug)
        for field in ("name", "title", "desc", "description"):
            v = p.get(field)
            items = v.items() if isinstance(v, dict) else ([("*", v)] if isinstance(v, str) else [])
            for lang, text in items:
                h = hits_in(text)
                if h:
                    bad.append(f"iap.json {p.get('slug', p.get('id'))}.{field} [{lang}] {h}: {text[:80]}")
    for b in bad:
        print("FAIL " + b)
    print(f"COPY_GATE: {len(rows) - 1} keys x {len(head) - 1} languages + IAP texts, {len(bad)} banned-word hits")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
