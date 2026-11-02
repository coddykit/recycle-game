#!/usr/bin/env python3
"""Pre-rendered native names of the CJK languages for Settings -> Language (E49, W61 memory).

The first Japanese / Chinese or Korean text a player sees makes Godot's text server load its CJK line-break data
(~55 MB per script, measured). An English player paid ~110 MB just for opening Settings, whose language buttons spell
日本語 / 한국어 / 简体中文 in their own scripts. Those buttons show these images (white glyphs, tinted like the button
text) unless the UI already runs in that language.

    python3 tools/make_lang_labels.py      -> assets/ui/lang_{ja,ko,zh}.png (bundled Noto Sans CJK SC, 96 px; Pillow+raqm)
"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONT = os.path.join(ROOT, "assets", "fonts", "NotoSansCJKsc-Regular.otf")
NAMES = {"ja": ("日本語", "ja"), "ko": ("한국어", "ko"), "zh": ("简体中文", "zh-Hans")}
SIZE = 96

os.makedirs(os.path.join(ROOT, "assets", "ui"), exist_ok=True)
for code, (text, lang) in NAMES.items():
    f = ImageFont.truetype(FONT, SIZE)
    l, t, r, b = f.getbbox(text, language=lang)
    pad = 4
    img = Image.new("RGBA", (r - l + pad * 2, b - t + pad * 2), (255, 255, 255, 0))
    ImageDraw.Draw(img).text((pad - l, pad - t), text, font=f, fill=(255, 255, 255, 255), language=lang)
    out = os.path.join(ROOT, "assets", "ui", f"lang_{code}.png")
    img.save(out)
    print(code, img.size, out)
