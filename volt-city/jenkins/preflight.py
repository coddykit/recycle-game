#!/usr/bin/env python3
"""Volt City (Unity) build preflight — fails CLOSED. Same idea as the Godot games' services_preflight.py.

  python3 games/volt-city/jenkins/preflight.py              # offline gates (every commit / Tools/check.sh / Jenkins Tests stage)
  python3 games/volt-city/jenkins/preflight.py --production # + everything a Play upload needs (Jenkins before the AAB)

Offline: Unity source gates, IP denylist + proof, generated files fresh, package manifest sane, no secrets in the project.
Production adds: ProjectVersion.txt committed (editor version pinned), vendor SDK packages present (Google Mobile Ads +
UMP, RevenueCat, Play Review), release ids non-empty (AdMob rewarded unit, RevenueCat key) and the AdMob APP id in
GoogleMobileAdsSettings, volt-city in the fleet house_related.py (not only the PENDING seed), symbols enabled.
Writes the table to build/android/preflight.txt when that folder exists.
"""
import json
import re
import subprocess
import sys
from pathlib import Path

sys.dont_write_bytecode = True
GAME = Path(__file__).resolve().parents[1]
UNITY = GAME / "unity"
TOOLS = UNITY / "Tools"
rows = []


def gate(name, ok, detail=""):
    rows.append((name, ok, detail))


def run(cmd):
    r = subprocess.run(cmd, capture_output=True, text=True)
    out = (r.stdout + r.stderr).strip().splitlines()
    return r.returncode == 0, (out[-1] if out else "")


def offline():
    ok, last = run([sys.executable, str(TOOLS / "check_unity_sources.py")])
    gate("source gates (G1-G7)", ok, last)
    ok, last = run([sys.executable, str(TOOLS / "ip_denylist.py")])
    gate("IP denylist + proof", ok, last)
    r = subprocess.run([sys.executable, str(UNITY / "Tools/build_iap.py"), "--check"], capture_output=True, text=True)
    gate("IAP catalogue fresh (P4: iap.json, rc-catalog, strings; no trial)", r.returncode == 0, (r.stdout.strip().splitlines() or [""])[-1][:120])
    try:
        m = json.loads((UNITY / "Packages/manifest.json").read_text())
        deps = m.get("dependencies", {})
        need = ["com.unity.render-pipelines.universal", "com.unity.inputsystem", "com.unity.test-framework", "com.unity.modules.uielements"]
        missing = [d for d in need if d not in deps]
        gate("package manifest", not missing, "missing: " + ", ".join(missing) if missing else f"{len(deps)} packages")
    except (OSError, ValueError) as e:
        gate("package manifest", False, str(e))
    secret_re = re.compile(r"-----BEGIN [A-Z ]*PRIVATE KEY-----|keystorePass: \S|keyaliasPass: \S|sk_live_|AIza[0-9A-Za-z_\-]{35}")
    hits = []
    for p in UNITY.rglob("*"):
        rel = p.relative_to(UNITY).as_posix()
        if not p.is_file() or rel.split("/")[0] in ("Library", "Temp", "Logs", "Build", "Builds", "obj", "UserSettings"):
            continue
        if p.suffix.lower() in (".jks", ".keystore", ".ulf", ".alf", ".p12"):
            hits.append(rel)
            continue
        if p.suffix.lower() in (".png", ".fbx", ".glb", ".jpg", ".webp", ".otf", ".ttf"):
            continue
        try:
            if secret_re.search(p.read_text(encoding="utf-8", errors="ignore")):
                hits.append(rel)
        except OSError:
            pass
    gate("no secrets / signing / license files in the project", not hits, ", ".join(hits[:5]))


def production():
    pv = UNITY / "ProjectSettings/ProjectVersion.txt"
    gate("ProjectVersion.txt committed (editor pinned)", pv.exists(), pv.read_text().splitlines()[0] if pv.exists() else "open the project once in Unity 6 LTS and commit it")
    deps = json.loads((UNITY / "Packages/manifest.json").read_text()).get("dependencies", {})
    for pkg in ("com.google.ads.mobile", "com.revenuecat.purchases-unity", "com.google.play.review"):
        gate(f"vendor package {pkg}", pkg in deps, deps.get(pkg, "not installed (docs/UNITY.md section 4)"))
    cfg = (UNITY / "Assets/Scripts/App/VoltGameConfig.cs").read_text()
    for const in ("AdMobRewardedAndroid", "RevenueCatAndroidKey"):
        m = re.search(r"const string %s = \"([^\"]*)\"" % const, cfg)
        val = m.group(1) if m else ""
        gate(f"release id {const}", bool(val), "set" if val else "empty (Patron creates it, P4/P8)")
    gma = list((UNITY / "Assets").rglob("GoogleMobileAdsSettings.asset"))
    app_id_ok = bool(gma) and re.search(r"ca-app-pub-\d+~\d+", gma[0].read_text(errors="ignore")) is not None
    gate("AdMob APP id in GoogleMobileAdsSettings", app_id_ok, str(gma[0].relative_to(UNITY)) if gma else "settings asset missing")
    fleet = (GAME.parent / "tools/ads/house_related.py")
    in_fleet = fleet.exists() and '"volt-city"' in fleet.read_text()
    gate("volt-city in games/tools/ads/house_related.py", in_fleet, "fleet entry" if in_fleet else "PENDING seed only (P4)")


def main():
    offline()
    if "--production" in sys.argv:
        production()
    width = max(len(r[0]) for r in rows)
    lines = [f"{'PASS' if ok else 'FAIL'}  {name.ljust(width)}  {detail}" for name, ok, detail in rows]
    bad = sum(1 for r in rows if not r[1])
    lines.append(f"VOLT_PREFLIGHT {'production' if '--production' in sys.argv else 'offline'}: {len(rows) - bad}/{len(rows)} pass")
    print("\n".join(lines))
    out = UNITY / "Build/android"
    if out.exists():
        (out / "preflight.txt").write_text("\n".join(lines) + "\n")
    return 0 if bad == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
