#!/usr/bin/env python3
"""Meshy API client for the CoddyApps games (owner 2026-10-23: Meshy for characters / creatures / organic hero props).

Every paid call goes through `create()`: price from PRICES (docs.meshy.ai/api/pricing, read 2026-10-23) -> cap check
against the ledger -> POST -> ledger entry. `wait()` polls (honours Retry-After), then writes the real
`consumed_credits` into the ledger (0 for FAILED: Meshy refunds failed tasks).

The API key is read from ~/.config/meshy/api_key (or MESHY_KEY_FILE) and only ever goes into the Authorization header
of requests to api.meshy.ai. It is never printed, logged, passed on a command line or sent to the asset CDN.

  python3 meshy.py balance
  python3 meshy.py price text-to-3d-preview --ai_model meshy-t2.5
  python3 meshy.py preview  <asset> "<prompt>" [--ai_model meshy-t2.5] [--polycount 3000] [--pose a-pose]
  python3 meshy.py refine   <asset> <preview_task_id> "<texture prompt>" [--ai_model meshy-6]
  python3 meshy.py image    <asset> <image path|url> [--ai_model meshy-6-lite] [--polycount 3000] [--no-texture]
  python3 meshy.py rig      <asset> <textured task id> [--height 1.72]
  python3 meshy.py animate  <asset> <rig task id> <action_id> [<action_id> ...]
  python3 meshy.py remesh   <asset> <task id> --polycount 3000
  python3 meshy.py wait     <kind> <task id>          (kind: text-to-3d | image-to-3d | rigging | animations | remesh)
  python3 meshy.py download <kind> <task id> <out dir> [--formats glb,fbx]
  python3 meshy.py library  [search]                  (animation library, free)
  python3 meshy.py ledger

The pilot cap applies while the ledger has "pilot_cap" and no "pilot_closed": true; set "pilot_closed" to move on to
the monthly plan (checked per calendar month from the task times).
Several keys: MESHY_KEY=2 selects ~/.config/meshy/api_key_2; ledger entries carry "key", the ledger keeps
spent_by_key, and "key_caps" {"1": N, "2": M} caps each key's total (the monthly check is per key: key_plans).
Environment: MESHY_LEDGER (default ~/.config/meshy/ledger.json), MESHY_CAP (overrides the ledger's pilot_cap),
MESHY_DRY=1 (price + cap check only, no POST).
"""
import argparse
import base64
import contextlib
import fcntl
import json
import mimetypes
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone

API = "https://api.meshy.ai"
# MESHY_KEY=1 (default, ~/.config/meshy/api_key) or 2 (~/.config/meshy/api_key_2, owner 10-27). Tasks belong to the key
# that created them: refine / rig / animate / wait / download must run with the same MESHY_KEY as the preview.
KEY_ID = os.environ.get("MESHY_KEY", "1")
KEY_FILE = os.environ.get("MESHY_KEY_FILE", os.path.expanduser(
    "~/.config/meshy/api_key" if KEY_ID == "1" else f"~/.config/meshy/api_key_{KEY_ID}"))
LEDGER = os.environ.get("MESHY_LEDGER", os.path.expanduser("~/.config/meshy/ledger.json"))

# Credits per request (docs.meshy.ai/api/pricing, 2026-10-23).
LITE_MODELS = {"meshy-6-lite", "meshy-t2", "meshy-t2.5"}
PATHS = {
    "text-to-3d": "/openapi/v2/text-to-3d",
    "image-to-3d": "/openapi/v1/image-to-3d",
    "rigging": "/openapi/v1/rigging",
    "animations": "/openapi/v1/animations",
    "remesh": "/openapi/v1/remesh",
    "balance": "/openapi/v1/balance",
    "library": "/openapi/v1/animations/library",
}


