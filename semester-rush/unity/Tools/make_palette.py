#!/usr/bin/env python3
"""Semester Rush — writes Assets/Resources/materials/palette.png (8×8 px). Swatch order = SemesterRush.World.Pal (MeshKit.cs)
and the Blender kit (tools/art/kit.py). Warm collegiate palette (plan section 8): brick, ivy, faculty colours; brand maroon —
no real university colours or crests (originality rule)."""
import os, sys
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "Assets/Resources/materials/palette.png")
SW = [
    "#8CC970", "#6FAF57", "#D9CDB4", "#F2EBDD", "#B9B3A8", "#F1E4CC", "#B5523B", "#8FB7D9",   # 0-7   grass, grass dark, path, path line, stone, cream wall, brick, blue wall
    "#8C3B30", "#5E6670", "#8E2A3A", "#F4EEDF", "#FAFAF7", "#3F7A3A", "#9EC9E2", "#4FA3C7",   # 8-15  roof red, slate, brand maroon, trim, white, ivy, window light, water
    "#EBD9A6", "#4E9F4C", "#7A5434", "#A9ADB2", "#33414E", "#7CCB6B", "#F2C744", "#E0533F",   # 16-23 sand, tree, trunk, locked, window dark, heat ok, heat busy, heat jam
    "#E0533F", "#F2C744", "#9A6B43", "#E7B93B", "#5FA35A", "#2E8C8A", "#D8B27A", "#B87333",   # 24-31 warning, yellow, bench wood, gold, theme ivy, theme modern, theme desert, copper
    "#E8B894", "#3B5BA9", "#7D4FA8", "#B83A4B", "#2F9E96", "#E5813A", "#3B4A5A", "#FFFFFF",   # 32-39 skin, business, literature, law, philosophy, physics, trousers, white
    "#4CAF6A", "#D9A520",                                                                     # 40-41 chemistry, maths
]
while len(SW) < 64: SW.append("#FF00FF")
img = Image.new("RGB", (8, 8))
for i, c in enumerate(SW):
    img.putpixel((i % 8, i // 8), tuple(int(c[k:k + 2], 16) for k in (1, 3, 5)))
os.makedirs(os.path.dirname(OUT), exist_ok=True)
img.save(OUT)
print("palette.png written", len([s for s in SW if s != "#FF00FF"]), "swatches")
