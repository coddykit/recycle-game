#!/usr/bin/env python3
"""Ridgeback Rangers — writes Assets/Resources/materials/palette.png (8×8 px). Swatch order = RidgebackRangers.World.Pal (MeshKit.cs)
and the Blender kit (tools/art/kit.py). Bright, clean civic palette (design §8); the brand is a fresh mint green — no
government green-white scheme colours (impersonation rule)."""
import os, sys
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "Assets/Resources/materials/palette.png")
SW = [
    "#8CC970", "#6FAF57", "#5A6270", "#F2F0E6", "#C9CDD2", "#F4E7C9", "#E3926B", "#8FB7D9",   # 0-7
    "#C9574A", "#7D8792", "#2FB89A", "#F7F9F8", "#FAFAF7", "#3E9C6E", "#9EA7AE", "#4FA3C7",   # 8-15
    "#EBD9A6", "#4E9F4C", "#7A5434", "#B9BEC2", "#33414E", "#8ED3F0", "#C7CDD3", "#5BB178",   # 16-23
    "#E0533F", "#F2C744", "#8A6A4A", "#E7B93B", "#F08A5D", "#2F6E46", "#22A6A1", "#D9C7A0",   # 24-31
    "#E8B894", "#4C7FD1", "#E57A3C", "#9B59B6", "#3FB0AC", "#F2C744", "#3B4A5A", "#FFFFFF",   # 32-39
]
while len(SW) < 64: SW.append("#FF00FF")
img = Image.new("RGB", (8, 8))
for i, c in enumerate(SW):
    img.putpixel((i % 8, i // 8), tuple(int(c[k:k + 2], 16) for k in (1, 3, 5)))
os.makedirs(os.path.dirname(OUT), exist_ok=True)
img.save(OUT)
print("palette.png written", len([s for s in SW if s != "#FF00FF"]), "swatches")
