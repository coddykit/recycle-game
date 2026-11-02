"""Minimal ElevenLabs client for the fleet audio tool (games/tools/audio/gen.py).

Key: ~/.config/elevenlabs/api_key (chmod 600) or $ELEVENLABS_API_KEY. The key is never printed, logged or written;
every error message is redacted. 429 / 5xx are retried with backoff (429 waits 60 s, as the fleet rule says).
"""
import json
import os
import time
import urllib.error
import urllib.request

API = "https://api.elevenlabs.io"
KEY_FILE = os.path.expanduser("~/.config/elevenlabs/api_key")
_key = None


def key() -> str:
    global _key
    if _key:
        return _key
    k = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not k and os.path.exists(KEY_FILE):
        k = open(KEY_FILE, encoding="utf-8").read().strip()
    if not k:
        raise SystemExit("ElevenLabs key missing (expected %s or $ELEVENLABS_API_KEY)" % KEY_FILE)
    _key = k
    return k


def _redact(s: str) -> str:
    return s.replace(_key, "***") if _key else s


def request(method, path, body=None, query="", accept_json=True, timeout=300, retries=6):
    """Returns (payload, headers). payload = parsed JSON or raw bytes."""
    url = API + path + (("?" + query) if query else "")
    headers = {"xi-api-key": key()}
    data = None
    if body is not None:
        data = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"
    last = None
    for attempt in range(retries):
        req = urllib.request.Request(url, data=data, headers=headers, method=method)
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                raw = r.read()
                return (json.loads(raw) if accept_json else raw), dict(r.headers)
        except urllib.error.HTTPError as e:
            msg = _redact(e.read().decode("utf-8", "replace")[:400])
            last = RuntimeError("HTTP %d %s: %s" % (e.code, path, msg))
            if e.code == 429:
                print("   429 from ElevenLabs, waiting 60 s (attempt %d)" % (attempt + 1), flush=True)
                time.sleep(60)
                continue
            if e.code in (500, 502, 503, 504):
                time.sleep(8 * (attempt + 1))
                continue
            raise last from None
        except (urllib.error.URLError, TimeoutError, ConnectionError) as e:
            last = RuntimeError("network %s: %s" % (path, _redact(str(e))))
            time.sleep(8 * (attempt + 1))
    raise last


def subscription() -> dict:
    d, _ = request("GET", "/v1/user/subscription", timeout=30)
    used, lim = int(d.get("character_count", 0)), int(d.get("character_limit", 0))
    return {"tier": d.get("tier"), "used": used, "limit": lim, "remaining": lim - used,
            "remaining_frac": (lim - used) / lim if lim else 0.0, "reset_unix": d.get("next_character_count_reset_unix")}


def sfx(text: str, seconds: float, influence: float = 0.5, loop: bool = False):
    body = {"text": text, "duration_seconds": seconds, "prompt_influence": influence, "model_id": "eleven_text_to_sound_v2"}
    if loop:
        body["loop"] = True
    return request("POST", "/v1/sound-generation", body, query="output_format=mp3_44100_128", accept_json=False)


def music(prompt: str, seconds: float):
    body = {"prompt": prompt, "music_length_ms": int(seconds * 1000), "model_id": "music_v1", "force_instrumental": True}
    return request("POST", "/v1/music", body, query="output_format=mp3_44100_128", accept_json=False, timeout=900)
