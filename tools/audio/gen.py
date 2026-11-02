#!/usr/bin/env python3
# recycle-game copy of games/tools/audio (CoddyApps). Added --out for Unity games (flat Resources/audio/<id>.ogg).
"""Fleet audio tool: ElevenLabs SFX / music / ambience → trimmed, loudness-normalised OGG Vorbis, manifest-driven.

    PY=~/.venvs/coddy-audio/bin/python          # numpy + soundfile (see README.md)
    $PY games/tools/audio/gen.py games/<game> --dry-run        # what is missing + credit estimate
    $PY games/tools/audio/gen.py games/<game>                  # generate everything missing (sequential)
    $PY games/tools/audio/gen.py games/<game> --only ui_tap,coin --force   # redo some ids (new API call)
    $PY games/tools/audio/gen.py games/<game> --reprocess      # rerun post-processing from the raw cache (no API)
    $PY games/tools/audio/gen.py games/<game> --status         # QA table (duration, LUFS, peak, silence, seam)

Manifest: games/<game>/audio/manifest.json (schema in README.md). Output: games/<game>/audio/<kind>/<id>.ogg
(deterministic names; res://audio/sfx/<id>.ogg in Godot). QA: audio/qa.json. Usage: audio/usage_log.jsonl
(one line per API call with measured credit cost). Raw downloads: $CODDY_AUDIO_RAW/<game>/<id>.<spec-hash>.mp3
(outside the repo, reused unless --force). Before each batch and before every request the subscription is read;
generation stops when remaining credits would fall below --floor (default 15 % of the monthly limit).
"""
import argparse
import datetime
import hashlib
import json
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dsp  # noqa: E402
import el_api  # noqa: E402

KINDS = ("sfx", "music", "ambience")
LEVELS = {  # loudness targets per kind (override per asset with "lufs" / "tp")
    "sfx": {"lufs": -14.0, "tp": -1.5, "channels": 1, "q": 4},
    "music": {"lufs": -18.0, "tp": -1.5, "channels": 2, "q": 3},
    "ambience": {"lufs": -24.0, "tp": -3.0, "channels": 2, "q": 2},
}
RAW_ROOT = os.environ.get("CODDY_AUDIO_RAW") or (
    "/Volumes/BuildSSD/game-deps/audio-raw" if os.path.isdir("/Volumes/BuildSSD/game-deps") else os.path.expanduser("~/.cache/coddy-audio"))


def now():
    return datetime.datetime.now().isoformat(timespec="seconds")


def load(game_dir):
    p = os.path.join(game_dir, "audio", "manifest.json")
    m = json.load(open(p, encoding="utf-8"))
    ids = set()
    for a in m["assets"]:
        assert a["kind"] in KINDS, a
        assert a["id"] not in ids, "duplicate id %s" % a["id"]
        assert a["id"].replace("_", "").isalnum() and a["id"].islower(), "id must be lower_snake: %s" % a["id"]
        ids.add(a["id"])
    return m


SFX_MAX_CHARS = 450  # sound-generation API limit (validation error text_too_long)


def full_prompt(m, a):
    """prompt + the game's style line for that kind. Sound generation takes at most 450 characters: when the full style
    does not fit, only its last sentence (the "No voices, no music." constraints) is kept, then the text is cut."""
    style = "" if a.get("no_style") else ((m.get("style") or {}).get(a["kind"], "") or "").strip()
    p = (a["prompt"].strip() + (" " + style if style else "")).strip()
    if a["kind"] != "music" and len(p) > SFX_MAX_CHARS:
        tail = [s for s in style.replace("! ", ". ").split(". ") if s.strip()]
        last = tail[-1].strip() if tail else ""
        if last and not last.endswith("."):
            last += "."
        p = (a["prompt"].strip() + (" " + last if last else "")).strip()
        if len(p) > SFX_MAX_CHARS:
            p = p[:SFX_MAX_CHARS].rsplit(" ", 1)[0]
    return p


def spec_hash(m, a):
    s = json.dumps([a["kind"], full_prompt(m, a), a["duration"], bool(a.get("loop")), a.get("influence", 0.5)], sort_keys=True)
    return hashlib.sha1(s.encode()).hexdigest()[:8]


OUT_DIR = ""   # --out: flat <out>/<id>.ogg (Unity Resources/audio) instead of <game>/audio/<kind>/<id>.ogg


def out_path(game_dir, a):
    if OUT_DIR:
        return os.path.join(OUT_DIR, a["id"] + ".ogg")
    return os.path.join(game_dir, "audio", a["kind"], a["id"] + ".ogg")


def raw_path(game, m, a):
    d = os.path.join(RAW_ROOT, game)
    os.makedirs(d, exist_ok=True)
    return os.path.join(d, "%s.%s.mp3" % (a["id"], spec_hash(m, a)))


