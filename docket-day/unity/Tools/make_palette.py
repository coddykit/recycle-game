#!/usr/bin/env python3
"""Docket Day — writes Assets/Resources/materials/palette.png (8×8 px). Swatch order = DocketDay.World.Pal (MeshKit.cs)
and the Blender kit (tools/art/kit.py). Warm harbour-town palette: navy firm brand, wood + paper office, pale stone
courthouse (fictional Calderport — no real court or firm colours / crests)."""
import os, sys
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "Assets/Resources/materials/palette.png")
SW = [
    "#7FB069", "#5E9150", "#4F5663", "#F2F0E6", "#C9CDD2", "#F1E4C8", "#B5654A", "#7FA6C9",   # 0-7   grass, road, line, sidewalk, walls
    "#A64B3C", "#6E7782", "#24406B", "#9C6B43", "#FAFAF5", "#2F8F83", "#D8D2C2", "#7EC4E6",   # 8-15  roofs, brand navy, wood, paper, teal, stone, window
    "#8E3B46", "#4E9F4C", "#7A5434", "#B9BEC2", "#2E3440", "#E9C77B", "#A9B1BA", "#3E8E5E",   # 16-23 carpet, tree, trunk, locked, dark, folder, metal, lamp
    "#D9483B", "#F2B33D", "#22262E", "#E2B13C", "#C2703F", "#2F6E46", "#1F1F24", "#D9C7A0",   # 24-31 warning, amber, robe, gold, loft, classic, deco, sand
    "#E8B894", "#3C5A99", "#E57A3C", "#8E5BB6", "#3FB0AC", "#F2C744", "#3B4A5A", "#FFFFFF",   # 32-39 skin, shirts A-E, trousers, white
]
while len(SW) < 64: SW.append("#FF00FF")
img = Image.new("RGB", (8, 8))
for i, c in enumerate(SW):
    img.putpixel((i % 8, i // 8), tuple(int(c[k:k + 2], 16) for k in (1, 3, 5)))
os.makedirs(os.path.dirname(OUT), exist_ok=True)
img.save(OUT)
print("palette.png written", len([s for s in SW if s != "#FF00FF"]), "swatches")
