#!/usr/bin/env python3
"""W5 shop screenshots (windowed GPU run; start it through /Volumes/BuildSSD/novareka-dev/shot-lock.sh).
Fake store (--paywall), isolated QA saves. Usage: python3 tools/qa_shop_shots.py [lang ...]   (default tr de ja ru)
Writes docs/qa/w5-2026-10-19/shots/<screen>_<lang>.png and prints the QA_* audit lines (small buttons / horizontal overflow)."""
import re, subprocess, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs/qa/w5-2026-10-19/shots'
OUT.mkdir(parents=True, exist_ok=True)
(ROOT / 'docs/qa/.gdignore').touch()
langs = sys.argv[1:] or ['tr', 'de', 'ja', 'ru']
SCREENS = {
    'shop_top': ['--panel=shop'],
    'shop_cash': ['--panel=shop', '--legacy=2', '--product=cash_l'],
    'shop_looks': ['--panel=shop', '--product=hq_theme'],
    'fleet_paints': ['--panel=fleet', '--detail=1', '--scrollto=paint_8C6A1F'],
    'settings_hq': ['--panel=settings', '--scrollto=hq_F2EAD3'],
    'business_auto': ['--panel=business', '--scrollto=auto_crisis', '--auto=2'],
    'offer_starter': ['--ui_modal=offer_starter'],
    'offer_legacy': ['--ui_modal=offer_legacy', '--legacy=1'],
    'feedback': ['--panel=settings', '--ui_modal=feedback'],
    'drivers': ['--panel=fleet', '--tab=Drivers'],
    'drivers_owned': ['--panel=fleet', '--tab=Drivers', '--owns=driver_portraits'],
}
bad = 0
for lang in langs:
    for name, extra in SCREENS.items():
        shot = OUT / f'{name}_{lang}.png'
        cmd = ['godot', '--path', str(ROOT), '--resolution', '540x960', '--', '--qa', '--fresh', '--notitle', '--notut', '--nomodal',
               '--paywall', '--delay=0.5', '--wait=0.8', '--audit', '--cash=200000', '--garage=2', '--lang=' + lang, '--theme=dark',
               '--shot=' + str(shot), *extra]
        r = subprocess.run(cmd, text=True, capture_output=True, timeout=240)
        log = r.stdout + r.stderr
        issues = []
        for l in log.splitlines():
            if 'QA_SMALL_BUTTON' in l:
                # the owner's touch rule is 88 canvas units (44 pt); the engine audit's own 107 limit is stricter than that rule
                m = re.search(r'\(([\d.]+), ([\d.]+)\)\s*$', l)
                if m and (float(m.group(1)) < 88 or float(m.group(2)) < 88):
                    issues.append(l)
            elif any(x in l for x in ['SCRIPT ERROR', 'QA_HORIZONTAL_OVERFLOW']):
                issues.append(l)
        ok = shot.exists() and not issues
        bad += 0 if ok else 1
        print(f'{lang:3} {name:14} {"ok" if ok else "ISSUES"}', *issues[:3], flush=True)
raise SystemExit(1 if bad else 0)
