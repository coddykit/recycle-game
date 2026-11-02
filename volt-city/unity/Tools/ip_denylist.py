#!/usr/bin/env python3
"""Volt City IP / content denylist scanner (games/volt-city/IP_RULES.md section 5) — the Python port of the Grid Boss /
Driftwood ip_denylist.gd engine, so it runs without Godot or Unity (preflight, check.sh, Jenkins).

  python3 games/volt-city/unity/Tools/ip_denylist.py            # scan games/volt-city -> exit 0 only with 0 matches + proof
  python3 games/volt-city/unity/Tools/ip_denylist.py --proof    # proof fixture only

* list: Tools/ip_denylist.txt — plain entry = case-insensitive regex on word boundaries, "~entry" = substring (non-Latin),
  "#" = comment.
* normalisation (text AND entries): CamelCase split (RiversideSubstation -> riverside substation), lower case, accents folded (NFKD, combining
  marks dropped, full-width -> ASCII), leetspeak 0->o 3->e 4->a @->a $->s 5->s 7->t (never 1->i), separators
  (space - _ . / \\ middle dot) folded to one space.
* scope: every text file of games/volt-city (C#, USS/UXML/TSS, asmdef, json, Unity YAML .unity/.asset/.prefab/.mat,
  shaders/HLSL, md, py, sh, Jenkinsfile, csv, txt …) and EVERY file name (models, textures, scenes). FBX / GLB node and
  material names are covered by blender/validate_art.py (P2).
* excluded: the files that explain the rule (IP_RULES.md, docs/DESIGN.md, docs/PLAN.md, docs/STORE.md, store/NAME_CHECK.md, docs/NAME_CHECK.md),
  the store listing lint store/validate.py (its own brand denylist),
  the denylists themselves, and GENERATED / vendor folders (Library, Temp, Obj, Logs, Build(s), UserSettings, Packages
  caches, android/build; LL Driftwood #7).
* second check (IP_RULES section 4, "nobody is hurt"): every localisation text (i18n / Localization CSV or JSON) that names a
  person or an animal must not carry a harm word.
* proof: a temporary fixture (outside the project) with planted words assembled at run time — plain, separator, accent,
  leetspeak, CamelCase, non-Latin, rule name, file name — must all be caught, look-alike innocents must pass and a planted
  word inside a generated folder must be skipped.
"""
import csv
import json
import re
import shutil
import sys
import tempfile
import unicodedata
from pathlib import Path

HERE = Path(__file__).resolve().parent
GAME = HERE.parents[1]                      # games/volt-city
DENYLIST = HERE / "ip_denylist.txt"
EXCLUDE_FILES = {"IP_RULES.md", "docs/DESIGN.md", "docs/PLAN.md", "docs/STORE.md", "store/NAME_CHECK.md", "docs/NAME_CHECK.md", "store/validate.py",
                 "unity/Tools/ip_denylist.txt", "blender/ip_denylist_art.txt", "tests/ip_denylist.txt"}
EXCLUDE_DIRS = ("unity/Library", "unity/Temp", "unity/Obj", "unity/obj", "unity/Logs", "unity/Build", "unity/Builds",
                "unity/UserSettings", "unity/MemoryCaptures", "build", "android/build", ".godot", "__pycache__",
                # vendor SDKs copied unchanged (their text is not ours)
                "unity/Assets/GoogleMobileAds", "unity/Assets/ExternalDependencyManager", "unity/Assets/Plugins/Android/gradleOut")
TEXT_EXT = {"cs", "uss", "uxml", "tss", "asmdef", "asmref", "json", "unity", "asset", "prefab", "mat", "shader", "hlsl",
            "cginc", "shadergraph", "md", "txt", "csv", "py", "sh", "jenkinsfile", "yml", "yaml", "xml", "gradle", "pro",
            "properties", "env", "gitignore", "html", "svg", "po", "inputactions", "meta"}
API_TOKENS = []   # engine API identifiers that contain a denylisted word (normalised form); none needed so far
# Lines that STATE the rule (not content), per file: (relative path, substring of the raw line). Keep this list tiny.
ALLOW_LINES = [
    ("blender/build_art.py", "no nuc" + "lear plant."),   # P2 header comment quoting IP_RULES section 4
]
LEET = str.maketrans({"0": "o", "3": "e", "4": "a", "@": "a", "$": "s", "5": "s", "7": "t"})
CAMEL = re.compile(r"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")


