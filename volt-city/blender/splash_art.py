"""Volt City own splash frame + loading-curtain art (no engine logo, no baked text: titles / progress are UI and get
localised). From the P2 Medium frames (blender/frames.sh + the 'city' overview):
    python3 blender/splash_art.py [frames dir]  -> unity/Assets/Art/Splash/{splash,curtain}.png (720 x 1560)
splash  = dusk city (the first impression; a soft dark band top + bottom for the title and the progress bar)
curtain = night overview of the whole city under a navy veil (behind the loading indicator); background colour for
          Player Settings > Splash / the curtain panel = SPLASH_BG."""
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SRC = Path(sys.argv[1] if len(sys.argv) > 1 else "/Volumes/BuildSSD/game-builds/volt-city/p2")
OUT = ROOT / "unity/Assets/Art/Splash"
SIZE = (720, 1560)
SPLASH_BG = (10, 23, 33)     # #0A1721, the navy of the night sky


def bands(im, top=.22, bottom=.26, strength=200):
    w, h = im.size
    over = Image.new("RGBA", im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(over)
    for y in range(h):
        a = 0
        if y < h * top:
            a = strength * (1 - y / (h * top)) ** 1.6
        elif y > h * (1 - bottom):
            a = strength * ((y - h * (1 - bottom)) / (h * bottom)) ** 1.6
        if a:
            d.line([(0, y), (w, y)], fill=SPLASH_BG + (int(a),))
    return Image.alpha_composite(im.convert("RGBA"), over).convert("RGB")


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    splash = Image.open(SRC / "medium/dusk.png").convert("RGB").resize(SIZE, Image.LANCZOS)
    bands(splash).save(OUT / "splash.png", optimize=True)
    city = Image.open(SRC / "medium/city.png").convert("RGB").resize(SIZE, Image.LANCZOS)
    veil = Image.new("RGB", SIZE, SPLASH_BG)
    bands(Image.blend(city, veil, .35), .3, .3, 230).save(OUT / "curtain.png", optimize=True)
    for n in ("splash", "curtain"):
        p = OUT / f"{n}.png"
        print(f"SPLASH_ART {p.relative_to(ROOT)} {p.stat().st_size} bytes")


main()
