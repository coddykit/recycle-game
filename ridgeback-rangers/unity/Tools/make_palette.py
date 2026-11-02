#!/usr/bin/env python3
"""Ridgeback Rangers — writes Assets/Resources/materials/palette.png (8×8 px). Swatch order = RidgebackRangers.World.Pal (MeshKit.cs)
and the Blender kit (tools/art/kit.py). Warm toy-diorama island (plan §8): saturated greens, terracotta, sand; the brand is a
ranger green. No film-franchise colour schemes (no black-and-yellow gate livery)."""
import os, sys
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "Assets/Resources/materials/palette.png")
SW = [
    "#8CC970", "#5E9E4A", "#C9A878", "#EFE3C2", "#B9B4AA", "#F4E7C9", "#D9875B", "#8FB7D9",   # 0-7   grass, grass dark, dirt path, path line, stone, cream, terracotta, blue
    "#C9574A", "#6F7A73", "#2F8F5B", "#F2EEDF", "#FAFAF7", "#3E7D4F", "#9A6B43", "#4FA3C7",   # 8-15  roof red, roof slate, ranger green, panel, white, jungle, wood, water
    "#E8D3A0", "#4E9F4C", "#7A5434", "#B9BEC2", "#33414E", "#9AA6AE", "#8C7F72", "#6DBF5A",   # 16-23 sand, tree, trunk, locked, dark, steel, rock, fern
    "#E0533F", "#F2C744", "#7E5F45", "#E7B93B", "#F08A5D", "#2F6E46", "#3FB6C9", "#C8B27E",   # 24-31 warning, yellow, mud, gold, sunset, forest, glacier, khaki
    "#E8B894", "#4C7FD1", "#E57A3C", "#9B59B6", "#3FB0AC", "#F2C744", "#3B4A5A", "#FFFFFF",   # 32-39 skin, shirts A-E, trousers, white
    "#5A4A42", "#FF7A3D", "#D8C8A8", "#A7C77A", "#86C95A", "#F1E6CC", "#EDE3CC", "#6F9446",   # 40-47 volcano rock, lava glow, egg shell, dino olive, leaf light, canvas, bone, moss
    "#FFE19A", "#5E4029", "#3B9C8A",                                                          # 48-50 lamp, bark, sail teal (P2 polish kit)
]
while len(SW) < 64: SW.append("#FF00FF")
img = Image.new("RGB", (8, 8))
for i, c in enumerate(SW):
    img.putpixel((i % 8, i // 8), tuple(int(c[k:k + 2], 16) for k in (1, 3, 5)))
os.makedirs(os.path.dirname(OUT), exist_ok=True)
img.save(OUT)
print("palette.png written", len([s for s in SW if s != "#FF00FF"]), "swatches")
