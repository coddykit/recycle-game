#!/usr/bin/env python3
"""Grill Convoy — writes Assets/Resources/materials/palette.png (8×8 px). Swatch order = GrillConvoy.World.Pal (MeshKit.cs)
and the Blender kit (tools/art/kit.py). Saturated street-food poster palette (design §8): convoy red-orange brand, cream
and teal trucks, warm harbour-town walls, bun / patty / cheese food colours. No real chain's brand colours or marks."""
import os, sys
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "Assets/Resources/materials/palette.png")
SW = [
    "#9BC46A", "#76A24E", "#4F5562", "#F3E9C8", "#D8CDBB", "#F6E3C0", "#D9774B", "#5FA8A0",   # 0-7   grass … teal wall
    "#C2443A", "#6E7480", "#E8542F", "#D6DADF", "#FFF4DC", "#2D8C8A", "#B7A58C", "#3E93C2",   # 8-15  roofs, brand, steel, trucks, commissary, sea
    "#F0D9A0", "#4F9A4A", "#7A5434", "#B4B6B8", "#2E3A46", "#E3A857", "#6B3A26", "#F6C443",   # 16-23 sand, tree, trunk, locked, glass, bun, patty, cheese
    "#E0533F", "#F5C842", "#7CC24C", "#E7B93B", "#F08A5D", "#E24CC0", "#2FA8C8", "#B8875A",   # 24-31 warning, yellow, lettuce, gold, sunset, neon, retro, wood
    "#E8B894", "#4C7FD1", "#E57A3C", "#9B59B6", "#3FB0AC", "#F2C744", "#3B4A5A", "#FFFFFF",   # 32-39 skin, shirts, trousers, white
    "#F2F2F2", "#FF8A1F", "#FFE08A", "#5A6E8C",                                                 # 40-43 steam, flame, string lights, stadium
]
while len(SW) < 64: SW.append("#FF00FF")
img = Image.new("RGB", (8, 8))
for i, c in enumerate(SW):
    img.putpixel((i % 8, i // 8), tuple(int(c[k:k + 2], 16) for k in (1, 3, 5)))
os.makedirs(os.path.dirname(OUT), exist_ok=True)
img.save(OUT)
print("palette.png written", len([s for s in SW if s != "#FF00FF"]), "swatches")
