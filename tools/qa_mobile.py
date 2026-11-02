#!/usr/bin/env python3
"""Render actual game UI at phone size with isolated saves and check runtime errors."""
import argparse
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--godot', default='godot')
parser.add_argument('--locales', default='en,tr,de,es,fr,ja,ko,pt,ru,zh')
parser.add_argument('--panels', default='contracts,dispatch,fleet,map,upgrades,business,settings,shop')
args = parser.parse_args()
out = ROOT / 'docs/qa'
out.mkdir(parents=True, exist_ok=True)
results = []
for lang in args.locales.split(','):
    for panel in args.panels.split(','):
        name = f'{lang}-{panel}'
        shot = out / (name + '.png')
        command = [args.godot, '--path', str(ROOT), '--resolution', '432x864', '--', '--qa', '--fresh', '--notitle',
                   '--notut', '--nomodal', '--theme=dark', f'--lang={lang}', f'--panel={panel}',
                   f'--shot={shot}', '--delay=0.6', '--wait=0.6', '--audit']
        result = subprocess.run(command, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=100)
        (out / (name + '.log')).write_text(result.stdout)
        failures = [l for l in result.stdout.splitlines() if any(t in l for t in ['SCRIPT ERROR', 'ERROR:', 'QA_SMALL_BUTTON', 'QA_HORIZONTAL_OVERFLOW'])]
        passed = result.returncode == 0 and shot.exists() and not failures
        results.append({'locale': lang, 'panel': panel, 'passed': passed, 'errors': failures})
        print(name, 'PASS' if passed else failures or 'FAILED', flush=True)
(out / 'report.json').write_text(json.dumps(results, ensure_ascii=False, indent=2) + '\n')
raise SystemExit(0 if all(r['passed'] for r in results) else 1)