def fold(text):
    out = []
    for ch in unicodedata.normalize("NFKD", text):
        if unicodedata.combining(ch):
            continue
        o = ord(ch)
        if 0xFF01 <= o <= 0xFF5E:
            ch = chr(o - 0xFEE0)
        out.append(ch)
    return "".join(out).replace("ß", "ss").replace("æ", "ae").replace("œ", "oe").replace("ø", "o").replace("ł", "l").replace("ı", "i").replace("đ", "d").replace("þ", "th")


def normalize(text, keep_regex=False):
    t = CAMEL.sub(" ", text)
    t = fold(t.lower()).translate(LEET)
    sep = r"[\s_\-/·]+" if keep_regex else r"[\s_.\-/\\·]+"
    return re.sub(sep, " ", t)


def load_entries(path=DENYLIST):
    entries = []
    for line in path.read_text(encoding="utf-8").splitlines():
        e = line.strip()
        if not e or e.startswith("#"):
            continue
        if e.startswith("~"):
            entries.append({"raw": e, "sub": normalize(e[1:]), "re": None})
        else:
            entries.append({"raw": e, "sub": None, "re": re.compile(r"\b(?:" + normalize(e, True) + r")\b")})
    return entries


def scan_text(text, entries):
    hits = []
    for n, line in enumerate(text.split("\n"), 1):
        norm = normalize(line)
        for tok in API_TOKENS:
            norm = norm.replace(tok, " ")
        for e in entries:
            if (e["sub"] is not None and e["sub"] in norm) or (e["re"] is not None and e["re"].search(norm)):
                hits.append((n, e["raw"], line.strip()[:120]))
    return hits


def excluded(rel):
    if rel in EXCLUDE_FILES:
        return True
    return any(rel == d or rel.startswith(d + "/") for d in EXCLUDE_DIRS)


def list_files(root):
    out = []
    for p in sorted(root.rglob("*")):
        rel = p.relative_to(root).as_posix()
        if any(part.startswith(".") and part not in (".gitignore",) for part in Path(rel).parts):
            continue
        if p.is_file() and not excluded(rel):
            out.append(rel)
    return out


def scan_tree(root, entries):
    hits, scanned = [], 0
    files = list_files(root)
    for rel in files:
        for _, raw, _ in scan_text(rel, entries):
            hits.append(f"{rel}:0 [{raw}] file name")
        ext = rel.rsplit(".", 1)[-1].lower() if "." in Path(rel).name else Path(rel).name.lower()
        if ext not in TEXT_EXT:
            continue
        try:
            text = (root / rel).read_text(encoding="utf-8")
        except (UnicodeDecodeError, OSError):
            continue   # binary content with a text extension (e.g. a binary .asset): its NAME was scanned
        scanned += 1
        for n, raw, line in scan_text(text, entries):
            if any(rel == f and snippet in line for f, snippet in ALLOW_LINES):
                continue
            hits.append(f"{rel}:{n} [{raw}] {line}")
    return hits, len(files), scanned


# ------------------------------------------------------------------------------------------------ nobody is hurt
SUBJECTS = {"patient", "patients", "people", "person", "resident", "residents", "family", "families", "crew", "crews", "worker",
            "workers", "lineman", "linemen", "driver", "drivers", "child", "children", "fan", "fans", "squirrel", "squirrels",
            "bird", "birds", "gull", "gulls", "owl", "owls", "cat", "cats", "dog", "dogs", "animal", "animals"}
HARM = ["die", "dies", "died", "dying", "dead", "death", "deaths", "kill", "killed", "kills", "hurt", "harm", "harmed", "injured",
        "injury", "injuries", "wounded", "fried", "electro" + "cuted", "zapped", "burned", "burnt", "in danger", "at risk"]


def harm_texts(lines):
    out = []
    for line in lines:
        norm = normalize(str(line))
        words = set(norm.split())
        if words & SUBJECTS and any((h in words) if " " not in h else (h in norm) for h in HARM):
            out.append(str(line)[:100])
    return out


