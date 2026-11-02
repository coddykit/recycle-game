# Volt City (Unity) — Android Linux build-only — **NOT WIRED**

Owner 2026-10-27: Volt City is the fleet's Unity test game. This folder replaces the Godot P0 plan (dropped, never committed).
Full context: [`../docs/UNITY.md`](../docs/UNITY.md) §7.

| File | What |
|---|---|
| `Jenkinsfile` | job `Games/volt-city-mobile`, agent `linux-android`, build-only (never uploads). Parameter `WIRED` (default **false**) stops every run at "Wiring check" until the editor + licence exist on the agent. |
| `build_unity_android.sh` | `test` or `build`: ProjectVersion.txt → editor → licence (serial or Personal `.ulf`, removed / returned in the exit trap) → `preflight.py` → EditMode tests → `VoltCity.EditorTools.VoltAndroidBuild.Run` → `Build/android/{volt-city.aab, volt-city.symbols.zip, version_code.txt, release.json}`. `DRY_RUN=1` prints the plan. |
| `preflight.py` | fails closed. Offline: source gates, IP denylist + proof, manifest, no secrets/keystores/licence files. `--production`: ProjectVersion.txt, vendor packages, release ids, AdMob app id, fleet house-ads entry. |
| `game.env` | `ENGINE=unity`, package, paths, `WIRED=false`. |

Rules kept from the fleet: build-only on Linux (no Android builds on the Mac), production mode by preflight (no release
with empty ids = no mock/test services in a release), `versionCode = date +%s`, symbols zip next to the AAB for the same
Play edit, nothing secret in git or logs (credentials only through Jenkins; the build clears the keystore passwords from
PlayerSettings and CI restores `ProjectSettings` after the run).

```sh
python3 games/volt-city/jenkins/preflight.py                 # offline gates (today: 4/4)
python3 games/volt-city/jenkins/preflight.py --production    # today: blocked by design (P4/P8 items)
DRY_RUN=1 sh games/volt-city/jenkins/build_unity_android.sh  # today: BLOCKED until ProjectVersion.txt is committed
```
