"""Trim silence and peak-normalise generated audio (needs ffmpeg).

    python tools/audio/postprocess.py
SFX peak at -1 dBFS, voice lines at -3 dBFS, leading silence removed so
sounds fire instantly on tap.
"""
import glob
import os
import re
import subprocess
import tempfile

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
TRIM = ("silenceremove=start_periods=1:start_threshold=-48dB:start_silence=0.02,"
        "areverse,silenceremove=start_periods=1:start_threshold=-52dB:start_silence=0.05,areverse")


def peak_db(path):
    out = subprocess.run(["ffmpeg", "-hide_banner", "-i", path, "-af", "volumedetect", "-f", "null", "-"],
                         capture_output=True, text=True).stderr
    m = re.search(r"max_volume: (-?[0-9.]+) dB", out)
    return float(m.group(1)) if m else 0.0


def process(path, target):
    tmp = tempfile.mktemp(suffix=".wav")
    subprocess.run(["ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-i", path, "-af", TRIM, tmp], check=True)
    gain = target - peak_db(tmp)
    out = tempfile.mktemp(suffix=".mp3")
    subprocess.run(["ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-i", tmp, "-af", "volume=%.2fdB" % gain,
                    "-codec:a", "libmp3lame", "-q:a", "3", out], check=True)
    os.replace(out, path)
    os.remove(tmp)
    return gain


def main():
    for p in sorted(glob.glob(os.path.join(ROOT, "assets", "audio", "*.mp3"))):
        print("sfx  %-28s %+5.1f dB" % (os.path.basename(p), process(p, -1.0)))
    for p in sorted(glob.glob(os.path.join(ROOT, "assets", "audio", "voice", "*", "*.mp3"))):
        print("voice %-28s %+5.1f dB" % (os.path.relpath(p, ROOT)[-28:], process(p, -3.0)))


if __name__ == "__main__":
    main()
