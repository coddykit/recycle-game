"""Volt City world surface texture (deterministic, tileable, Pillow only): unity/Assets/Art/Textures/surface.png 256 px.
  R = fine grain (asphalt / gravel roofs / ground speckle), G = soft blotches (tone patches), B = streaks (water
  ripple / furrows). Sampled by WORLD position with mipmaps + anisotropic filtering in Shaders/VcWorldCore.hlsl
  (no per-cell hash textures: they shimmer — LL Grid Boss). Import: Default, sRGB OFF (data), mipmaps on, Repeat,
  Trilinear, Aniso 4, Max 256 (Editor/VcArtImport.cs sets it).
python3 blender/make_textures.py"""
import math
import random
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
N = 256


def value_noise(seed, cells):
    r = random.Random(seed)
    g = [[r.random() for _ in range(cells)] for _ in range(cells)]

    def f(x, y):
        x *= cells / N
        y *= cells / N
        x0, y0 = int(math.floor(x)) % cells, int(math.floor(y)) % cells
        x1, y1 = (x0 + 1) % cells, (y0 + 1) % cells
        tx, ty = x - math.floor(x), y - math.floor(y)
        tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
        a = g[y0][x0] + (g[y0][x1] - g[y0][x0]) * tx
        b = g[y1][x0] + (g[y1][x1] - g[y1][x0]) * tx
        return a + (b - a) * ty
    return f


def main():
    fine = [value_noise(11 + k, c) for k, c in enumerate((64, 128, 256))]
    blot = [value_noise(31 + k, c) for k, c in enumerate((4, 8, 16))]
    streak = value_noise(51, 32)
    img = Image.new("RGB", (N, N))
    px = img.load()
    for y in range(N):
        for x in range(N):
            r = .5 * fine[0](x, y) + .3 * fine[1](x, y) + .2 * fine[2](x, y)
            g = .55 * blot[0](x, y) + .3 * blot[1](x, y) + .15 * blot[2](x, y)
            s = streak(x * 4 % N, y / 4)
            b = .5 + .5 * math.sin(2 * math.pi * (y / N * 16) + 3.0 * s)
            px[x, y] = tuple(max(0, min(255, int(round(v * 255)))) for v in (r, g, b))
    out = ROOT / "unity/Assets/Art/Textures/surface.png"
    img.save(out, optimize=True)
    print(f"TEXTURE {out.relative_to(ROOT)} {out.stat().st_size} bytes")


main()
