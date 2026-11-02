#!/usr/bin/env python3
"""Borderline Empire (smuggler) onboarding v2 evidence: disposable HOME (user://), shared GPU lock, strict logs,
project.godot restored after every run.

  ONBOARDING_MODE=prepare|heroes|cards|memory|tests|perf ONBOARDING_OUT=/abs/dir [ONBOARDING_MOMENTS=/abs/dir] \
      python3 tools/onboarding/capture_heroes.py
"""
import fcntl
import hashlib
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import tempfile
import time

from PIL import Image

GAME = Path(__file__).resolve().parents[2]
ENGINE = os.environ.get("GODOT", "/Volumes/BuildSSD/godot/4.7.2/Godot.app/Contents/MacOS/Godot")
LOCK = "/Volumes/BuildSSD/novareka-dev/shot-lock.sh"
ERRORS = ("SCRIPT ERROR", "SHADER ERROR", "Parse Error", "Compile Error", "ONBOARDING_FAIL")
DRIVER = "tools/onboarding/onboarding_run.gd"
MODES = {   # marker, headless, window size
    "prepare": ("ONBOARDING_EARNED_PASS", True, "540x1170"),
    "heroes": ("ONBOARDING_HERO_PASS", False, "512x384"),
    "cards": ("ONBOARDING_CARDS_PASS 240", False, "480x640"),
    "memory": ("ONBOARDING_MEMORY_PASS", False, "540x1170"),
}


def checked(args, path, marker="", env=None, seconds=1500):
    with path.open("w") as out:
        proc = subprocess.Popen(args, stdout=out, stderr=subprocess.STDOUT, env=env, cwd=GAME, start_new_session=True)
        try:
            deadline = time.monotonic() + seconds
            while proc.poll() is None:
                if time.monotonic() > deadline or any(e in path.read_text(errors="replace") for e in ERRORS):
                    raise RuntimeError("Timeout or engine error: " + str(path))
                time.sleep(0.25)
        finally:
            if proc.poll() is None:   # only the process group this wrapper started
                os.killpg(proc.pid, signal.SIGTERM)
                try:
                    proc.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    os.killpg(proc.pid, signal.SIGKILL)
                    proc.wait()
    text = path.read_text(errors="replace")
    if proc.returncode or (marker and marker not in text) or any(e in text for e in ERRORS):
        raise RuntimeError("Failed evidence gate: " + str(path))
    print("PASS " + str(path), flush=True)
    return text


def godot(mode, scratch, extra, log, marker, size, headless):
    home = Path(tempfile.mkdtemp(prefix="smg-onboarding-", dir=scratch))   # user:// = a throw-away HOME
    env = {**os.environ, "HOME": str(home), "XDG_DATA_HOME": str(home / ".local/share"), "XDG_CONFIG_HOME": str(home / ".config")}
    args = [ENGINE] + (["--headless"] if headless else []) + [
        "--path", str(GAME), "--rendering-method", "gl_compatibility", "--audio-driver", "Dummy",
        "--resolution", size, "--position", "0,0", "-s", "res://tools/onboarding/onboarding.gd", "--", "--mode=" + mode] + extra
    return checked(args, log, marker, env)


def main():
    mode = os.environ.get("ONBOARDING_MODE", "heroes")
    out = Path(os.environ["ONBOARDING_OUT"])
    if not out.is_absolute() or shutil.disk_usage(GAME).free < 6 * 1024**3:
        raise RuntimeError("Absolute output and at least 6 GiB free required")
    out.mkdir(parents=True, exist_ok=True)
    # perf_gate.sh takes the GPU lock itself; prepare and the tests run headless (the suite's perf step locks too)
    if mode not in ("prepare", "tests", "perf") and "--locked" not in sys.argv:
        # one Godot GPU window at a time on this Mac
        raise SystemExit(subprocess.call([LOCK, "perl", "-e", "alarm 2400; exec @ARGV", sys.executable, __file__, "--locked"]))
    cache = GAME / ".godot"
    cache.mkdir(exist_ok=True)
    with (cache / "onboarding-qa.lock").open("w") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        project = GAME / "project.godot"
        original = project.read_bytes()
        source = {"game": GAME.name, "capture_driver_sha256": hashlib.sha256((GAME / DRIVER).read_bytes()).hexdigest()}
        try:
            source["commit"] = subprocess.check_output(["git", "-C", str(GAME), "rev-parse", "HEAD"], text=True, stderr=subprocess.DEVNULL).strip()
            source["tracked_diff_sha256"] = hashlib.sha256(subprocess.check_output(["git", "-C", str(GAME), "diff", "HEAD", "--", "."])).hexdigest()
        except subprocess.CalledProcessError:
            source["commit"] = "not a git checkout (git archive copy for a before/after measurement)"
        (out / "source.json").write_text(json.dumps(source, indent=2) + "\n")
        try:
            env = {**os.environ}
            checked([ENGINE, "--headless", "--audio-driver", "Dummy", "--path", str(GAME), "--import"], out / "import.log", "", env, 900)
            if mode == "tests":
                home = Path(tempfile.mkdtemp(prefix="smg-onboarding-tests-", dir=os.environ.get("TMPDIR")))
                checked(["sh", "jenkins/build.sh", "test"], out / "build-test.log", "TESTS: Checks:",
                        {**env, "GODOT": ENGINE, "GAME_OUT": str(out), "HOME": str(home)}, 3600)
                return
            if mode == "perf":
                checked(["sh", "../tools/perf/perf_gate.sh", "."], out / "perf_gate.log", "PERF_GATE:", {**env, "GODOT": ENGINE}, 1200)
                return
            marker, headless_run, size = MODES[mode]
            with tempfile.TemporaryDirectory(prefix="smg-onboarding-", dir=os.environ.get("TMPDIR")) as scratch:
                if mode == "heroes":
                    records = []
                    for index in range(3):
                        godot("hero", scratch, ["--index=%d" % index], out / ("hero-%d.log" % index), marker, size, False)
                        record = json.loads((out / ("hero-%d.json" % index)).read_text())
                        # lossless re-encode (same pixels, smaller zlib stream): the three stay under 1 MB
                        png = out / (record["id"] + ".png")
                        with Image.open(png) as im:
                            im.load()
                            if im.size != (512, 384) or im.mode != "RGB":
                                raise RuntimeError("expected a 512x384 RGB hero: " + str(png))
                            pixels = im.tobytes()
                            im.save(png, "PNG", optimize=True)
                        with Image.open(png) as im:
                            if im.tobytes() != pixels:
                                raise RuntimeError("re-encode changed pixels: " + str(png))
                        records.append(record)
                    (out / "capture.json").write_text(json.dumps(records, indent=2) + "\n")
                else:
                    godot(mode, scratch, [], out / "capture.log", marker, size, headless_run)
        finally:
            project.write_bytes(original)


if __name__ == "__main__":
    main()
