"""Fill the Turkish column of translations/strings.csv from tr_strings.py.
Run after extract_strings.py:  python tools/i18n/apply_translations.py"""
import csv
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from tr_strings import TR  # noqa: E402

CSV = os.path.normpath(os.path.join(HERE, "..", "..", "translations", "strings.csv"))
PH = re.compile(r"%[-+0-9.]*[sdf]")


def main():
    rows = list(csv.DictReader(open(CSV, encoding="utf-8")))
    missing = []
    for r in rows:
        k = r["keys"]
        if k in TR:
            r["tr"] = TR[k]
            if PH.findall(k.replace("%%", "")) != PH.findall(TR[k].replace("%%", "")):
                # placeholder order must match for Godot's % formatting
                print("PLACEHOLDER MISMATCH:", repr(k), "->", repr(TR[k]))
        elif not r.get("tr"):
            missing.append(k)
    with open(CSV, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0]))
        w.writeheader()
        w.writerows(rows)
    print("%d rows, %d missing Turkish" % (len(rows), len(missing)))
    for m in missing:
        print("  missing:", repr(m))


if __name__ == "__main__":
    main()
