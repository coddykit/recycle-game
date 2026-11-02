#!/usr/bin/env python3
"""Static gates for the Unity project (no editor needed). Fleet rules that a grep can enforce:

  G1  exactly ONE `MobileAds.Initialize(` call site in Assets (games/CLAUDE.md §5: one SDK owner; Train Baron crash cluster)
  G2  no AdMob interstitial / banner types anywhere (owner 2026-10-25: house only; AdMob = rewarded only)
  G3  no legacy input (`Input.GetMouseButton`, `Input.GetTouch`, `Input.touches`, `Input.mousePosition`) and no UI Toolkit
      `MouseDownEvent` / `MouseUpEvent` / `ClickEvent` + `PointerUpEvent` double registrations in our code (one tap = one action)
  G4  `Screen.safeArea` only inside CoddyKit/Runtime/SafeArea.cs (everything else uses SafeArea.Rect / Edges / Bottom)
  G5  no `PlayerPrefs` for game or kit state (saves go through SaveRepository: atomic + .bak + .unreadable)
  G6  no "free trial" / "try free" / "start your free" in shipped text (games/CLAUDE.md §1: no trial anywhere)
  G7  generated files fresh: HouseRelatedTable.g.cs, CoddyTokens.uss
Scope: our code only (Assets/CoddyKit, Assets/Scripts/App, Assets/Editor/VoltCity, Assets/Tests/App + P3's Scripts/Game,
Scripts/UI, Scripts/World, Tests/Game, Tests/PlayMode, Resources/i18n, Resources/ui); P1 Sim and P2 Art are checked by
their owners. Exit 0 = all gates pass.
"""
import re
import subprocess
import sys
from pathlib import Path

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
UNITY = HERE.parent
ROOTS = ["Assets/CoddyKit", "Assets/Scripts/App", "Assets/Editor/VoltCity", "Assets/Tests/App",
         # P3 game layer + its UI assets / strings / tests
         "Assets/Scripts/Game", "Assets/Scripts/UI", "Assets/Scripts/World", "Assets/Tests/Game", "Assets/Tests/PlayMode",
         "Assets/Resources/i18n", "Assets/Resources/ui",
         # P4: the IAP catalogue + its generator texts (G6 no trial wording) + the store files for Play / RevenueCat
         "Assets/Resources/data", "Tools/i18n_p4", "../store"]


def files(exts):
    for r in ROOTS:
        for p in sorted((UNITY / r).rglob("*")):
            if p.is_file() and p.suffix.lstrip(".") in exts:
                yield p


def code_lines(p):
    for n, line in enumerate(p.read_text(encoding="utf-8").splitlines(), 1):
        stripped = line.strip()
        if stripped.startswith("//") or stripped.startswith("///") or stripped.startswith("*") or stripped.startswith("/*"):
            continue
        yield n, line


def main():
    fails = []
    init_sites = []
    for p in files({"cs"}):
        rel = p.relative_to(UNITY).as_posix()
        for n, line in code_lines(p):
            if "MobileAds.Initialize(" in line:
                init_sites.append(f"{rel}:{n}")
            if re.search(r"\b(InterstitialAd|BannerView|AppOpenAd|RewardedInterstitialAd|NativeAd)\b", line):
                fails.append(f"G2 AdMob non-rewarded format: {rel}:{n}")
            if re.search(r"\bInput\.(GetMouseButton\w*|GetTouch|touches|touchCount|mousePosition)\b", line) and "InputSystem" not in line:
                fails.append(f"G3 legacy input: {rel}:{n}")
            if re.search(r"RegisterCallback<(MouseDownEvent|MouseUpEvent|ClickEvent)>", line):
                fails.append(f"G3 mouse/click event registration (use Button.clicked or PointerUpEvent once): {rel}:{n}")
            if "Screen.safeArea" in line and not rel.endswith("CoddyKit/Runtime/SafeArea.cs"):
                fails.append(f"G4 Screen.safeArea outside SafeArea.cs: {rel}:{n}")
            if re.search(r"\bPlayerPrefs\.", line):
                fails.append(f"G5 PlayerPrefs (use SaveRepository / JsonStateFile): {rel}:{n}")
    if len(init_sites) != 1:
        fails.append(f"G1 MobileAds.Initialize call sites = {len(init_sites)} (need exactly 1): {init_sites}")
    trial = re.compile(r"free\s+trial|try\s+(it\s+)?free|start\s+your\s+free", re.I)
    for p in files({"cs", "uss", "uxml", "json", "csv", "txt"}):
        rel = p.relative_to(UNITY).as_posix()
        for n, line in enumerate(p.read_text(encoding="utf-8").splitlines(), 1):
            if trial.search(line) and "check_unity_sources" not in rel:
                fails.append(f"G6 trial wording: {rel}:{n}")
    for tool in ("gen_house_related.py", "gen_uss_tokens.py"):
        r = subprocess.run([sys.executable, str(HERE / tool), "--check"], capture_output=True, text=True)
        if r.returncode != 0:
            fails.append(f"G7 {tool} --check: {(r.stdout + r.stderr).strip()}")
    for f in fails:
        print("FAIL", f)
    print(f"UNITY_SOURCE_GATES: {len(fails)} failures (MobileAds.Initialize sites: {len(init_sites)})")
    return 0 if not fails else 1


if __name__ == "__main__":
    sys.exit(main())
