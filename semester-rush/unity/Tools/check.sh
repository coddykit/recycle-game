#!/bin/sh
# Semester Rush — every check that runs without the Unity editor (the editor's EditMode/PlayMode runs come on top, see
# docs/UNITY.md). Exit 0 = PASS.
#   sh semester-rush/unity/Tools/check.sh [--fast]      (--fast skips the Unity reference compile)
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
grep -rn -w -E "DOA|TÜÇA|TUCA|Sıfır Atık|BİM|Carrefour|Return and Earn" "$A/Resources" "$UNITY/Tools/i18n" "$A/Scripts" && bad "real scheme / brand names (impersonation rule)"
grep -rn -E "msy_[A-Za-z0-9]{8,}|goog_[A-Za-z0-9]{8,}|BEGIN (RSA|EC|OPENSSH) PRIVATE KEY" "$UNITY" --include=*.cs --include=*.json --include=*.md --include=*.py --include=*.sh && bad "a secret in the repo"
for f in palette.png; do [ -f "$A/Resources/materials/$f" ] || bad "missing $f (Tools/make_palette.py)"; done
for f in coach_operator intro_1 intro_2 intro_3 curtain; do [ -f "$A/Resources/ui/$f.png" ] || bad "missing ui/$f.png (tools/art/render_ui.py)"; done
for f in machine_s machine_m shop horeca van bike compactor bale_pet_lod1 bale_alu_lod1; do [ -f "$A/Resources/kit/$f.fbx" ] || bad "missing kit/$f.fbx (tools/art/kit.py)"; done
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
if [ $fail -eq 0 ]; then echo "BOTTLE_ROUTE_CHECK: PASS"; else echo "BOTTLE_ROUTE_CHECK: FAIL"; fi
exit $fail
