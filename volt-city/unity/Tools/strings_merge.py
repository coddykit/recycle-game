#!/usr/bin/env python3
"""Volt City strings table tool (Assets/Resources/i18n/strings.csv: key,en,tr,de,es,fr,pt,ru,ja,ko,hi).

  python3 games/volt-city/unity/Tools/strings_merge.py --check                 # gate: every cell filled, placeholders = en
  python3 games/volt-city/unity/Tools/strings_merge.py --merge de=/path/de.json  # fill one language column from key->text JSON
  python3 games/volt-city/unity/Tools/strings_merge.py --export /dir            # write en.json (+ every lang) for translators

Translations come from in-session sub-agents only (games/CLAUDE.md section 9: no OpenRouter / external MT / API).
"""
import csv
import io
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
TABLE = HERE.parent / "Assets/Resources/i18n/strings.csv"
LANGS = ["en", "tr", "de", "es", "fr", "pt", "ru", "ja", "ko", "hi"]
PH = re.compile(r"\{([a-z_]+)\}")
TRIAL = re.compile(r"free\s+trial|try\s+(it\s+)?free", re.I)


def load():
    rows = list(csv.reader(io.StringIO(TABLE.read_text(encoding="utf-8-sig"))))
    head = rows[0]
    assert head[:1] == ["key"] and head[1:] == LANGS, f"header must be key,{','.join(LANGS)}"
    return [r + [""] * (len(head) - len(r)) for r in rows[1:] if r and r[0] and not r[0].startswith("#")]


def save(rows):
    out = io.StringIO()
    w = csv.writer(out, lineterminator="\n")
    w.writerow(["key"] + LANGS)
    for r in rows:
        w.writerow(r[: 1 + len(LANGS)])
    TABLE.write_text(out.getvalue(), encoding="utf-8")


def check(rows):
    problems = []
    seen = set()
    for r in rows:
        k = r[0]
        if k in seen:
            problems.append(f"duplicate {k}")
        seen.add(k)
        en_ph = sorted(PH.findall(r[1]))
        for i, lang in enumerate(LANGS):
            v = r[1 + i]
            if not v.strip():
                problems.append(f"empty {lang}:{k}")
                continue
            if sorted(PH.findall(v)) != en_ph:
                problems.append(f"placeholders {lang}:{k}: {v!r}")
            if TRIAL.search(v):
                problems.append(f"trial wording {lang}:{k}")
    return problems


def main(argv):
    rows = load()
    if "--merge" in argv:
        for spec in argv[argv.index("--merge") + 1:]:
            if spec.startswith("--"):
                break
            lang, path = spec.split("=", 1)
            col = 1 + LANGS.index(lang)
            data = json.loads(Path(path).read_text(encoding="utf-8"))
            n = 0
            for r in rows:
                if r[0] in data and isinstance(data[r[0]], str):
                    r[col] = data[r[0]].strip()
                    n += 1
            extra = sorted(set(data) - {r[0] for r in rows})
            print(f"merged {lang}: {n} cells" + (f", ignored unknown keys {extra[:5]}" if extra else ""))
        save(rows)
    if "--export" in argv:
        d = Path(argv[argv.index("--export") + 1])
        d.mkdir(parents=True, exist_ok=True)
        for i, lang in enumerate(LANGS):
            (d / f"{lang}.json").write_text(json.dumps({r[0]: r[1 + i] for r in rows}, ensure_ascii=False, indent=1), encoding="utf-8")
        print("exported", len(rows), "keys ->", d)
    probs = check(rows)
    for p in probs[:40]:
        print("FAIL", p)
    print(f"STRINGS: {len(rows)} keys x {len(LANGS)} languages, {len(probs)} problems")
    return 1 if probs and "--check" in argv else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
