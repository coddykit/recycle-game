"""Docket Day — store art from the repo's own models (Meshy cast in unity/Assets/Art/Meshy + the Blender kit in
unity/Assets/Resources/kit): the Play feature graphic (1024×500, no text: the wordmark/tagline overlay comes from
store/screenshots/en.json at capture time) and three app-icon candidates (512×512, full-bleed, no text, built to read at
48 px). The placeholder in unity/Assets/Art/Icon stays until the owner picks one (docs/ART.md). EEVEE, headless.
  blender -b --python docket-day/tools/art/render_store.py -- <unity dir>
Outputs: <game>/store/feature-graphic.png, <game>/store/icon-candidates/c1..c3.png
"""
import math
import os
import sys

import bpy
import mathutils

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import render_ui as R  # noqa: E402  (helpers only; its main() is guarded)

STORE = os.path.join(os.path.dirname(os.path.abspath(R.UNITY)), "store")
NAVY = (0.055, 0.10, 0.20)
DUSK = (0.12, 0.17, 0.32)


def light(name, energy, rot, color=(1, 1, 1), kind="SUN", loc=(0, 0, 0), size=1.0):
    ob = bpy.data.objects.new(name, bpy.data.lights.new(name, kind))
    ob.data.energy = energy
    ob.data.color = color
    ob.rotation_euler = mathutils.Euler(tuple(math.radians(a) for a in rot))
    ob.location = loc
    if kind == "AREA":
        ob.data.size = size
    bpy.context.scene.collection.objects.link(ob)
    return ob


def backdrop(color, loc=(0, 12, 0), size=80, emit=1.0):
    """A flat emissive card behind the subject so the icon is full-bleed with a controlled colour."""
    bpy.ops.mesh.primitive_plane_add(size=size, location=loc, rotation=(math.radians(90), 0, 0))
    p = bpy.context.active_object
    m = bpy.data.materials.new("backdrop")
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (*color, 1)
    em.inputs["Strength"].default_value = emit
    nt.links.new(em.outputs[0], out.inputs[0])
    p.data.materials.append(m)
    return p


def quality(world=0.35, sun=1.4, fill=0.35):
    """Dimmer base rig than the UI renders + AgX tone mapping so the pale stone and the window swatch don't clip."""
    sc = bpy.context.scene
    sc.world.node_tree.nodes["Background"].inputs[1].default_value = world
    bpy.data.objects["sun"].data.energy = sun
    bpy.data.objects["fill"].data.energy = fill
    try:
        sc.view_settings.view_transform = "AgX"
        sc.view_settings.look = "AgX - Medium High Contrast"
    except TypeError:
        sc.view_settings.view_transform = "Filmic"
    sc.eevee.taa_render_samples = 64
    sc.eevee.use_soft_shadows = True
    sc.eevee.use_gtao = True
    sc.eevee.use_bloom = True
    sc.eevee.bloom_intensity = 0.03


def feature():
    R.reset(bg=DUSK)
    quality(world=0.5, sun=1.0, fill=0.3)
    backdrop((0.16, 0.22, 0.42), loc=(0, 40, 0), size=300, emit=1.0)
    R.ground(size=120, color=(0.24, 0.26, 0.31))
    R.kit("courthouse", (3.5, 9.0, 0), scale=0.55)
    R.kit("lamp", (-3.6, 1.6, 0), scale=0.75)
    R.kit("lamp", (8.6, 2.2, 0), scale=0.75)
    R.kit("bench", (-2.4, 2.0, 0), yaw=8, scale=0.9)
    R.kit("boxes", (5.9, 0.6, 0), yaw=-20, scale=0.6)
    R.char("judge", (2.2, 1.0, 0), yaw=-6)
    R.char("counsel", (0.6, 0.0, 0), yaw=14)
    R.char("associate", (3.9, 0.1, 0), yaw=-18)
    R.char("client", (5.2, -0.5, 0), yaw=-30)
    R.char("office_cat", (-0.6, -0.9, 0), yaw=28, scale=1.6)
    light("warm", 2.6, (62, 0, -55), color=(1.0, 0.78, 0.52))
    light("rim", 3.5, (65, 0, 150), color=(0.55, 0.68, 1.0))
    R.camera((1.6, -8.0, 1.1), (2.2, 1.0, 2.0), lens=30)
    R.render(os.path.join(STORE, "feature-graphic.png"), 1024, 500)


def icon_cat(path):
    R.reset(bg=NAVY)
    quality()
    backdrop((0.10, 0.19, 0.40), loc=(0, 3, 0), emit=1.0)
    R.char("office_cat", (0, 0, 0), yaw=6, scale=2.2)
    light("warm", 3.2, (48, 0, -38), color=(1.0, 0.88, 0.70))
    light("rim", 5.0, (78, 0, 160), color=(0.70, 0.82, 1.0))
    R.camera((0.0, -2.7, 0.92), (0.0, 0, 0.80), lens=65)
    R.render(path, 512, 512)


def icon_counsel(path):
    R.reset(bg=NAVY)
    quality()
    backdrop((0.85, 0.55, 0.16), loc=(0, 3, 0), emit=1.0)
    R.char("counsel", (0, 0, 0), yaw=-8)
    light("warm", 3.0, (45, 0, -35), color=(1.0, 0.92, 0.80))
    light("rim", 4.0, (70, 0, 160), color=(1.0, 0.90, 0.70))
    R.camera((0.05, -1.05, 1.50), (0.0, 0, 1.50), lens=55)
    R.render(path, 512, 512)


def icon_steps(path):
    R.reset(bg=DUSK)
    quality()
    backdrop((0.14, 0.20, 0.42), loc=(0, 30, 0), size=200)
    R.ground(size=80, color=(0.22, 0.24, 0.30))
    R.kit("courthouse", (0, 5.0, 0), scale=0.42)
    R.char("office_cat", (0.0, -1.0, 0), yaw=10, scale=4.2)
    light("warm", 2.6, (55, 0, -60), color=(1.0, 0.80, 0.55))
    light("rim", 3.5, (70, 0, 165), color=(0.60, 0.72, 1.0))
    R.camera((0.0, -4.6, 0.7), (0.0, 2.0, 2.3), lens=28)
    R.render(path, 512, 512)


def main():
    os.makedirs(os.path.join(STORE, "icon-candidates"), exist_ok=True)
    feature()
    icon_cat(os.path.join(STORE, "icon-candidates/c1.png"))
    icon_counsel(os.path.join(STORE, "icon-candidates/c2.png"))
    icon_steps(os.path.join(STORE, "icon-candidates/c3.png"))
    print("STORE_RENDER_DONE")


main()
