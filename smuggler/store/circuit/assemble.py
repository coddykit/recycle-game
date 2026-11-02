#!/usr/bin/env python3
"""Borderline Empire circuit set: copy the raw game frames of store/deck/capture_raw.sh (playphone 1080x2340, playtab
1200x1920) into the render layout and record each frame's state (debug_tools.gd --store_state) in raw/scenes.json.
  python3 store/circuit/assemble.py <WORK>/work <OUT>   ->  <OUT>/raw/<locale>/{phone,tablet}/NN-<id>.png + <OUT>/raw/scenes.json"""
import json
import shutil
import sys
from pathlib import Path

LOCALE = {"en": "en-US", "tr": "tr-TR", "de": "de-DE", "es": "es-ES", "fr": "fr-FR", "ja": "ja-JP", "ko": "ko-KR",
          "pt": "pt-BR", "ru": "ru-RU", "zh": "zh-CN"}
SHOT = {"01_empire": "01-empire", "02_dispatch": "02-dispatch", "03_event": "03-event", "04_fleet": "04-fleet",
        "05_upgrades": "05-upgrades", "06_map": "06-map", "07_contracts": "07-contracts", "08_frost": "08-frost"}
DEVICE = {"playphone": "phone", "playtab": "tablet"}

work, out = Path(sys.argv[1]), Path(sys.argv[2])
shots = []
for dev, kind in DEVICE.items():
    for lang, loc in LOCALE.items():
        src = work / f"raw_{dev}" / lang
        for sid, name in SHOT.items():
            png, state = src / f"{sid}.png", src / f"{sid}.state.json"
            if not png.exists():
                continue
            dst = out / "raw" / loc / kind / f"{name}.png"
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(png, dst)
            st = json.loads(state.read_text()) if state.exists() and state.stat().st_size else {}
            shots.append({"locale": loc, "device": kind, "shot": name, **st})
(out / "raw").mkdir(parents=True, exist_ok=True)
(out / "raw/scenes.json").write_text(json.dumps({"shots": shots}, ensure_ascii=False, indent=0) + "\n")
print("ASSEMBLED", len(shots), "frames")