def estimate(a):
    """Credit estimate for the pre-request floor check; the real cost (response header `character-cost`) is logged.
    Measured 2026-10-21 (Starter): sound generation 10 credits/s, music 12.5 credits/s (750 per minute)."""
    if a["kind"] == "music":
        return int(round(a["duration"] * 12.5))
    return int(max(a["duration"], 0.5) * 12)


def log_usage(game_dir, **kw):
    kw["t"] = now()
    with open(os.path.join(game_dir, "audio", "usage_log.jsonl"), "a", encoding="utf-8") as f:
        f.write(json.dumps(kw, ensure_ascii=False) + "\n")


# ---------------------------------------------------------------------------------------------- post-processing
def process(m, a, rp, op):
    kind = a["kind"]
    lv = dict(LEVELS[kind])
    lv.update({k: a[k] for k in ("lufs", "tp", "q") if k in a})
    ch = lv["channels"]
    x = dsp.decode(rp, ch)
    if kind == "sfx":
        if a.get("loop"):
            x = dsp.loop_seam(x, min(0.25, len(x) / dsp.SR * 0.1))
        else:
            x = dsp.trim(x, -50, -55, 0.004)
            if "max_len" in a:
                x = dsp.cap_length(x, float(a["max_len"]))
            x = dsp.fade(x, 0.003, min(0.06, len(x) / dsp.SR * 0.2))
    elif kind == "music":
        x = dsp.trim(x, -50, -60, 0.0)
        if a.get("loop"):
            x = dsp.cut_tail_fade(x)
            x = dsp.loop_seam(x, 3.0)
        else:
            x = dsp.fade(x, 0.01, 0.5)
    else:  # ambience
        x = dsp.loop_seam(x, min(1.5, len(x) / dsp.SR * 0.1)) if a.get("loop", True) else dsp.fade(x, 0.05, 0.5)
    loop = bool(a.get("loop", kind == "ambience"))
    x = dsp.to_lufs(x, lv["lufs"], lv["tp"], 4.0 if kind == "sfx" else None, loop)
    dsp.encode_ogg(x, op, lv["q"], ch)


def analyze(path, loop):
    x = dsp.decode(path, dsp.sf.info(path).channels)
    i, tp = dsp.measure(x)
    head, tail = dsp.silence_edges(x)
    r = {"dur": round(len(x) / dsp.SR, 2), "lufs": None if i is None else round(i, 1), "tp": None if tp is None else round(tp, 1),
         "peak": round(dsp.peak_db(x), 1), "head_sil": round(head, 3), "tail_sil": round(tail, 3),
         "kb": round(os.path.getsize(path) / 1024, 1), "ch": x.shape[1]}
    if loop:
        r["seam_jump"] = round(dsp.seam_jump(x), 3)
        r["seam_step_db"] = round(dsp.edge_rms_ratio_db(x), 1)
    flags = []
    if r["peak"] > -0.9:
        flags.append("hot")
    if not loop and r["head_sil"] > 0.05:
        flags.append("head-silence")
    if loop and r["seam_jump"] > 1.5:   # seam_step_db is informative: a rest before a downbeat is music, not a click
        flags.append("seam")
    if r["lufs"] is not None and r["lufs"] < -40:
        flags.append("quiet")
    r["flags"] = flags
    return r


def write_qa(game_dir, m):
    qa = {}
    total = 0
    for a in m["assets"]:
        op = out_path(game_dir, a)
        if os.path.exists(op):
            qa[a["id"]] = dict(kind=a["kind"], **analyze(op, bool(a.get("loop"))))
            total += os.path.getsize(op)
    json.dump({"total_mb": round(total / 1048576, 2), "assets": qa}, open(os.path.join(game_dir, "audio", "qa.json"), "w"), indent=1)
    return qa, total


def status(game_dir, m):
    qa, total = write_qa(game_dir, m)
    print("%-22s %-8s %6s %6s %6s %6s %6s %7s  %s" % ("id", "kind", "dur", "LUFS", "TP", "head", "tail", "KB", "flags/seam"))
    for a in m["assets"]:
        r = qa.get(a["id"])
        if not r:
            print("%-22s %-8s  MISSING" % (a["id"], a["kind"]))
            continue
        extra = " seam=%.2fx/%+.1fdB" % (r["seam_jump"], r["seam_step_db"]) if "seam_jump" in r else ""
        print("%-22s %-8s %6.2f %6s %6s %6.3f %6.3f %7.1f  %s%s" % (a["id"], a["kind"], r["dur"], r["lufs"], r["tp"], r["head_sil"],
                                                               r["tail_sil"], r["kb"], ",".join(r["flags"]), extra))
    print("total %.2f MB, %d/%d assets" % (total / 1048576, len(qa), len(m["assets"])))


