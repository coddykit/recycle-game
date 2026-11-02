"""Post-processing + measurement helpers (numpy + ffmpeg + soundfile). No API calls here.
Adapted from games/cinnamon-cappadocia/tools/audio/gen_audio.py."""
import json
import os
import re
import shutil
import subprocess
import tempfile

import numpy as np
import soundfile as sf

SR = 44100


def ff(*args):
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", *args], check=True)


def decode(path, channels):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-ac", str(channels), "-ar", str(SR), "-f", "f32le", "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).copy().reshape(-1, channels)


def write_wav(path, x):
    sf.write(path, x.astype(np.float32), SR, subtype="FLOAT")


def measure(x, min_len=0.5):
    """(integrated LUFS, true peak dBTP). Short clips are padded with silence (gated out by EBU R128) so a
    0.2 s click still gets a 400 ms-block loudness instead of 'too short'."""
    n = int(min_len * SR)
    if len(x) < n:
        x = np.concatenate([x, np.zeros((n - len(x), x.shape[1]), dtype=x.dtype)])
    fd, tmp = tempfile.mkstemp(suffix=".wav")
    os.close(fd)
    try:
        write_wav(tmp, x)
        err = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", tmp, "-af", "loudnorm=print_format=json", "-f", "null", "-"],
                             capture_output=True, text=True).stderr
    finally:
        os.remove(tmp)
    m = re.search(r"\{[^{}]*\"input_i\"[^{}]*\}", err, re.S)
    if not m:
        return None, None
    d = json.loads(m.group(0))
    try:
        i = float(d["input_i"])
    except ValueError:
        i = None
    try:
        tp = float(d["input_tp"])
    except ValueError:
        tp = None
    if i is not None and i < -70:
        i = None
    return i, tp


def peak_db(x):
    p = float(np.max(np.abs(x))) if x.size else 0.0
    return 20 * np.log10(max(p, 1e-9))