def price(kind, body):
    """Credits a request costs, from the published table. Raises on a combination the table does not list."""
    model = body.get("ai_model", "latest")
    ultra = 5 if body.get("geometry_resolution") in ("2k", "4k") else 0
    if kind == "text-to-3d-preview":
        return (5 if model in LITE_MODELS else 20) + ultra
    if kind == "text-to-3d-refine":
        return 15 if body.get("texture_resolution") == "8k" else 10
    if kind == "image-to-3d":
        lite = model in LITE_MODELS
        if not body.get("should_texture", True):
            return (5 if lite else 20) + ultra
        res = body.get("texture_resolution", "2k")
        if res == "8k":
            return (20 if lite else 35) + ultra
        return (15 if lite else 30) + ultra
    if kind == "rigging":
        return 5
    if kind == "animations":
        ids = body.get("action_ids") or [body.get("action_id")]
        return 3 * len(ids)
    if kind == "remesh":
        return 5
    raise ValueError("no price for " + kind)


# ---------------------------------------------------------------- HTTP (the key stays inside this module)
def _key():
    with open(KEY_FILE) as f:
        k = f.read().strip()
    if not k:
        sys.exit("meshy: empty key file")
    return k


def _req(method, path, body=None, tries=6):
    data = None if body is None else json.dumps(body).encode()
    for attempt in range(tries):
        req = urllib.request.Request(API + path, data=data, method=method)
        req.add_header("Authorization", "Bearer " + _key())
        req.add_header("Content-Type", "application/json")
        try:
            with urllib.request.urlopen(req, timeout=120) as r:
                raw = r.read()
                return json.loads(raw) if raw else {}, dict(r.headers)
        except urllib.error.HTTPError as e:
            msg = e.read().decode(errors="replace")[:400]
            if e.code == 429 and "NoMore" in msg:
                # too many concurrent tasks: no timer retry; the caller must wait for a task to finish
                raise RuntimeError("meshy 429 concurrency: " + msg)
            if e.code == 429 or e.code >= 500:
                wait = int(e.headers.get("Retry-After") or 0) or min(60, 5 * (attempt + 1))
                print(f"meshy: HTTP {e.code} on {method} {path.split('?')[0]}, retry in {wait}s", file=sys.stderr)
                time.sleep(wait)
                continue
            raise RuntimeError(f"meshy HTTP {e.code} on {method} {path.split('?')[0]}: {msg}")
        except urllib.error.URLError as e:
            print(f"meshy: network error {e.reason}, retry", file=sys.stderr)
            time.sleep(5 * (attempt + 1))
    raise RuntimeError(f"meshy: gave up on {method} {path.split('?')[0]}")


def balance():
    j, _ = _req("GET", PATHS["balance"])
    return j.get("balance", j)


# ---------------------------------------------------------------- ledger
@contextlib.contextmanager
def ledger_lock():
    """Several `wait` / `create` processes run in parallel: serialise every load -> modify -> save (10-27: two
    parallel saves left trailing bytes in the JSON)."""
    with open(LEDGER + ".lock", "a") as lk:
        fcntl.flock(lk, fcntl.LOCK_EX)
        try:
            yield
        finally:
            fcntl.flock(lk, fcntl.LOCK_UN)


def load_ledger():
    with open(LEDGER) as f:
        led = json.load(f)
    led.setdefault("tasks", [])
    led.setdefault("spent", 0)
    return led


def save_ledger(led):
    led["spent"] = sum(int(t.get("credits", 0)) for t in led["tasks"])
    led["pilot_spent"] = sum(int(t.get("credits", 0)) for t in led["tasks"] if t.get("pilot"))
    by = {}
    for t in led["tasks"]:
        k = str(t.get("key", "1"))
        by[k] = by.get(k, 0) + int(t.get("credits", 0))
    led["spent_by_key"] = by
    tmp = f"{LEDGER}.{os.getpid()}.tmp"
    with open(tmp, "w") as f:
        json.dump(led, f, indent=1)
    os.chmod(tmp, 0o600)
    os.replace(tmp, LEDGER)


def _now():
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def create(kind, asset, body, note=""):
    """Price -> cap check -> POST -> ledger. kind: text-to-3d-preview | text-to-3d-refine | image-to-3d | rigging |
    animations | remesh."""
    cost = price(kind, body)
    with ledger_lock():
        return _create_locked(kind, asset, body, note, cost)


