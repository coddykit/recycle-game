"""Split a candidate .blend into Android adaptive-icon layers (prep only; nothing is installed).
    blender -b <cN.blend> -P layers.py -- <out_dir> <name>
-> <name>_fg_raw.png (transparent, art only) and <name>_bg_raw.png (backdrop gradient only), 1024 x 1024."""
import sys
from pathlib import Path

import bpy

a = sys.argv[sys.argv.index("--") + 1:]
out, name = Path(a[0]), a[1]
out.mkdir(parents=True, exist_ok=True)
sc = bpy.context.scene
bd = bpy.data.objects["Backdrop"]
sc.render.image_settings.color_mode = "RGBA"
sc.render.film_transparent = True
bd.hide_render = True
sc.render.filepath = str(out / f"{name}_fg_raw.png")
bpy.ops.render.render(write_still=True)
sc.render.film_transparent = False
sc.render.image_settings.color_mode = "RGB"
bd.hide_render = False
for o in sc.objects:
    if o is not bd and o.type not in ("CAMERA",):
        o.hide_render = True
sc.render.filepath = str(out / f"{name}_bg_raw.png")
bpy.ops.render.render(write_still=True)
print("LAYERS_DONE", out, name)
