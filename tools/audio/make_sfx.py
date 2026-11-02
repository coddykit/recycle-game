"""Generates the placeholder sound effects + music loop for Borderline Empire.

Pure Python (no dependencies). Writes 16-bit mono WAV files into
res://assets/audio/. Replace any file with a final asset of the same name and
the game picks it up automatically (see scripts/core/audio_manager.gd).
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "..", "assets", "audio"))
random.seed(7)


def write(name, samples, loop=False):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".wav")
    peak = max(1e-6, max(abs(s) for s in samples))
    gain = min(1.0, 0.9 / peak)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        frames = bytearray()
        for s in samples:
            frames += struct.pack("<h", int(max(-1.0, min(1.0, s * gain)) * 32767))
        w.writeframes(bytes(frames))
    print("wrote", name, len(samples) / RATE, "s")


def silence(sec):
    return [0.0] * int(sec * RATE)


def env(i, n, a=0.005, r=0.6):
    t = i / RATE
    dur = n / RATE
    att = min(1.0, t / a) if a > 0 else 1.0
    rel = max(0.0, 1.0 - max(0.0, t - (dur - dur * r)) / (dur * r)) if r > 0 else 1.0
    return att * rel


def tone(freq, sec, vol=0.5, shape="sine", a=0.005, r=0.7, slide=0.0):
    n = int(sec * RATE)
    out = []
    ph = 0.0
    for i in range(n):
        f = freq + slide * (i / n)
        ph += 2 * math.pi * f / RATE
        if shape == "sine":
            v = math.sin(ph)
        elif shape == "square":
            v = 0.6 if math.sin(ph) >= 0 else -0.6
        elif shape == "tri":
            v = 2 / math.pi * math.asin(math.sin(ph))
        elif shape == "saw":
            v = ((ph / (2 * math.pi)) % 1.0) * 2 - 1
        else:
            v = random.uniform(-1, 1)
        out.append(v * vol * env(i, n, a, r))
    return out


def mix(*tracks, offsets=None):
    offsets = offsets or [0.0] * len(tracks)
    length = max(int(o * RATE) + len(t) for t, o in zip(tracks, offsets))
    out = [0.0] * length
    for t, o in zip(tracks, offsets):
        s = int(o * RATE)
        for i, v in enumerate(t):
            out[s + i] += v
    return out


def seq(notes, step, shape="tri", vol=0.4, length=None):
    tracks, offs = [], []
    for i, f in enumerate(notes):
        if f:
            tracks.append(tone(f, length or step * 1.6, vol, shape))
            offs.append(i * step)
    return mix(*tracks, offsets=offs)


def lowpass(samples, k=0.2):
    out, y = [], 0.0
    for s in samples:
        y += k * (s - y)
        out.append(y)
    return out


def note(n):  # midi -> Hz
    return 440.0 * 2 ** ((n - 69) / 12)


def main():
    write("click", mix(tone(1400, 0.035, 0.5, "sine", r=0.9), tone(700, 0.05, 0.3, "tri", r=0.9)))
    write("tap_soft", tone(900, 0.05, 0.35, "sine", r=0.9))
    write("cash", mix(tone(note(88), 0.12, 0.35, "square", r=0.8), tone(note(93), 0.3, 0.35, "square", r=0.9),
                      tone(note(100), 0.35, 0.2, "sine"), offsets=[0, 0.06, 0.06]))
    write("coin", mix(tone(note(91), 0.06, 0.3, "square"), tone(note(96), 0.16, 0.3, "square"), offsets=[0, 0.04]))
    write("accept", mix(tone(note(72), 0.1, 0.4, "tri"), tone(note(79), 0.18, 0.4, "tri"), offsets=[0, 0.07]))
    write("dispatch", mix(lowpass(tone(0, 0.45, 0.6, "noise", a=0.05, r=0.8), 0.08),
                          tone(110, 0.45, 0.4, "saw", a=0.05, r=0.6, slide=90), offsets=[0, 0]))
    write("complete", seq([note(n) for n in (72, 76, 79, 84)], 0.08, "square", 0.25, length=0.25))
    write("upgrade", seq([note(n) for n in (67, 71, 74, 79, 83)], 0.06, "tri", 0.4, length=0.2))
    write("warning", mix(tone(880, 0.12, 0.35, "square"), tone(880, 0.12, 0.35, "square"), offsets=[0, 0.2]))
    write("event", mix(tone(note(64), 0.15, 0.35, "square"), tone(note(60), 0.3, 0.35, "square"), offsets=[0, 0.14]))
    write("error", tone(180, 0.18, 0.4, "square", slide=-60))
    write("unlock", mix(seq([note(n) for n in (60, 64, 67, 72, 76, 79, 84)], 0.07, "square", 0.22, length=0.3),
                        tone(note(48), 1.0, 0.3, "tri", a=0.02, r=0.8)))
    write("whoosh", lowpass(tone(0, 0.3, 0.6, "noise", a=0.08, r=0.7), 0.12))
    write("pop", tone(500, 0.08, 0.5, "sine", slide=500, r=0.9))
    write("fail", seq([note(n) for n in (67, 63, 58)], 0.12, "tri", 0.4, length=0.3))
    # engine loop: low rumble with harmonics, seamless 1 second loop at 50 Hz multiples
    n = RATE
    eng = []
    for i in range(n):
        t = i / RATE
        v = (math.sin(2 * math.pi * 50 * t) * 0.5 + math.sin(2 * math.pi * 100 * t) * 0.25
             + math.sin(2 * math.pi * 150 * t) * 0.12) * (0.8 + 0.2 * math.sin(2 * math.pi * 8 * t))
        eng.append(v * 0.5)
    write("engine_loop", lowpass(eng, 0.3), loop=True)
    # music: chill 8-bar loop, 96 bpm, I-vi-IV-V in C with soft bass + hats
    bpm = 96
    beat = 60.0 / bpm
    bars = 8
    total = int(bars * 4 * beat * RATE)
    music = [0.0] * total
    prog = [(48, [60, 64, 67]), (45, [57, 60, 64]), (41, [57, 60, 65]), (43, [59, 62, 67])] * 2
    melody = [72, 0, 76, 74, 72, 0, 69, 0, 72, 74, 76, 0, 79, 0, 76, 74,
              77, 0, 76, 74, 72, 0, 74, 0, 74, 0, 71, 72, 74, 0, 0, 0] * 2
    for b, (bass, chord) in enumerate(prog):
        start = b * 4 * beat
        for k, nn in enumerate(chord):
            pad = tone(note(nn), 4 * beat, 0.07, "tri", a=0.3, r=0.5)
            s = int(start * RATE)
            for i, v in enumerate(pad):
                if s + i < total:
                    music[s + i] += v
        for q in range(4):
            bl = tone(note(bass - 12 + (12 if q == 2 else 0)), beat * 0.9, 0.22, "sine", a=0.01, r=0.6)
            s = int((start + q * beat) * RATE)
            for i, v in enumerate(bl):
                if s + i < total:
                    music[s + i] += v
        for e in range(8):
            hat = tone(0, 0.04, 0.05 if e % 2 else 0.08, "noise", a=0.001, r=0.9)
            s = int((start + e * beat / 2) * RATE)
            for i, v in enumerate(hat):
                if s + i < total:
                    music[s + i] += v
        for q in (0, 2):
            kick = tone(90, 0.18, 0.35, "sine", a=0.001, r=0.9, slide=-50)
            s = int((start + q * beat) * RATE)
            for i, v in enumerate(kick):
                if s + i < total:
                    music[s + i] += v
    for i, nn in enumerate(melody):
        if nn:
            mel = tone(note(nn), beat * 0.45, 0.09, "square", a=0.01, r=0.7)
            s = int(i * beat / 2 * RATE)
            for j, v in enumerate(mel):
                if s + j < total:
                    music[s + j] += v
    write("music_loop", lowpass(music, 0.35), loop=True)


if __name__ == "__main__":
    main()
