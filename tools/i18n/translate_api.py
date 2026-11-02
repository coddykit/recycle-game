#!/usr/bin/env python3
"""Incremental NitroRouter translation. Never embeds credentials or falls back to English."""
import argparse
import concurrent.futures
import csv
import json
import os
from pathlib import Path
import re
import time
import urllib.request
import urllib.error

LANGS = ['en', 'tr', 'de', 'es', 'fr', 'ja', 'ko', 'pt', 'ru', 'zh']
NAMES = dict(zip(LANGS, ['English', 'Turkish', 'German', 'Spanish', 'French', 'Japanese', 'Korean', 'Portuguese', 'Russian', 'Simplified Chinese']))
URL = 'https://api.nitrorouter.com/v1/chat/completions'
MODEL = 'gpt-6-luna'
PRINTF = re.compile(r'%(?:[-+0#]*\d*(?:\.\d+)?[sdif]|%)')
NAMED = re.compile(r'\{[A-Za-z_][A-Za-z_0-9]*\}')


def valid(source, value):
    return (isinstance(value, str) and bool(value.strip())
            and PRINTF.findall(source) == PRINTF.findall(value)
            and sorted(NAMED.findall(source)) == sorted(NAMED.findall(value)))


def translate(lang, items, key):
    prompt = (f'Translate every value into {NAMES[lang]} for Borderline Empire, a fictional mobile logistics tycoon. '
              'Use natural concise game UI language. English source keys are identifiers: retain them exactly. '
              'Keep all printf tokens (%s, %d, %.1f, %%) in the EXACT same order; preserve {named} placeholders. '
              'Preserve line breaks. A suffix %s may represent an English plural suffix: retain it; runtime supplies an empty string outside English. '
              'Do not translate company names or Dot, the mentor name. Return only a JSON object mapping each input key to translated text.')
    body = {'model': MODEL, 'messages': [{'role': 'system', 'content': prompt},
            {'role': 'user', 'content': json.dumps(items, ensure_ascii=False)}], 'max_tokens': 12000}
    for attempt in range(6):
        try:
            req = urllib.request.Request(URL, json.dumps(body).encode(),
                headers={'Authorization': 'Bearer ' + key, 'Content-Type': 'application/json'})
            with urllib.request.urlopen(req, timeout=150) as response:
                result = json.load(response)
            content = result['choices'][0]['message']['content'].strip()
            if content.startswith('```'):
                content = re.sub(r'^```(?:json)?\s*|\s*```$', '', content)
            parsed = json.loads(content)
            if not isinstance(parsed, dict):
                raise ValueError('Expected object')
            good = {src: parsed[src] for src in items if valid(src, parsed.get(src))}
            return good, result.get('usage', {}), result.get('model', MODEL)
        except (urllib.error.URLError, TimeoutError, ValueError, KeyError) as err:
            # Status only; never log request headers or service error bodies.
            print(f'{lang}: retry {attempt + 1}: {type(err).__name__}', flush=True)
            if attempt == 5:
                raise RuntimeError(f'Translation failed for {lang}') from None
            time.sleep(min(2 ** attempt * 2, 30))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--csv', type=Path, default=Path(__file__).resolve().parents[2] / 'translations/strings.csv')
    parser.add_argument('--cache', type=Path, default=Path(__file__).resolve().parents[2] / 'translations/api-cache')
    parser.add_argument('--workers', type=int, default=3)
    args = parser.parse_args()
    key = os.environ.get('NITROROUTER_API_KEY', '').strip()
    if not key:
        parser.error('NITROROUTER_API_KEY is required')
    rows = list(csv.DictReader(args.csv.open(encoding='utf-8', newline='')))
    args.cache.mkdir(parents=True, exist_ok=True)
    def work(lang):
        path = args.cache / (lang + '.json')
        cache = json.loads(path.read_text()) if path.exists() else {}
        missing = [r['keys'] for r in rows if not valid(r['keys'], cache.get(r['keys'])) and not valid(r['keys'], r.get(lang))]
        calls = 0
        models = set()
        for start in range(0, len(missing), 24):
            batch = missing[start:start + 24]
            got, usage, model = translate(lang, {s: s for s in batch}, key)
            for src in batch:
                if src not in got:
                    for retry in range(4):
                        fixed, _, _ = translate(lang, {src: src}, key)
                        if src in fixed:
                            got.update(fixed)
                            break
                    if src not in got:
                        raise RuntimeError(f'Invalid placeholders after retries: {lang}: {src!r}')
            cache.update(got)
            tmp = path.with_suffix('.tmp')
            tmp.write_text(json.dumps(cache, ensure_ascii=False, indent=2) + '\n')
            tmp.replace(path)
            calls += 1
            models.add(model)
            print(f'{lang}: {min(start + 24, len(missing))}/{len(missing)}', flush=True)
        return lang, cache, calls, sorted(models)
    report = {'endpoint': URL, 'requested_model': MODEL, 'locales': {}}
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        for lang, cache, calls, models in pool.map(work, LANGS[1:]):
            for row in rows:
                if valid(row['keys'], cache.get(row['keys'])):
                    row[lang] = cache[row['keys']]
            report['locales'][lang] = {'calls': calls, 'response_models': models}
    for row in rows:
        row['en'] = row['keys']
        for lang in LANGS:
            if not valid(row['keys'], row.get(lang)):
                raise SystemExit(f'Incomplete locale {lang}: {row["keys"]!r}')
    tmp = args.csv.with_suffix('.tmp')
    with tmp.open('w', encoding='utf-8', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=['keys'] + LANGS)
        writer.writeheader()
        writer.writerows(rows)
    tmp.replace(args.csv)
    (args.cache / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    print(f'Complete: {len(rows)} keys in {len(LANGS)} languages', flush=True)

if __name__ == '__main__':
    main()
