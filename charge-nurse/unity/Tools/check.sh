#!/bin/sh
# Charge Nurse — every check that runs without the Unity editor (the editor's EditMode/PlayMode runs come on top, see
# docs/UNITY.md). Exit 0 = PASS.
#   sh charge-nurse/unity/Tools/check.sh [--fast]      (--fast skips the Unity reference compile)
set -u
HERE=$(cd "$(dirname "$0")" && pwd)
UNITY=$(dirname "$HERE")
A="$UNITY/Assets"
fail=0
step() { printf "\n== %s\n" "$1"; }
bad() { echo "FAIL: $1"; fail=1; }

step "generated files fresh"
python3 "$HERE/build_iap.py" --check || fail=1
python3 "$HERE/i18n_merge.py" --check || fail=1

step "static gates (fleet rules)"
n=$(grep -rn "MobileAds.Initialize" "$A" --include=*.cs | grep -v "CoddyKit/" | grep -v -E ":[0-9]+:\s*//" | wc -l)
[ "$n" -eq 0 ] || bad "MobileAds.Initialize outside the kit ($n files) — the kit owns the ONE init (§5)"
grep -rn -E "InterstitialAd\b|BannerView\b|AdView\b" "$A" --include=*.cs | grep -v "CoddyKit/Runtime/Ads/House" && bad "AdMob interstitial / banner code (house only since 10-25, §2)"
grep -rn "PlayerPrefs" "$A/Scripts" --include=*.cs && bad "PlayerPrefs (saves go through SaveRepository, §14)"
grep -rn -E "purchased_any|has_purchased|HasPurchased" "$A/Scripts" --include=*.cs && bad "'bought anything' must not decide ads (§3)"
grep -rn -i -E "free trial|try free|subscription" "$A/Resources" "$UNITY/Tools/i18n" && bad "trial / subscription wording (§1)"
grep -rn -i -w -E "Codigames|Hospital Empire|Empire Tycoon|Mayo Clinic|Cleveland Clinic|Johns Hopkins|NHS|Red Cross|Red Crescent|Kızılay|Tylenol|Aspirin|Ibuprofen|Paracetamol|Grey.?s Anatomy" "$A/Resources/data" "$UNITY/Tools/i18n/en.json" "$A/Scripts" && bad "real hospital / brand / drug / reference-game names (originality + impersonation rules)"
grep -rn -E "msy_[A-Za-z0-9]{8,}|goog_[A-Za-z0-9]{8,}|BEGIN (RSA|EC|OPENSSH) PRIVATE KEY" "$UNITY" --include=*.cs --include=*.json --include=*.md --include=*.py --include=*.sh && bad "a secret in the repo"
for f in palette.png; do [ -f "$A/Resources/materials/$f" ] || bad "missing $f (Tools/make_palette.py)"; done
for f in coach_nurse intro_1 intro_2 intro_3 curtain; do [ -f "$A/Resources/ui/$f.png" ] || bad "missing ui/$f.png (tools/art/render_ui.py)"; done
for f in bay bed desk chairs ambulance xray lab ct or_table; do [ -f "$A/Resources/kit/$f.fbx" ] || bad "missing kit/$f.fbx (tools/art/kit.py)"; done
for f in nurse doctor patient; do [ -f "$A/Art/Meshy/${f}_lod0.fbx" ] || bad "missing Art/Meshy/${f}_lod0.fbx (tools/meshy + tools/art/glb2fbx.py)"; done
grep -rn -i -E "\b(blood|bleed|gore|corpse|dies|died|dead)\b" "$UNITY/Tools/i18n/en.json" && bad "graphic wording (plan §5 tone: no gore, no death)"
[ $fail -eq 0 ] && echo "static gates PASS"

step "engine-free compile + NUnit (Sim, Game, CoddyKit.Core)"
out=$(sh "$HERE/dotnet_check.sh" 2>&1); echo "$out" | grep -E "Error|Passed!|Failed" ; echo "$out" | grep -q "Passed!" || fail=1
echo "$out" | grep -q "Failed!" && fail=1

if [ "${1:-}" != "--fast" ]; then
  step "engine-bound compile vs UnityEngine refs + C# parse"
  ref=$(sh "$HERE/unity_ref_check.sh" 2>&1); echo "$ref" | tail -6
  echo "$ref" | grep -q "UNITY_REF_CHECK: PASS" || fail=1
fi

echo
if [ $fail -eq 0 ]; then echo "CHARGE_NURSE_CHECK: PASS"; else echo "CHARGE_NURSE_CHECK: FAIL"; fi
exit $fail