# ---------------------------------------------------------------------------------------------- main
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("game_dir")
    ap.add_argument("--only", default="")
    ap.add_argument("--force", action="store_true", help="new API call even if raw/output exist (with --only)")
    ap.add_argument("--reprocess", action="store_true", help="post-process again from the raw cache (no API)")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--status", action="store_true")
    ap.add_argument("--floor", type=float, default=0.15, help="stop when remaining credits / limit would drop below this")
    ap.add_argument("--gap", type=float, default=1.5, help="seconds between API calls")
    ap.add_argument("--out", default="", help="flat output dir (Unity: <game>/unity/Assets/Resources/audio)")
    args = ap.parse_args()
    game_dir = os.path.abspath(args.game_dir)
    global OUT_DIR
    OUT_DIR = os.path.abspath(args.out) if args.out else ""
    game = os.path.basename(game_dir.rstrip("/"))
    m = load(game_dir)
    if args.status:
        status(game_dir, m)
        return 0
    only = set(filter(None, args.only.split(",")))
    todo = []
    for a in m["assets"]:
        if only and a["id"] not in only:
            continue
        op, rp = out_path(game_dir, a), raw_path(game, m, a)
        if args.reprocess:
            if os.path.exists(rp):
                todo.append((a, False))
            continue
        if os.path.exists(op) and not args.force:
            continue
        todo.append((a, args.force or not os.path.exists(rp)))
    need = [a for a, call in todo if call]
    est = sum(estimate(a) for a in need)
    print("%s: %d to build, %d API calls (~%d credits est.), raw cache %s" % (game, len(todo), len(need), est, os.path.join(RAW_ROOT, game)))
    if args.dry_run:
        for a, call in todo:
            print("   %-22s %-8s %5.1fs %s" % (a["id"], a["kind"], a["duration"], "API" if call else "cached raw"))
        return 0
    sub = el_api.subscription() if need else None
    if sub:
        print("credits: %d / %d remaining (%.1f%%), floor %.0f%%" % (sub["remaining"], sub["limit"], 100 * sub["remaining_frac"], 100 * args.floor))
        log_usage(game_dir, kind="batch_start", remaining=sub["remaining"], limit=sub["limit"], calls=len(need), est=est)
    spent = 0
    ncalls = 0
    spent_since_read = 0
    for a, call in todo:
        op, rp = out_path(game_dir, a), raw_path(game, m, a)
        if call:
            floor = int(args.floor * sub["limit"])
            if sub["remaining"] - spent_since_read - estimate(a) < floor:
                print("STOP: %d credits left, floor %d — %s not generated" % (sub["remaining"], floor, a["id"]))
                log_usage(game_dir, kind="stop_floor", id=a["id"], remaining=sub["remaining"], floor=floor)
                break
            t0 = time.time()
            try:
                if a["kind"] == "music":
                    blob, hdr = el_api.music(full_prompt(m, a), a["duration"])
                else:
                    blob, hdr = el_api.sfx(full_prompt(m, a), a["duration"], a.get("influence", 0.5),
                                           bool(a.get("loop", a["kind"] == "ambience")))
            except RuntimeError as e:
                print("   ! %s: %s" % (a["id"], e))
                log_usage(game_dir, kind="error", id=a["id"], err=str(e)[:300])
                time.sleep(args.gap)
                continue
            open(rp, "wb").write(blob)
            hc = hdr.get("character-cost") or hdr.get("Character-Cost")
            # music has no cost header and the subscription counter lags by minutes: use the measured rate
            # (Starter, 2026-10-21: 60 s of music = 750 credits); the batch_start lines carry the real totals.
            cost = int(float(hc)) if hc is not None else estimate(a)
            spent += cost
            spent_since_read += cost
            ncalls += 1
            if ncalls % 10 == 0 or hc is None:
                fresh = el_api.subscription()
                sub["remaining"] = min(sub["remaining"] - spent_since_read, fresh["remaining"])
                spent_since_read = 0
            rem = sub["remaining"] - spent_since_read
            log_usage(game_dir, kind=a["kind"], id=a["id"], seconds=a["duration"], credits=cost, remaining=rem,
                      cost_from="header" if hc is not None else "rate",
                      hash=spec_hash(m, a), took=round(time.time() - t0, 1))
            print("   + %-22s %-8s %5.1fs  %4d credits  (left %d)" % (a["id"], a["kind"], a["duration"], cost, rem), flush=True)
            time.sleep(args.gap)
        try:
            process(m, a, rp, op)
        except Exception as e:  # keep the batch going; the raw stays cached
            print("   ! process %s: %s" % (a["id"], e))
    if need:
        rem = sub["remaining"] - spent_since_read
        log_usage(game_dir, kind="batch_end", spent=spent, remaining=rem)
        print("spent %d credits this batch; %d remaining (%.1f%%)" % (spent, rem, 100 * rem / sub["limit"]))
    status(game_dir, m)
    return 0


if __name__ == "__main__":
    sys.exit(main())
