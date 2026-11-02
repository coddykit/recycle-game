#!/usr/bin/env python3
"""Platform-specific release gate. Configuration-only mode is never release approval."""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CHECKS = ('launch', 'pause_resume', 'back_navigation', 'purchase', 'restore',
          'purchase_cancel', 'reward_earned', 'reward_cancel', 'consent', 'analytics')


def read_object(path):
    try:
        value = json.loads(path.read_text())
        return value if isinstance(value, dict) else {}
    except (OSError, ValueError):
        return {}


def gate(root, platforms, configuration_only=False, artifact=None):
    config = read_object(root / 'data/monetization.json')
    project = (root / 'project.godot').read_text()
    presets = (root / 'export_presets.cfg').read_text()
    problems = []
    for platform in platforms:
        prefix = 'goog_' if platform == 'android' else 'appl_'
        if not str(config.get('revenuecat_' + platform + '_key', '')).startswith(prefix):
            problems.append(platform + ': missing app-specific RevenueCat public key')
        unit = str(config.get('rewarded_' + platform, ''))
        if not re.fullmatch(r'ca-app-pub-\d+/\d+', unit) or '3940256099942544' in unit:
            problems.append(platform + ': missing production rewarded ad unit')
        app = re.search(r'general/' + platform + r'/app_id="([^"]+)"', project)
        if not app or not re.fullmatch(r'ca-app-pub-\d+~\d+', app.group(1)) or '3940256099942544' in app.group(1):
            problems.append(platform + ': AdMob application ID is absent or a Google sample')
        key = 'package/unique_name' if platform == 'android' else 'application/bundle_identifier'
        match = re.search(re.escape(key) + r'="([^"]+)"', presets)
        package = match.group(1) if match else ''
        if not package:
            problems.append(platform + ': package identifier missing')
        if platform == 'android':
            sdk = re.search(r'gradle_build/target_sdk="(\d+)"', presets)
            if not sdk or int(sdk.group(1)) < 36:
                problems.append('android: target SDK must meet the configured API 36 release baseline')
            firebase = read_object(root / 'google-services.json')
            packages = [c.get('client_info', {}).get('android_client_info', {}).get('package_name')
                        for c in firebase.get('client', []) if isinstance(c, dict)]
            if package not in packages:
                problems.append('android: Firebase package does not match export package')
        elif not re.search(r'application/app_store_team_id="[A-Z0-9]+"', presets):
            problems.append('ios: signing team missing')
        if configuration_only:
            continue
        declarations = read_object(root / 'docs/analytics-store-declarations.json').get(platform, {})
        if not isinstance(declarations, dict) or declarations.get('analytics_declared') is not True or declarations.get('package') != package or not declarations.get('evidence'):
            problems.append(platform + ': Analytics store privacy declaration unverified for current package')
        native = read_object(root / 'docs/native-sandbox-verification.json').get(platform, {})
        if not isinstance(native, dict):
            native = {}
        if native.get('package') != package or not native.get('device') or not native.get('tested_at'):
            problems.append(platform + ': native evidence lacks current package/device/test date')
        checks = native.get('checks', {})
        if not isinstance(checks, dict):
            checks = {}
        for name in CHECKS:
            result = checks.get(name, {})
            if not isinstance(result, dict) or result.get('status') != 'passed' or not result.get('evidence'):
                problems.append(platform + ': native ' + name + ' evidence missing or not passed')
        if platform == 'android':
            bundle = artifact or root / 'build/android/borderlineempire.aab'
            manifest = read_object(bundle.with_suffix('.release.json'))
            if not bundle.is_file():
                problems.append('android: final AAB missing')
                continue
            sha = hashlib.sha256(bundle.read_bytes()).hexdigest()
            version = re.search(r'config/version="([^"]+)"', project)
            if manifest.get('sha256') != sha or manifest.get('package') != package or manifest.get('version_name') != (version.group(1) if version else None):
                problems.append('android: release manifest does not match bundle/package/source version')
            if not isinstance(manifest.get('version_code'), int) or manifest.get('version_code', 0) <= 1791611984:
                problems.append('android: release must be newer than the old 1791611984 draft')
            if native.get('artifact_sha256') != sha or native.get('version_code') != manifest.get('version_code'):
                problems.append('android: native evidence does not match final AAB hash/version')
    return problems


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--platform', choices=('android', 'ios', 'all'), default='all')
    parser.add_argument('--configuration-only', action='store_true')
    parser.add_argument('--artifact', type=Path)
    args = parser.parse_args()
    problems = gate(ROOT, ['android', 'ios'] if args.platform == 'all' else [args.platform], args.configuration_only, args.artifact)
    for problem in problems:
        print('BLOCKED: ' + problem)
    if not problems:
        print('Configuration passed (not release clearance).' if args.configuration_only else 'Release evidence gate passed; verify Play Console submission checks before rollout.')
    return bool(problems)


if __name__ == '__main__':
    raise SystemExit(main())
