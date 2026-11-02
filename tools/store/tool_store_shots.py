#!/usr/bin/env python3
"""Store screenshot capture for Borderline Empire (raw game frames for store/deck).

Runs the real game WINDOWED (real GPU render) at the exact pixel size of the game area of one store slide
(slide size minus the deck's caption band), with an isolated QA save (--qa --fresh, never the player's save),
the tutorial skipped and modals/toasts silenced, through the QA hooks in scripts/core/debug_tools.gd.

  python3 tools/store/tool_store_shots.py --device iphone69 --langs en,tr --out /tmp/be-raw [--only 01_hero,04_event]

Output: <out>/<device>/<lang>/<shot>.png at exactly DEVICES[device].game size.
"""
import argparse
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "store" / "deck"))
from deck_spec import DEVICES, SHOTS  # noqa: E402


def capture(godot, device, lang, shot, out):
    w, h = DEVICES[device]["game"]
    dest = out / device / lang / f"{shot}.png"
    dest.parent.mkdir(parents=True, exist_ok=True)
    tmp = dest.with_name(f"_{shot}.png")
    log = dest.with_suffix(".log")
    cmd = [godot, "--path", str(ROOT), "--always-on-top", "--resolution", f"{w}x{h}", "--", "--qa", f"--shot={tmp}", "--delay=2.5",
           "--notitle", "--fresh", f"--lang={lang}", "--quality=high"] + SHOTS[shot]["args"].split()
    for attempt in range(2):
        tmp.unlink(missing_ok=True)
        r = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=240)
        log.write_text(r.stdout)
        bad = [l for l in r.stdout.splitlines() if "SCRIPT ERROR" in l]
        if tmp.exists() and not bad:
            from PIL import Image
            im = Image.open(tmp).convert("RGB")
            if im.size != (w, h):
                im = im.resize((w, h), Image.LANCZOS)
            im.save(dest)
            tmp.unlink()
            return f"ok   {device} {lang} {shot}"
    return f"FAIL {device} {lang} {shot} ({log})"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--godot", default="godot")
    ap.add_argument("--device", default="iphone69")
    ap.add_argument("--langs", default="en")
    ap.add_argument("--only", default="")
    ap.add_argument("--out", default="/tmp/be-raw")
    ap.add_argument("--jobs", type=int, default=1)  # >1 corrupts: parallel instances share the QA save dir
    a = ap.parse_args()
    shots = [s for s in SHOTS if not a.only or s in a.only.split(",")]
    out = Path(a.out)
    jobs = [(d, l, s) for d in a.device.split(",") for l in a.langs.split(",") for s in shots if SHOTS[s].get("raw", True)]
    fails = 0
    with ThreadPoolExecutor(a.jobs) as ex:
        for msg in ex.map(lambda j: capture(a.godot, j[0], j[1], j[2], out), jobs):
            print(msg, flush=True)
            fails += msg.startswith("FAIL")
    sys.exit(1 if fails else 0)


if __name__ == "__main__":
    main()