def _create_locked(kind, asset, body, note, cost):
    led = load_ledger()
    pilot = led.get("pilot_cap") is not None and not led.get("pilot_closed")
    mine = [t for t in led["tasks"] if str(t.get("key", "1")) == KEY_ID]
    if led.get("key_caps"):
        cap = int(os.environ.get("MESHY_CAP") or led["key_caps"].get(KEY_ID, 0))
        spent_now = sum(int(t.get("credits", 0)) for t in mine)
    else:
        cap = int(os.environ.get("MESHY_CAP") or led.get("pilot_cap") or led.get("plan_monthly", 1100))
        spent_now = sum(int(t.get("credits", 0)) for t in led["tasks"] if (t.get("pilot") or not pilot))
    if spent_now + cost > cap:
        raise SystemExit(f"meshy: REFUSED {kind} for {asset}: key {KEY_ID} {spent_now} + {cost} > cap {cap}")
    month = _now()[:7]
    plan = int((led.get("key_plans") or {}).get(KEY_ID, led.get("plan_monthly", 1100)))
    month_spent = sum(int(t.get("credits", 0)) for t in mine if str(t.get("time", "")).startswith(month))
    if month_spent + cost > plan:
        raise SystemExit(f"meshy: REFUSED {kind}: this month's plan would be exceeded ({month_spent} + {cost})")
    if os.environ.get("MESHY_DRY"):
        print(f"DRY {kind} {asset}: {cost} credits (spent {spent_now}/{cap})")
        return None
    path = PATHS["text-to-3d" if kind.startswith("text-to-3d") else kind]
    j, _ = _req("POST", path, body)
    tid = j.get("result")
    entry = {"time": _now(), "asset": asset, "type": kind, "task_id": tid, "credits": cost, "estimated": cost,
             "status": "CREATED", "pilot": pilot, "note": note, "key": KEY_ID}
    for k in ("prompt", "texture_prompt", "ai_model", "target_polycount", "action_ids", "action_id", "pose_mode"):
        if k in body:
            entry[k] = body[k]
    led["tasks"].append(entry)
    save_ledger(led)
    print(f"CREATED {kind} {asset} task={tid} est={cost} (key {KEY_ID} spent {spent_now + cost}/{cap})")
    left = plan - month_spent - cost
    if left <= int(led.get("alert_at_remaining", 100)):
        print(f"meshy: WARNING only {left} credits left this month ({led.get('on_exhaust', 'ask the owner')})")
    return tid


def get(kind, tid):
    j, h = _req("GET", f"{PATHS[kind]}/{tid}")
    return j, h


def wait(kind, tid, timeout=1800):
    t0 = time.time()
    last = -1
    while True:
        j, h = get(kind, tid)
        st = j.get("status")
        if j.get("progress") != last:
            last = j.get("progress")
            print(f"  {kind} {tid[-12:]} {st} {last}%", file=sys.stderr)
        if st in ("SUCCEEDED", "FAILED", "CANCELED"):
            with ledger_lock():
                led = load_ledger()
                for t in led["tasks"]:
                    if t.get("task_id") == tid:
                        t["status"] = st
                        if "consumed_credits" in j:
                            t["credits"] = int(j["consumed_credits"])
                        elif st != "SUCCEEDED":
                            t["credits"] = 0
                        if st != "SUCCEEDED":
                            t["error"] = str(j.get("task_error"))[:300]
                save_ledger(led)
            return j
        if time.time() - t0 > timeout:
            raise RuntimeError(f"meshy: timeout waiting for {tid}")
        time.sleep(max(5, int(h.get("Retry-After") or h.get("retry-after") or 0)))


def _fetch(url, path):
    """Download an asset URL (signed CDN link, no Authorization header)."""
    for attempt in range(4):
        try:
            with urllib.request.urlopen(urllib.request.Request(url), timeout=300) as r, open(path, "wb") as f:
                while True:
                    b = r.read(1 << 20)
                    if not b:
                        break
                    f.write(b)
            return path
        except Exception as e:  # noqa: BLE001
            print(f"meshy: download retry ({type(e).__name__})", file=sys.stderr)
            time.sleep(5 * (attempt + 1))
    raise RuntimeError("meshy: download failed for " + os.path.basename(path))


