#!/usr/bin/env python3
"""Translate store/deck/copy/en.json into the 9 other deck locales with nitrorouter gpt-5.6-luna (gpt-cheap).

Owner rule (CLAUDE.md): store copy is translated ONLY through api.nitrorouter.com, model gpt-5.6-luna;
never OpenRouter, never by hand. The key lives on the Mac mini, so run it there:
  ssh mehmet@100.113.101.52 'python3 - ' < store/deck/translate_copy.py   (en.json is embedded by the caller via EN_JSON)
or locally with NITRO_KEY_FILE pointing to a copy of the key. Output: JSON {lang: {...}} on stdout.
"""
import json
import os
import sys
import time
import urllib.request

URL = "https://api.nitrorouter.com/v1/chat/completions"
MODEL = "gpt-5.6-luna"
KEY = open(os.environ.get("NITRO_KEY_FILE", "/Volumes/BuildSSD/scene-translate/nitro_cheap.key")).read().strip()
EN = json.loads(os.environ["EN_JSON"]) if os.environ.get("EN_JSON") else json.load(sys.stdin)
LANGS = {"tr": "Turkish", "de": "German", "es": "Spanish (Spain)", "fr": "French", "ja": "Japanese",
         "ko": "Korean", "pt": "Brazilian Portuguese", "ru": "Russian", "zh": "Simplified Chinese"}
only = [l for l in os.environ.get("ONLY", "").split(",") if l]

RULES = """You translate App Store / Google Play screenshot captions for "Borderline Empire", a casual idle
logistics tycoon game (deliveries, trucks, routes, fictional border crossings). Rules:
- Return ONLY JSON with exactly the same keys and structure as the input.
- Keep it SHORT: each value at most about 1.2x the English length (captions on a phone screenshot).
- "lead" + "key" read as ONE grammatically correct sentence/phrase when read together ("lead key"): check
  agreement, case and articles across the split (e.g. Russian case endings, Turkish suffixes, French articles).
  lead is the small first half, key the big punchline (1-3 words). Never leave a dangling possessive.
- Every "sub" must also be grammatical and in the target language only (no English words except Borderline Empire, VIP).
- "stamp" is a single real word printed on a rubber stamp (like APPROVED / URGENT), uppercase, no abbreviations like OK/AUTO.
- "manifest" means a shipping cargo manifest document.
- Keep the brand "Borderline Empire" in Latin letters. Keep digits (8, 10). Keep "VIP".
- Natural marketing tone, no exclamation marks, no words like free/best/#1.
- Uppercase where the script has case."""


def call(lang_name, payload):
    body = json.dumps({"model": MODEL, "temperature": 0.2, "messages": [
        {"role": "system", "content": RULES},
        {"role": "user", "content": f"Target language: {lang_name}\n\n{json.dumps(payload, ensure_ascii=False)}"}]}).encode()
    for i in range(8):
        try:
            req = urllib.request.Request(URL, body, {"Authorization": f"Bearer {KEY}", "Content-Type": "application/json"})
            with urllib.request.urlopen(req, timeout=120) as r:
                txt = json.load(r)["choices"][0]["message"]["content"].strip()
            txt = txt[txt.find("{"): txt.rfind("}") + 1]
            out = json.loads(txt)
            assert set(out) == set(payload) and set(out["slides"]) == set(payload["slides"])
            return out
        except Exception as e:  # luna on gpt-cheap gives ~10 % intermittent 403s: retry
            print(f"retry {lang_name} {i}: {str(e)[:120]}", file=sys.stderr)
            time.sleep(2 + 2 * i)
    raise SystemExit(f"failed {lang_name}")


res = {}
for code, name in LANGS.items():
    if only and code not in only:
        continue
    res[code] = call(name, EN)
    print(f"ok {code}", file=sys.stderr)
print(json.dumps(res, ensure_ascii=False, indent=1))
