"""Generate announcer + mentor voice lines (EN and TR) and AI sound effects
with ElevenLabs. The API key is read from ELEVENLABS_API_KEY and never stored
in the repository. Output: res://assets/audio/voice/<lang>/<key>.mp3 and
res://assets/audio/<sfx>.mp3. The game skips any line that is missing.

    export ELEVENLABS_API_KEY=...        # never commit this
    python tools/audio/elevenlabs_voice.py            # voices, both languages
    python tools/audio/elevenlabs_voice.py --sfx      # + sound effects
    python tools/audio/elevenlabs_voice.py --music    # + music loops
    python tools/audio/elevenlabs_voice.py --force    # regenerate
"""
import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.request

API = "https://api.elevenlabs.io/v1"
ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
VOICE_DIR = os.path.join(ROOT, "assets", "audio", "voice")
SFX_DIR = os.path.join(ROOT, "assets", "audio")

# premade / library voices (override with env vars)
VOICES = {
    "en": {"announcer": "N2lVS1w4EtoT3dr4eOWO",   # Callum - husky trickster
           "mentor": "pFZP5JQG7iQjIQuC4Bku"},     # Lily - velvety, mature
    "tr": {"announcer": "JC9WV662woJoMSO0pGgJ",   # Orhan - upbeat
           "mentor": "vWs6DV3PkGKMP80V1Hv6"},     # Dilek - mature, Istanbul
}

ANNOUNCER = {
    "en": {
        "welcome": "Welcome to the business, boss. Let's make some deliveries.",
        "welcome_back": "Welcome back, boss! The crew kept things moving.",
        "accept": "On it, boss.",
        "dispatch": "Wheels are rolling!",
        "delivered": "Cha-ching! Delivery complete.",
        "delivered_big": "Now that's a payday!",
        "failed": "Ouch. That one went sideways.",
        "event": "Boss, we've got a situation.",
        "upgrade": "Shiny new upgrade!",
        "garage": "Look at this place. We're moving up!",
        "new_vehicle": "Fresh wheels for the fleet!",
        "region": "New territory unlocked. Time to expand!",
        "depot": "New depot is open for business.",
        "heat_hot": "Things are heating up out there.",
        "heat_critical": "Heat is critical! Maybe lay low for a while.",
        "stage": "The empire grows!",
        "goal": "Goal complete. Nice work!",
        "automation": "Automation online. The network runs itself now.",
        "prestige": "A legend sells high. Time to build it all again!"
    },
    "tr": {
        "welcome": "İşe hoş geldin patron. Hadi biraz teslimat yapalım.",
        "welcome_back": "Tekrar hoş geldin patron! Ekip işleri yürüttü.",
        "accept": "Hemen hallediyorum patron.",
        "dispatch": "Tekerlekler dönüyor!",
        "delivered": "Şıkır şıkır! Teslimat tamam.",
        "delivered_big": "İşte buna maaş günü derim!",
        "failed": "Ah be. Bu iş ters gitti.",
        "event": "Patron, bir durumumuz var.",
        "upgrade": "Pırıl pırıl yeni geliştirme!",
        "garage": "Şu mekâna bak. Yükseliyoruz!",
        "new_vehicle": "Filoya taze tekerlekler!",
        "region": "Yeni bölge açıldı. Büyüme zamanı!",
        "depot": "Yeni depo hizmete açıldı.",
        "heat_hot": "Ortalık ısınıyor.",
        "heat_critical": "Isı kritik seviyede! Biraz ortadan kaybolsak iyi olur.",
        "stage": "İmparatorluk büyüyor!",
        "goal": "Hedef tamam. Harika iş!",
        "automation": "Otomasyon devrede. Ağ artık kendi kendine dönüyor.",
        "prestige": "Efsaneler pahalıya satar. Her şeyi yeniden kurma zamanı!"
    }
}

