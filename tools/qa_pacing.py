#!/usr/bin/env python3
"""Measure the real Godot bot sequentially, with isolated saves and exact game time."""
import argparse
import hashlib
import json
import os
import statistics
import subprocess
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
# Existing one-hour ceilings retained. The 30 s idle ceiling was declared before tuning.
BUDGETS = {'first_delivery': 90, 'first_upgrade': 900, '2nd vehicle': 1500,
           'region: Green Republic': 2100, 'Auto Accept': 3600}
MAX_IDLE_15_S = 30


def compact(row):
    """Keep complete travel aggregates and the opening trips, not thousands of duplicate rows."""
    trips = row.pop('trips_30', None)
    if trips is not None:
        row['travel_30'] = {}
        for field in ('outbound_s', 'return_s'):
            values = [t[field] for t in trips if field in t]
            row['travel_30'][field] = {'n': len(values), 'median_s': statistics.median(values) if values else None,
                                     'max_s': max(values) if values else None}
        row['opening_trips'] = trips[:8]
    return row


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default='docs/pacing-quality.json')
    ap.add_argument('--baseline', action='store_true', help='Record old gameplay without the new onboarding ceilings')
    args = ap.parse_args()
    godot = os.environ.get('GODOT', '/Volumes/BuildSSD/godot/4.7.2/Godot.app/Contents/MacOS/Godot')
    rows = []
    for daily in (False, True):
        for seed in (12345, 42, 2026, 701, 908):
            env = {**os.environ, 'PACING': '1', 'SIM_SEED': str(seed), 'DAILY_REWARDS': str(int(daily))}
            with tempfile.TemporaryDirectory(prefix='borderline-pace-') as home:
                env.update(HOME=home, XDG_DATA_HOME=home + '/.local/share', XDG_CONFIG_HOME=home + '/.config')
                r = subprocess.run([godot, '--headless', '--audio-driver', 'Dummy', 'res://tests/test_runner.tscn'],
                                   cwd=ROOT, env=env, text=True, capture_output=True, timeout=120)
            output = r.stdout + r.stderr
            errors = [s for s in output.splitlines() if s.startswith('ERROR:') and
                      not any(x in s for x in ('resources still in use', 'RID allocations', 'ObjectDB'))]
            if r.returncode or 'SCRIPT ERROR' in output or errors:
                raise SystemExit(output)
            line = next(x for x in r.stdout.splitlines() if x.startswith('PACING_JSON:'))
            row = compact(json.loads(line.split(':', 1)[1]))
            rows.append(row)
            print(seed, daily, 'Auto Accept:', row['milestones'].get('Auto Accept'),
                  'max forced idle:', row['max_forced_idle_15_s'], flush=True)
    summary, failures = {}, []
    for daily in (False, True):
        group = [r for r in rows if r['daily_rewards'] == daily]
        summary[str(daily)] = {}
        for milestone, limit in BUDGETS.items():
            values = [r['milestones'].get(milestone, 3601) for r in group]
            summary[str(daily)][milestone] = {'median': statistics.median(values), 'tail_max': max(values), 'ceiling': limit}
            if max(values) > limit:
                failures.append(f'{daily}: {milestone}')
        for r in group:
            if r['daily_grants'] > 600:
                failures.append('daily reward cap')
            if not args.baseline:
                if r['max_forced_idle_15_s'] > MAX_IDLE_15_S:
                    failures.append(f"idle gap: {r['seed']} {daily}")
                if r['milestones'].get('first_delivery', 3601) > 15:
                    failures.append(f"opening delivery: {r['seed']} {daily}")
    sources = ('scripts/economy/economy_manager.gd', 'scripts/contracts/contract_manager.gd', 'scripts/progression/progression_manager.gd',
               'scripts/vehicles/vehicle_manager.gd', 'tests/test_runner.gd')
    report = {'schema': 2, 'engine': 'Godot 4.7.2', 'horizon_seconds': 3600, 'detail_horizon_seconds': 1800,
              'budgets': BUDGETS, 'max_forced_idle_15_s_ceiling': MAX_IDLE_15_S,
              'policy': 'One decision each second; wait for tutorial delivery before spending; then original bot policy. '
                        'Forced idle = no dispatch/event/affordable core progression action. Bot action gaps include voluntary saving. '
                        'Ten 0.1 s ticks per second; old qa_pacing used eleven. Audio feedback suspended to exclude wall-clock pitch RNG. No ads or IAP.',
              'source_sha256': {s: hashlib.sha256((ROOT / s).read_bytes()).hexdigest() for s in sources},
              'summary': summary, 'runs': rows, 'failures': failures,
              'human_playthrough': 'not measured by this tool'}
    (ROOT / args.out).write_text(json.dumps(report, indent=2) + '\n')
    print('PACING PASS' if not failures else 'PACING FAIL: ' + ', '.join(failures))
    return int(bool(failures))


if __name__ == '__main__':
    raise SystemExit(main())
