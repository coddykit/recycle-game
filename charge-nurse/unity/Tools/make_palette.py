#!/usr/bin/env python3
"""Charge Nurse — writes Assets/Resources/materials/palette.png (8×8 px) and the hospital theme variants
(palette_modern / palette_retro / palette_pediatric / palette_gold: a theme = a palette swap, zero extra draws). Swatch
order = ChargeNurse.World.Pal (MeshKit.cs) and the Blender kit (tools/art/kit.py). Clean pastel clinical palette with
warm accents (plan §8); severity tags green / yellow / orange / red (the UI adds shapes: colour-blind safe). No real
hospital or brand colour scheme."""
import os
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__))
DIR = os.path.join(os.path.dirname(HERE), "Assets/Resources/materials")
BASE = [
    "#8CC970", "#6FAF57", "#D8D4C8", "#EEF2F0", "#C9CDD2", "#F4EEDF", "#CFE8DF", "#BFD7EA",   # 0-7  grass, grass dark, paving, floor, concrete, wall cream, wall mint, wall blue
    "#C9574A", "#7D8792", "#22A6A1", "#E9EEF1", "#FBFCFD", "#A9D8C4", "#DCE4E8", "#8ED3F0",   # 8-15 roof, roof grey, brand teal, panel, sheet white, curtain, corridor, glass
    "#C89B6D", "#4E9F4C", "#7A5434", "#B9BEC2", "#33414E", "#4CB860", "#F2C744", "#F08A3C",   # 16-23 wood, tree, trunk, locked, window, tag green, tag yellow, tag orange
    "#E0533F", "#3D7FE0", "#AEB8C2", "#E7B93B", "#C9B6E4", "#2F6E46", "#167C78", "#D9C7A0",   # 24-31 tag red, siren blue, OR floor, gold, lavender, forest, teal dark, retro beige
    "#E8B894", "#B5D3EE", "#2BA39A", "#F4F6F8", "#F08A3C", "#8E6CC4", "#3B4A5A", "#4C7FD1",   # 32-39 skin, gown, nurse, doctor coat, porter, cleaner, trousers, clerk
    "#2E4A7A", "#9CC3E6", "#9AA3AC", "#7FF0E6", "#2A2D31", "#FFFFFF", "#6E757D", "#5E9E58",  # 40-47 tech, mattress, machine, screen, rubber, white, asphalt, hedge
]
THEMES = {
    "modern":    {3: "#F2F4F7", 5: "#E3E8EE", 6: "#D5DEE8", 7: "#AFC3D9", 10: "#3D7FE0", 13: "#C7D8EE", 14: "#E6EAF0", 16: "#9AA3AC"},
    "retro":     {3: "#EFE6D2", 5: "#E9D9B5", 6: "#CFD6A8", 7: "#D9B98C", 10: "#B5733A", 13: "#D8C79A", 14: "#E2D6BC", 16: "#A06A3C"},
    "pediatric": {3: "#FFF6E8", 5: "#FFE3EC", 6: "#D9F2E4", 7: "#D6E6FF", 10: "#F2A541", 13: "#FFD6A5", 14: "#FFF0D9", 16: "#F6C177"},
    "gold":      {10: "#C9A227", 13: "#EAD9A0", 14: "#EFE7D2", 11: "#F3EBD3"},
}


def write(name, sw):
    sw = list(sw) + ["#FF00FF"] * (64 - len(sw))
    img = Image.new("RGB", (8, 8))
    for i, c in enumerate(sw):
        img.putpixel((i % 8, i // 8), tuple(int(c[k:k + 2], 16) for k in (1, 3, 5)))
    img.save(os.path.join(DIR, name + ".png"))


os.makedirs(DIR, exist_ok=True)
write("palette", BASE)
for t, ch in THEMES.items():
    sw = list(BASE)
    for i, c in ch.items():
        sw[i] = c
    write("palette_" + t, sw)
print("palettes written:", 1 + len(THEMES), "swatches", len(BASE))