MENTOR = {
    "en": {
        "tut_contracts": "A client is calling! Open Contracts.",
        "tut_accept": "Easy job, zero risk. Accept it!",
        "tut_dispatch": "Pick your car and a route. Normal is fine. Hit Dispatch!",
        "tut_watch": "There she goes! Watch the delivery.",
        "tut_cash": "Cha-ching! Your first profit lands here.",
        "tut_upgrade": "Invest in your ride. A better engine means faster trips.",
        "tut_again": "Clients keep calling. Take another job!",
        "tut_garage": "Time to expand! A bigger garage means more vehicles.",
        "tut_region": "Green Republic is next door. Unlock it to grow your network!",
        "tut_done": "You're a natural, boss. I'll leave the empire to you!",
        "tab_fleet": "Your Fleet is open! Upgrade cars and hire drivers here.",
        "tab_upgrades": "Upgrades unlocked! Grow the garage and your business.",
        "tab_map": "The World Map is open! Plan your expansion.",
        "tab_business": "Business unlocked! Track your empire and automate it.",
        "mentor_hello": "Hey there, rookie! I'm Dot. Thirty years running dispatch in this valley.",
        "mentor_garage": "I'm retiring, so this little garage is yours now. One rusty hatchback, one driver, big dreams.",
        "mentor_regions": "Out there are eight regions full of clients who need things moved. Quickly. Quietly. No questions.",
        "mentor_border": "Every border, every route, every risk... from now on, that's your call.",
        "mentor_name": "First things first: every empire needs a name.",
        "mentor_go": "I like it! Now let's earn your first dollar."
    },
    "tr": {
        "tut_contracts": "Bir müşteri arıyor! Sözleşmeleri aç.",
        "tut_accept": "Kolay iş, sıfır risk. Kabul et!",
        "tut_dispatch": "Aracını ve rotanı seç. Normal yeterli. Yola Çıkar'a bas!",
        "tut_watch": "İşte gidiyor! Teslimatı izle.",
        "tut_cash": "Şıkır şıkır! İlk kârın buraya düşer.",
        "tut_upgrade": "Aracına yatırım yap. Daha iyi motor, daha hızlı seferler demek.",
        "tut_again": "Müşteriler aramaya devam ediyor. Bir iş daha al!",
        "tut_garage": "Büyüme zamanı! Daha büyük garaj, daha çok araç demek.",
        "tut_region": "Yeşil Cumhuriyet hemen yanımızda. Ağını büyütmek için kilidini aç!",
        "tut_done": "Doğuştan yeteneklisin patron. İmparatorluk artık sende!",
        "tab_fleet": "Filon açıldı! Araçları geliştir, şoför işe al.",
        "tab_upgrades": "Geliştirmeler açıldı! Garajını ve işini büyüt.",
        "tab_map": "Dünya Haritası açıldı! Genişlemeni planla.",
        "tab_business": "İşletme açıldı! İmparatorluğunu izle ve otomatikleştir.",
        "mentor_hello": "Selam çaylak! Ben Dot. Bu vadide otuz yıl sevkiyat yönettim.",
        "mentor_garage": "Emekli oluyorum, bu küçük garaj artık senin. Paslı bir hatchback, bir şoför ve büyük hayaller.",
        "mentor_regions": "Dışarıda, işini taşıtacak müşterilerle dolu sekiz bölge var. Hızlı. Sessiz. Soru sormadan.",
        "mentor_border": "Her sınır, her rota, her risk... artık senin kararın.",
        "mentor_name": "Önce en önemlisi: her imparatorluğun bir adı olmalı.",
        "mentor_go": "Sevdim! Hadi ilk doların peşine düşelim."
    }
}

MUSIC = {
    "music_main": "Playful laid-back lo-fi funk loop for a cozy mobile tycoon game, light jazzy guitar, muted bass, "
                  "soft brushed drums, sneaky heist vibe, warm and bright, no vocals, steady tempo, seamless loop",
    "music_empire": "Upbeat confident funky groove for a mobile tycoon game at its peak, slap bass, punchy drums, "
                    "brass stabs, retro spy movie flavor, fun and catchy, no vocals, steady tempo, seamless loop",
}

SFX = {
    "cash": "cartoon cash register cha-ching with jingling coins, short and bright, mobile game",
    "coin": "single bright cartoon coin pickup, very short",
    "complete": "short cheerful success jingle with sparkle, mobile game reward, 1 second",
    "unlock": "triumphant short brass fanfare with sparkles, mobile game level unlock",
    "upgrade": "quick rising magical power-up whoosh with chime, mobile game",
    "event": "short playful cartoon alert sting, two notes, light tension",
    "dispatch": "small cartoon car engine starts and zooms away quickly",
    "fail": "short sad cartoon trombone wah wah",
    "pop": "soft cartoon bubble pop, very short",
    "accept": "short positive UI confirmation blip, two rising notes",
    "warning": "short cartoon warning beep beep, mobile game",
    "whoosh": "soft quick UI swoosh",
    "click": "tiny soft UI tap click",
    "error": "short soft negative UI buzz",
}