def i18n_texts(root):
    texts = []
    for p in list_files(root):
        low = p.lower()
        if not ("/i18n/" in "/" + low or "/localization/" in "/" + low):
            continue
        f = root / p
        if low.endswith(".csv"):
            with f.open(encoding="utf-8", newline="") as fh:
                rows = list(csv.reader(fh))
            for row in rows[1:]:
                texts.extend(c for c in row[1:] if c)
        elif low.endswith(".json"):
            def walk(v):
                if isinstance(v, str):
                    texts.append(v)
                elif isinstance(v, dict):
                    for x in v.values():
                        walk(x)
                elif isinstance(v, list):
                    for x in v:
                        walk(x)
            walk(json.loads(f.read_text(encoding="utf-8")))
    return texts


def harm_proof():
    fails = []
    if harm_texts(["The squirrel is fine.", "Patients are safe: the hospital runs on backup power.", "Send the crew to Riverside."]):
        fails.append("harm test flags innocents")
    if len(harm_texts(["The squirrel " + "died."])) != 1 or len(harm_texts(["Patients " + "in danger"])) != 1 \
            or len(harm_texts(["A crew member was " + "injured"])) != 1:
        fails.append("harm test misses planted words")
    return fails


def self_test(entries):
    root = Path(tempfile.mkdtemp(prefix="volt_ip_fixture_"))
    try:
        planted = [
            ("unity/Assets/Resources/data/events.json", '{"name": "' + "Cherno" + 'byl drill"}', "plain"),
            ("unity/Assets/Resources/data/plants.json", '{"owner": "' + "National" + '_Grid"}', "separator"),
            ("unity/Assets/i18n/game.csv", 'KEY,"The ' + "Fukú" + 'shima line"', "accent"),
            ("store/en.txt", "Avoid a " + "M3lt" + "d0wn tonight", "leetspeak"),
            ("unity/Assets/Scripts/App/Names.cs", "class " + "Hoo" + "verDamPlant {}", "camelcase"),
            ("unity/Assets/i18n/ru.csv", 'KEY,"' + "Черно" + 'быль"', "non-latin"),
            ("unity/Assets/Scenes/Board.unity", "  m_Name: " + "Funken" + "schlag_Board", "rule name"),
        ]
        for rel, body, _ in planted:
            f = root / rel
            f.parent.mkdir(parents=True, exist_ok=True)
            f.write_text("clean line\n" + body + "\n", encoding="utf-8")
        (root / "unity/Assets/Resources/data/clean.json").write_text(
            "volt city power grid manager national park hoovering step 3 on icon editor sim cities draxel edfu enelope\n", encoding="utf-8")
        model = "unity/Assets/Art/Models/plants/" + "three_" + "gorges.fbx"
        (root / model).parent.mkdir(parents=True, exist_ok=True)
        (root / model).write_bytes(b"\x00binary")
        gen = root / "unity/Library/Artifacts/strings.txt"
        gen.parent.mkdir(parents=True, exist_ok=True)
        gen.write_text("Cherno" + "byl\n", encoding="utf-8")
        hits, _, _ = scan_tree(root, entries)
        joined = "\n".join(hits)
        fails = [f"fixture not caught ({kind}): {rel}" for rel, _, kind in planted if f"{rel}:2 " not in joined]
        if f"{model}:0 " not in joined:
            fails.append("fixture not caught (file name): " + model)
        if "clean.json" in joined:
            fails.append("innocent words flagged: " + joined)
        if ":1 " in joined:
            fails.append("clean first lines flagged")
        if "unity/Library" in joined:
            fails.append("generated unity/Library was scanned")
        return fails
    finally:
        shutil.rmtree(root, ignore_errors=True)


def main():
    entries = load_entries()
    proof = self_test(entries) + harm_proof()
    for p in proof:
        print("FAIL", p)
    if "--proof" in sys.argv:
        print("IP_DENYLIST proof:", "caught all planted words" if not proof else "FAILED")
        return 0 if not proof else 1
    hits, files, text_files = scan_tree(GAME, entries)
    for h in hits:
        print("FAIL IP match", h)
    harm = harm_texts(i18n_texts(GAME))
    for h in harm:
        print("FAIL harm text:", h)
    bad = len(hits) + len(proof) + len(harm)
    print(f"IP_DENYLIST: {len(entries)} entries, {files} files ({text_files} text) scanned, {len(hits)} matches, "
          f"proof fixture {'caught all planted words' if not proof else 'FAILED'}, harm texts {len(harm)}")
    return 0 if bad == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
