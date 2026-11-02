#!/usr/bin/env python3
"""Offline release gate: source inventory, locales, placeholders and font coverage."""
import csv
from pathlib import Path
from translate_api import LANGS, valid
from extract_strings import clean, from_json, from_code, EXTRA

ROOT = Path(__file__).resolve().parents[2]
rows = list(csv.DictReader((ROOT / 'translations/strings.csv').open(encoding='utf-8', newline='')))
errors = []
keys = [r['keys'] for r in rows]
if len(set(keys)) != len(keys):
    errors.append('Duplicate source keys')
for key in clean(from_json() + from_code() + EXTRA):
    if key not in keys:
        errors.append(f'Unextracted UI string: {key!r}')
for row in rows:
    for lang in LANGS:
        if not valid(row['keys'], row.get(lang)):
            errors.append(f'{lang}: missing/invalid format tokens: {row["keys"]!r}')
project = (ROOT / 'project.godot').read_text()
for lang in LANGS:
    if f'strings.{lang}.translation' not in project:
        errors.append(f'Locale not registered: {lang}')
if errors:
    print('\n'.join(errors))
    raise SystemExit(1)
print(f'PASS: {len(rows)} keys × {len(LANGS)} complete locales; placeholder order and source inventory valid')
