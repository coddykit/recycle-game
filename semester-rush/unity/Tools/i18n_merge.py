#!/usr/bin/env python3
"""Semester Rush — builds Assets/Resources/i18n/strings.csv (key,en,tr,de,es,fr,pt,ru,ja,ko,hi) from Tools/i18n/en.json
(the source) + Tools/i18n/<lang>.json (in-session translations, fleet §9: Claude Haiku sub-agents; no external API).
A missing / empty translation stays empty in the CSV (the game falls back to English; the i18n test reports it).
  python3 semester-rush/unity/Tools/i18n_merge.py [--check]
"""
import csv, io, json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
UNITY = os.path.dirname(HERE)
LANGS = ["en", "tr", "de", "es", "fr", "pt", "ru", "ja", "ko", "hi"]
OUT = os.path.join(UNITY, "Assets/Resources/i18n/strings.csv")

def load(lang):
    p = os.path.join(HERE, "i18n", lang + ".json")
    return json.load(open(p, encoding="utf-8")) if os.path.exists(p) else {}

def build():
    tables = {l: load(l) for l in LANGS}
    en = tables["en"]
    buf = io.StringIO()
    w = csv.writer(buf, lineterminator="\n")
    w.writerow(["key"] + LANGS)
    missing = {l: 0 for l in LANGS[1:]}
    for k in sorted(en):
        row = [k]
        for l in LANGS:
            v = tables[l].get(k, "") if l != "en" else en[k]
            if l != "en" and not v: missing[l] += 1
            row.append(v)
        w.writerow(row)
    return buf.getvalue(), missing

text, missing = build()
if "--check" in sys.argv:
    cur = open(OUT, encoding="utf-8").read() if os.path.exists(OUT) else ""
    print("I18N_CHECK:", "PASS" if cur == text else "FAIL (run i18n_merge.py)", "missing:", missing)
    sys.exit(0 if cur == text else 1)
os.makedirs(os.path.dirname(OUT), exist_ok=True)
open(OUT, "w", encoding="utf-8").write(text)
print("strings.csv written; missing per language:", missing)