def limit(x, ceiling_db, circular=False):
    """alimiter with its look-ahead delay compensated (latency=1). circular: a loop is limited with its own tail in
    front and head behind, so the gain at the seam matches on both sides (no click where the loop wraps)."""
    pad = min(int(0.5 * SR), len(x) // 4) if circular else 0
    src = np.concatenate([x[-pad:], x, x[:pad]]) if pad else x
    with tempfile.TemporaryDirectory() as d:
        a, b = os.path.join(d, "a.wav"), os.path.join(d, "b.wav")
        write_wav(a, src)
        lim = 10 ** ((ceiling_db - 0.5) / 20)
        ff("-i", a, "-af", "alimiter=limit=%.5f:attack=2:release=50:level=disabled:latency=1" % lim, "-ar", str(SR), "-c:a", "pcm_f32le", b)
        y = decode(b, x.shape[1])
    y = y[pad: pad + len(x)]
    n = min(len(x), len(y))
    y = y[:n]
    # alimiter has a few ms of look-ahead latency; a hard safety clip catches anything left
    c = 10 ** (ceiling_db / 20)
    return np.clip(y, -c, c)


def to_lufs(x, target, ceiling, max_limit_db=None, loop=False):
    """Gain to `target` LUFS, peak-safe: true peak stays under `ceiling` (0.7 dB headroom for the Vorbis encoder).
    max_limit_db (SFX): at most this much limiter gain reduction — a short click is not squashed to reach the
    target, it just ends up a bit quieter than -14 LUFS."""
    i, tp = measure(x)
    ceil = ceiling - 0.7
    pk = tp if tp is not None else peak_db(x)
    if i is None:
        return x * (10 ** ((ceil - pk) / 20))
    g = target - i
    if max_limit_db is not None:
        g = min(g, ceil - pk + max_limit_db)
    y = x * (10 ** (g / 20))
    if pk + g > ceil:
        y = limit(y, ceil, loop)
    return y


def envelope_db(x, win=0.05):
    mono = np.mean(x, axis=1)
    n = max(int(SR * win), 1)
    k = len(mono) // n
    if k == 0:
        return np.array([-120.0]), n
    rms = np.sqrt(np.mean(mono[: k * n].reshape(k, n) ** 2, axis=1) + 1e-12)
    return 20 * np.log10(rms), n


def trim(x, start_db=-50.0, end_db=-55.0, pad=0.01):
    mono = np.max(np.abs(x), axis=1)
    sm = np.convolve(mono, np.ones(64) / 64, mode="same")
    a = np.where(sm > 10 ** (start_db / 20))[0]
    b = np.where(sm > 10 ** (end_db / 20))[0]
    if a.size == 0 or b.size == 0:
        return x
    s = max(int(a[0] - pad * SR), 0)
    e = min(int(b[-1] + pad * SR), len(x))
    return x[s:e]


def fade(x, fin=0.004, fout=0.05):
    y = x.copy()
    n_in, n_out = min(int(fin * SR), len(y)), min(int(fout * SR), len(y))
    if n_in > 0:
        y[:n_in] *= np.linspace(0, 1, n_in)[:, None]
    if n_out > 0:
        y[-n_out:] *= (np.linspace(1, 0, n_out) ** 2)[:, None]
    return y


def cap_length(x, seconds, fout=0.15):
    n = int(seconds * SR)
    if len(x) <= n:
        return x
    return fade(x[:n], 0.0, fout)


def cut_tail_fade(x, max_cut=14.0, max_head=8.0):
    """Music loops: drop a slow fade-in head and a fade-out/ring-out tail so the seam crossfades full-energy material."""
    env, n = envelope_db(x, 0.1)
    if env.size < 20:
        return x
    ref = float(np.median(env[len(env) // 4: 3 * len(env) // 4]))
    keep = len(env)
    lim = max(len(env) - int(max_cut / 0.1), 0)
    for i in range(len(env) - 1, lim, -1):
        if env[i] > ref - 8.0:
            keep = i + 1
            break
    start = 0
    for i in range(0, min(int(max_head / 0.1), keep)):
        if env[i] > ref - 12.0:
            start = i
            break
    return x[start * n: keep * n]


def loop_seam(x, xfade):
    """Seamless loop: tail crossfaded (equal power) into the head; output length = len - xfade."""
    n = int(xfade * SR)
    if n <= 0 or len(x) < 3 * n:
        return x
    head, body = x[:n], x[n:]
    t = np.linspace(0, np.pi / 2, n)[:, None]
    tail = body[-n:] * np.cos(t) + head * np.sin(t)
    return np.concatenate([body[:-n], tail])


def seam_jump(x):
    """Sample jump at the loop point (last → first sample), worst channel, relative to the clip's own 99th-percentile
    sample-to-sample step (noisy beds — water, wind — jump a lot between any two samples). > ~1.5 risks a click."""
    d = np.abs(np.diff(x, axis=0))
    ref = max(float(np.percentile(d, 99)), 0.02)   # Vorbis leaves ~0.02 of end-of-stream error: inaudible, not a click
    return float(np.max(np.abs(x[-1] - x[0]))) / ref


def edge_rms_ratio_db(x, win=0.02):
    """Loudness step across the seam: RMS of the last `win` s vs the first `win` s (dB)."""
    n = int(win * SR)
    a = np.sqrt(np.mean(x[-n:] ** 2) + 1e-12)
    b = np.sqrt(np.mean(x[:n] ** 2) + 1e-12)
    return float(20 * np.log10(a / b))


def silence_edges(x, thr_db=-50.0):
    mono = np.max(np.abs(x), axis=1)
    idx = np.where(mono > 10 ** (thr_db / 20))[0]
    if idx.size == 0:
        return len(x) / SR, len(x) / SR
    return idx[0] / SR, (len(x) - 1 - idx[-1]) / SR


def encode_ogg(x, path, quality, channels):
    """OGG Vorbis via libsndfile (Homebrew ffmpeg has no libvorbis). quality = Vorbis -q (0…10)."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    y = np.clip(x, -1.0, 1.0).astype(np.float32)
    if channels == 1 and y.shape[1] > 1:
        y = np.mean(y, axis=1, keepdims=True)
    level = max(0.0, min(1.0, 1.0 - quality / 10.0))
    tmp = path + ".tmp.ogg"
    if shutil.which("ffmpeg") and b"libvorbis" in subprocess.run(["ffmpeg", "-hide_banner", "-encoders"], capture_output=True).stdout:
        # Linux ffmpeg has libvorbis; libsndfile 1.2.x crashed (silently) writing 60 s stereo Vorbis on Ubuntu 24.04
        wav = path + ".tmp.wav"
        sf.write(wav, y if channels > 1 else y[:, 0], SR, subtype="FLOAT")
        ff("-i", wav, "-c:a", "libvorbis", "-q:a", str(quality), "-ac", str(channels), tmp)
        os.remove(wav)
    else:
        sf.write(tmp, y if channels > 1 else y[:, 0], SR, format="OGG", subtype="VORBIS", compression_level=level)
    os.replace(tmp, path)
