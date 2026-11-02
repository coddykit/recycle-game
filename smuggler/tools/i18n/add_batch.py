#!/usr/bin/env python3
"""Appends reviewed translations to translations/strings.csv without touching the other rows.
Usage: python3 tools/i18n/add_batch.py docs/qa/quality-2026-10-18/i18n_batch_1.json
The JSON maps an English key to {tr, de, es, fr, ja, ko, pt, ru, zh} (+ optional en, default = the key);
every language is required. Keys such as "tab.upgrades" (short caption variants) carry their own `en` text.
Existing keys are updated in place, new keys are appended. Then run Godot --headless --import to rebuild the
.translation files and python3 tools/i18n/validate.py."""
import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CSV = ROOT / 'translations/strings.csv'
LANGS = ['en', 'tr', 'de', 'es', 'fr', 'ja', 'ko', 'pt', 'ru', 'zh']


def main(batch_path):
    batch = json.loads(Path(batch_path).read_text(encoding='utf-8'))
    with CSV.open(encoding='utf-8', newline='') as f:
        rows = list(csv.DictReader(f))
    index = {r['keys']: r for r in rows}
    for key, tr in batch.items():
        missing = [l for l in LANGS[1:] if not str(tr.get(l, '')).strip()]
        if missing:
            raise SystemExit(f'{key!r}: missing {missing}')
        row = index.get(key)
        if row is None:
            row = {'keys': key}
            rows.append(row)
            index[key] = row
        row['en'] = tr.get('en', key)
        for l in LANGS[1:]:
            row[l] = tr[l]
    with CSV.open('w', encoding='utf-8', newline='') as f:
        w = csv.DictWriter(f, fieldnames=['keys'] + LANGS)
        w.writeheader()
        w.writerows(rows)
    print(f'{len(batch)} keys applied, {len(rows)} rows total')


if __name__ == '__main__':
    main(sys.argv[1])
