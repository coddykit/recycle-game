#!/usr/bin/env python3
"""Pour & Roast — writes Assets/Resources/materials/palette.png (8×8 px). Swatch order = PourAndRoast.World.Pal (MeshKit.cs)
and the Blender kit (tools/art/kit.py). Warm harbour-cafe palette (design §8): walnut + cream + burnt-orange brand with a
teal accent — no green-and-white chain look, no real brand colours (originality rule)."""
import os
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "Assets/Resources/materials/palette.png")
SW = [
    "#C89B6D", "#9C7350", "#A9A49B", "#8B867E", "#F3E6CF", "#9FB8A0", "#B5654A", "#4A3B32",   # 0-7   floor, quay, walls, trim
    "#EDE7DD", "#6E4B35", "#D9693B", "#C9CED3", "#7C848C", "#2B2B2E", "#C07A45", "#4F8FB0",   # 8-15  counter, brand, steel, copper, water
    "#E8D9B5", "#2C7A7B", "#4E8F4A", "#2F6B3A", "#9FD3E6", "#4B2E22", "#F7F3EA", "#9BA35A",   # 16-23 awning, teal, plants, window, coffee, milk, green beans
    "#E0533F", "#E7B93B", "#B89A6A", "#E0A458", "#2F3A35", "#DDE3E6", "#1F3B4D", "#C9A227",   # 24-31 warning, gold, sack, pastry, chalk, nordic, deco
    "#E8B894", "#4C7FD1", "#E57A3C", "#9B59B6", "#3FB0AC", "#F2C744", "#3B4A5A", "#5B7F4F",   # 32-39 skin, shirts, trousers, apron
    "#B57B55", "#3A2A22", "#B9BEC2", "#3E86A8", "#8E3B46", "#F4F1EA", "#6B8E9E", "#FFFFFF",   # 40-47 skin 2, hair, locked, sea, wine apron, cup, slate
]
while len(SW) < 64:
    SW.append("#FF00FF")
img = Image.new("RGB", (8, 8))
for i, c in enumerate(SW):
    img.putpixel((i % 8, i // 8), tuple(int(c[k:k + 2], 16) for k in (1, 3, 5)))
os.makedirs(os.path.dirname(OUT), exist_ok=True)
img.save(OUT)
print("palette.png written", len([s for s in SW if s != "#FF00FF"]), "swatches")