def download(kind, tid, out, formats=("glb",)):
    os.makedirs(out, exist_ok=True)
    j, _ = get(kind, tid)
    got = []
    urls = dict(j.get("model_urls") or {})
    res = j.get("result") if isinstance(j.get("result"), dict) else {}
    for k, v in res.items():
        if isinstance(v, str) and v.startswith("http"):
            urls[k] = v
        if isinstance(v, dict):
            for k2, v2 in v.items():
                if isinstance(v2, str) and v2.startswith("http"):
                    urls[k2] = v2
    for name, url in urls.items():
        ext = name.rsplit("_", 1)[-1] if "_url" not in name else name.replace("_url", "").rsplit("_", 1)[-1]
        if formats and ext not in formats and name not in formats:
            continue
        fn = f"{tid[-12:]}_{name.replace('_url', '')}.{ext if ext in ('glb', 'fbx', 'obj', 'usdz') else 'bin'}"
        got.append(_fetch(url, os.path.join(out, fn)))
    for i, tex in enumerate(j.get("texture_urls") or []):
        for name, url in tex.items():
            if isinstance(url, str) and url.startswith("http"):
                got.append(_fetch(url, os.path.join(out, f"{tid[-12:]}_{name}{i or ''}.png")))
    if j.get("thumbnail_url"):
        got.append(_fetch(j["thumbnail_url"], os.path.join(out, f"{tid[-12:]}_thumb.png")))
    meta = {k: v for k, v in j.items() if k not in ("model_urls", "texture_urls", "thumbnail_url", "result",
                                                    "thumbnail_urls", "alpha_thumbnail_url")}
    with open(os.path.join(out, f"{tid[-12:]}_task.json"), "w") as f:
        json.dump(meta, f, indent=1)
    for p in got:
        print("  ", p)
    return got