def post(path, key, body):
    req = urllib.request.Request(API + path, data=json.dumps(body).encode(), method="POST")
    req.add_header("xi-api-key", key)
    req.add_header("Content-Type", "application/json")
    req.add_header("Accept", "audio/mpeg")
    with urllib.request.urlopen(req, timeout=120) as r:
        return r.read()


def save(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(data)


def tts(key, voice, text, model, out, style):
    body = {"text": text, "model_id": model, "language_code": None,
            "voice_settings": {"stability": 0.42, "similarity_boost": 0.8, "style": style, "use_speaker_boost": True}}
    body.pop("language_code")
    save(out, post("/text-to-speech/%s?output_format=mp3_44100_128" % voice, key, body))


def make_loop(src, dst, fade=3.0):
    """Seamless OGG loop (gapless, unlike MP3): the last `fade` seconds are cross-faded into the start.
    Pure Python PCM maths (no numpy) via ffmpeg decode/encode."""
    import array
    import subprocess
    rate = 44100
    pcm = subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-i", src, "-f", "s16le", "-ac", "2",
                          "-ar", str(rate), "-"], capture_output=True, check=True).stdout
    a = array.array("h", pcm)
    n = int(fade * rate) * 2
    body = a[:len(a) - n]
    tail = a[len(a) - n:]
    out = array.array("h", body)
    for i in range(n):
        f = (i // 2) / (n // 2)
        v = body[i] * f + tail[i] * (1.0 - f)
        out[i] = int(max(-32768, min(32767, v)))
    subprocess.run(["ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-f", "s16le", "-ac", "2", "-ar", str(rate),
                    "-i", "-", "-af", "volume=-2dB", "-codec:a", "libvorbis", "-q:a", "4", dst],
                   input=out.tobytes(), check=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="eleven_multilingual_v2")
    ap.add_argument("--sfx", action="store_true")
    ap.add_argument("--force", action="store_true")
    ap.add_argument("--lang", default="en,tr")
    ap.add_argument("--music", action="store_true")
    args = ap.parse_args()
    key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not key:
        sys.exit("Set ELEVENLABS_API_KEY first (do not commit it).")
    for lang in args.lang.split(","):
        voices = VOICES[lang]
        jobs = [(k, t, voices["announcer"], 0.6) for k, t in ANNOUNCER[lang].items()]
        jobs += [(k, t, voices["mentor"], 0.35) for k, t in MENTOR[lang].items()]
        for name, text, voice, style in jobs:
            out = os.path.join(VOICE_DIR, lang, name + ".mp3")
            if os.path.exists(out) and not args.force:
                continue
            try:
                tts(key, voice, text, args.model, out, style)
                print("voice", lang, name)
            except urllib.error.HTTPError as e:
                print("voice", lang, name, "failed:", e.code, e.read()[:200])
            time.sleep(0.2)
    if args.music:
        for name, prompt in MUSIC.items():
            raw = os.path.join(SFX_DIR, name + ".raw.mp3")
            if os.path.exists(os.path.join(SFX_DIR, name + ".ogg")) and not args.force:
                continue
            req = urllib.request.Request(API + "/music", data=json.dumps(
                {"prompt": prompt, "music_length_ms": 48000}).encode(), method="POST")
            req.add_header("xi-api-key", key)
            req.add_header("Content-Type", "application/json")
            with urllib.request.urlopen(req, timeout=300) as r:
                save(raw, r.read())
            make_loop(raw, os.path.join(SFX_DIR, name + ".ogg"))
            os.remove(raw)
            print("music", name)
    if args.sfx:
        for name, prompt in SFX.items():
            out = os.path.join(SFX_DIR, name + ".mp3")
            if os.path.exists(out) and not args.force:
                continue
            dur = 0.6 if name in ("click", "pop", "coin", "whoosh", "error") else 1.5
            try:
                save(out, post("/sound-generation", key, {"text": prompt, "duration_seconds": dur, "prompt_influence": 0.65}))
                print("sfx", name)
            except urllib.error.HTTPError as e:
                print("sfx", name, "failed:", e.code, e.read()[:200])
            time.sleep(0.2)


if __name__ == "__main__":
    main()