def _image_arg(s):
    if s.startswith("http") or s.startswith("data:"):
        return s
    mt = mimetypes.guess_type(s)[0] or "image/png"
    with open(s, "rb") as f:
        return f"data:{mt};base64," + base64.b64encode(f.read()).decode()


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sp = ap.add_subparsers(dest="cmd", required=True)
    sp.add_parser("balance")
    sp.add_parser("ledger")
    p = sp.add_parser("price")
    p.add_argument("kind")
    p.add_argument("--ai_model", default="latest")
    p = sp.add_parser("preview")
    p.add_argument("asset"); p.add_argument("prompt")
    p.add_argument("--ai_model", default="meshy-t2.5"); p.add_argument("--polycount", type=int)
    p.add_argument("--pose", default=""); p.add_argument("--note", default="")
    p = sp.add_parser("refine")
    p.add_argument("asset"); p.add_argument("preview_id"); p.add_argument("texture_prompt")
    p.add_argument("--ai_model"); p.add_argument("--pbr", action="store_true"); p.add_argument("--note", default="")
    p = sp.add_parser("image")
    p.add_argument("asset"); p.add_argument("image")
    p.add_argument("--ai_model", default="meshy-6-lite"); p.add_argument("--polycount", type=int)
    p.add_argument("--no-texture", action="store_true"); p.add_argument("--texture_prompt")
    p.add_argument("--pose", default=""); p.add_argument("--note", default="")
    p = sp.add_parser("rig")
    p.add_argument("asset"); p.add_argument("task_id"); p.add_argument("--height", type=float, default=1.7)
    p.add_argument("--model_file"); p.add_argument("--texture_file"); p.add_argument("--note", default="")
    p = sp.add_parser("animate")
    p.add_argument("asset"); p.add_argument("rig_id"); p.add_argument("actions", nargs="+", type=int)
    p.add_argument("--note", default="")
    p = sp.add_parser("remesh")
    p.add_argument("asset"); p.add_argument("task_id"); p.add_argument("--polycount", type=int, default=3000)
    p = sp.add_parser("wait")
    p.add_argument("kind"); p.add_argument("task_id")
    p = sp.add_parser("download")
    p.add_argument("kind"); p.add_argument("task_id"); p.add_argument("out")
    p.add_argument("--formats", default="glb")
    p = sp.add_parser("library")
    p.add_argument("search", nargs="?")
    a = ap.parse_args()

    if a.cmd == "balance":
        print(f"balance key {KEY_ID}:", balance())
    elif a.cmd == "ledger":
        led = load_ledger()
        print(f"spent {led['spent']} / plan {led.get('plan_monthly')}  by key {led.get('spent_by_key')} caps {led.get('key_caps')}")
        per = {}
        for t in led["tasks"]:
            per[t["asset"]] = per.get(t["asset"], 0) + int(t.get("credits", 0))
            print(f"  {t['time']} k{t.get('key', '1')} {t['asset']:<14} {t['type']:<20} {t.get('status',''):<10} {t.get('credits')} {t.get('task_id')}")
        print("per asset:", per)
    elif a.cmd == "price":
        print(price(a.kind, {"ai_model": a.ai_model}))
    elif a.cmd == "preview":
        body = {"mode": "preview", "prompt": a.prompt, "ai_model": a.ai_model, "target_formats": ["glb"]}
        if a.ai_model in ("meshy-t2", "meshy-t2.5"):
            body["model_type"] = "smart-topology"
        elif a.polycount:
            body["should_remesh"] = True
        if a.polycount:
            body["target_polycount"] = a.polycount
        if a.pose:
            body["pose_mode"] = a.pose
        create("text-to-3d-preview", a.asset, body, a.note)
    elif a.cmd == "refine":
        body = {"mode": "refine", "preview_task_id": a.preview_id, "texture_prompt": a.texture_prompt,
                "enable_pbr": a.pbr, "target_formats": ["glb"]}
        if a.ai_model:
            body["ai_model"] = a.ai_model
        create("text-to-3d-refine", a.asset, body, a.note)
    elif a.cmd == "image":
        body = {"image_url": _image_arg(a.image), "ai_model": a.ai_model, "should_texture": not a.no_texture,
                "target_formats": ["glb"]}
        if a.ai_model in ("meshy-t2", "meshy-t2.5"):
            body["model_type"] = "smart-topology"
        elif a.polycount:
            body["should_remesh"] = True
        if a.polycount:
            body["target_polycount"] = a.polycount
        if a.texture_prompt:
            body["texture_prompt"] = a.texture_prompt
        if a.pose:
            body["pose_mode"] = a.pose
        create("image-to-3d", a.asset, body, a.note)
    elif a.cmd == "rig":
        body = {"height_meters": a.height}
        if a.model_file:
            body["model_url"] = _image_arg(a.model_file) if a.model_file.startswith("http") else \
                "data:model/gltf-binary;base64," + base64.b64encode(open(a.model_file, "rb").read()).decode()
            if a.texture_file:
                body["texture_image_url"] = _image_arg(a.texture_file)
        else:
            body["input_task_id"] = a.task_id
        create("rigging", a.asset, body, a.note)
    elif a.cmd == "animate":
        body = {"rig_task_id": a.rig_id}
        if len(a.actions) == 1:
            body["action_id"] = a.actions[0]
        else:
            body["action_ids"] = a.actions
        create("animations", a.asset, body, a.note)
    elif a.cmd == "remesh":
        create("remesh", a.asset, {"input_task_id": a.task_id, "target_polycount": a.polycount,
                                   "target_formats": ["glb"]})
    elif a.cmd == "wait":
        j = wait(a.kind, a.task_id)
        print(j.get("status"), "consumed", j.get("consumed_credits"), str(j.get("task_error") or "")[:200])
    elif a.cmd == "download":
        download(a.kind, a.task_id, a.out, tuple(a.formats.split(",")) if a.formats else ())
    elif a.cmd == "library":
        path = PATHS["library"] + (f"?search={urllib.parse.quote(a.search)}" if a.search else "")
        j, _ = _req("GET", path)
        items = j if isinstance(j, list) else j.get("result", j.get("data", j))
        for it in items if isinstance(items, list) else []:
            print(it.get("action_id"), "|", it.get("name"), "|", it.get("category"), "|", it.get("sub_category", ""))


if __name__ == "__main__":
    main()
