"""Charge Nurse — store art from the repo's own models (Blender kit in unity/Assets/Resources/kit + Meshy characters in
unity/Assets/Art/Meshy): the Play feature graphic (1024×500) and three app-icon candidates (512×512, full-bleed, no
text, readable at 48 px). The owner picks the icon later (docs/ART.md); the in-game placeholder icon stays as it is.
No text, no logos, no cross / crescent / service emblem, nothing graphic. EEVEE, headless.
  blender -b --python charge-nurse/tools/art/render_store.py -- <unity dir> <store dir>
Outputs: <store>/feature-graphic.png, <store>/icon-candidates/c1.png … c3.png
"""
import math
import os
import sys

import bpy
import mathutils

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import render_ui as R  # noqa: E402  (helpers: reset, char, kit, camera, render)

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
STORE = argv[1] if len(argv) > 1 else os.path.join(R.UNITY, "../store")
ICONS = os.path.join(STORE, "icon-candidates")


def hexcol(h):
    c = tuple(int(h[i:i + 2], 16) / 255.0 for i in (1, 3, 5))
    return tuple(x ** 2.2 for x in c)   # sRGB -> linear for the BSDF


_mats = {}


def flat(h, rough=0.8, emit=0.0):
    if (h, emit) in _mats:
        return _mats[(h, emit)]
    m = bpy.data.materials.new("c" + h)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*hexcol(h), 1)
    b.inputs["Roughness"].default_value = rough
    if emit:
        b.inputs["Emission Color"].default_value = (*hexcol(h), 1)
        b.inputs["Emission Strength"].default_value = emit
    _mats[(h, emit)] = m
    return m


def block(center, size, h, bevel=0.03):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(center[0], center[1], center[2] + size[2] / 2))
    o = bpy.context.active_object
    o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if bevel:
        mod = o.modifiers.new("b", "BEVEL")
        mod.width = bevel
        mod.segments = 2
    o.data.materials.append(flat(h))
    return o


def disc(center, r, h, depth=0.04):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=depth, vertices=32, location=center)
    o = bpy.context.active_object
    o.data.materials.append(flat(h))
    return o


def plane(center, size, h, z=0.0):
    bpy.ops.mesh.primitive_plane_add(size=1, location=(center[0], center[1], z))
    o = bpy.context.active_object
    o.scale = (size[0], size[1], 1)
    o.data.materials.append(flat(h))
    return o


def reset(bg, sun=2.6, world=0.55):
    """render_ui.reset, but the world light is neutral (the visible backdrop is its own emissive plane) and softer: the
    pastel palette washes out under the UI renders' brighter setup."""
    _mats.clear()   # read_factory_settings drops every material
    R.reset(bg=bg)
    w = bpy.context.scene.world.node_tree.nodes["Background"]
    w.inputs[0].default_value = (0.86, 0.9, 0.92, 1)
    w.inputs[1].default_value = world
    bpy.data.objects["sun"].data.energy = sun
    bpy.data.objects["fill"].data.energy = 0.6


# palette (unity/Tools/make_palette.py)
GRASS, PAVING, FLOOR, CREAM, MINT, BLUEWALL, PANEL = "#8CC970", "#D8D4C8", "#EEF2F0", "#F4EEDF", "#CFE8DF", "#BFD7EA", "#E9EEF1"
BRAND, TEALDARK, CORRIDOR, GLASS = "#22A6A1", "#167C78", "#DCE4E8", "#8ED3F0"
TAGS = ("#4CB860", "#F2C744", "#F08A3C", "#E0533F")


def room(cx, cy, w, d, wall, floor=FLOOR):
    """A cut-away room like the game's: floor, tall back + side walls, knee-high front wall, glass windows on the back."""
    plane((cx, cy), (w, d), floor, 0.02)
    t = 0.22
    block((cx, cy + d / 2, 0), (w, t, 2.6), wall)
    block((cx - w / 2, cy, 0), (t, d, 2.6), wall)
    block((cx + w / 2, cy, 0), (t, d, 2.6), wall)
    block((cx - w * 0.3, cy - d / 2, 0), (w * 0.4, t, 0.5), wall)
    block((cx + w * 0.3, cy - d / 2, 0), (w * 0.4, t, 0.5), wall)
    n = max(1, int(w // 3.5))
    for i in range(n):
        x = cx - w / 2 + w / n * (i + 0.5)
        block((x, cy + d / 2 - t / 2 - 0.02, 1.15), (1.4, 0.02, 1.0), GLASS, bevel=0)


def feature():
    """Feature graphic: a cut-away corner of the hospital at golden hour — ward bays with staff and a patient, the
    corridor heat strip going green → red, the ambulance under its canopy; calm lawn on the left for the deck's wordmark."""
    reset(bg=(0.80, 0.92, 0.95))
    plane((6, 4), (120, 80), "#7DB862", 0)
    plane((8, 2), (34, 20), PAVING, 0.01)
    # rooms: bays (mint), triage (cream), scan (blue)
    room(9.5, 5.5, 12, 8, MINT)
    room(0.5, 5.5, 6, 8, CREAM)
    room(19.5, 5.5, 8, 8, BLUEWALL)
    # corridor heat strip in front of the rooms: calm → busy → jammed
    for i, (x0, x1, col) in enumerate(((-3, 4, CORRIDOR), (4, 9, TAGS[1]), (9, 13, TAGS[2]), (13, 17, TAGS[3]), (17, 24, CORRIDOR))):
        plane(((x0 + x1) / 2, 0.4), (x1 - x0, 1.8), col, 0.03)
    R.kit("bay", (6.0, 7.2, 0))
    R.kit("bay", (9.5, 7.2, 0))
    R.kit("bay", (13.0, 7.2, 0))
    R.kit("desk", (0.5, 7.0, 0))
    R.kit("ct", (19.5, 7.0, 0))
    R.char("patient", (9.4, 4.6, 0), yaw=15)
    R.char("nurse", (7.6, 3.8, 0), yaw=-20)
    R.char("doctor", (12.0, 4.4, 0), yaw=25)
    R.char("paramedic", (1.2, 3.6, 0), yaw=-10)
    # ambulance bay on the right
    plane((30, 3), (11, 9), "#C9CDD2", 0.02)
    R.kit("amb_canopy", (30, 7.0, 0))
    R.kit("ambulance", (29.5, 2.6, 0), yaw=-90)
    R.kit("stretcher", (33.3, 5.0, 0))
    # grounds
    R.kit("sign", (-5.0, -1.6, 0))
    for x, y, t in ((-9, 9, "tree_round"), (-7, 3, "tree_cone"), (-11, -2, "tree_round"), (38, 9, "tree_cone"), (37, -3, "tree_round"),
                    (25, 12, "tree_round"), (-3, 12, "tree_cone")):
        R.kit(t, (x, y, 0), yaw=x * 7, scale=1.3)
    for x in (-2.0, 1.5):
        R.kit("planter", (x, -1.8, 0))
    sun = bpy.data.objects["sun"]
    sun.rotation_euler = mathutils.Euler((math.radians(50), math.radians(10), math.radians(-40)))
    sun.data.color = (1.0, 0.95, 0.86)
    # foreground: visitors' cars, a hedge, patients waiting where the corridor turns red
    for x, name in ((-1.5, "car_a"), (1.4, "car_b"), (7.2, "car_c")):
        R.kit(name, (x, -4.4, 0), yaw=180)
    for x in (10.5, 13.6, 16.7):
        R.kit("hedge", (x, -3.4, 0))
    R.char("patient", (14.4, 0.6, 0), yaw=-30, scale=0.95)
    R.char("patient", (11.0, 0.3, 0), yaw=20, scale=0.9)
    R.camera((16.5, -24, 17.5), (16.0, 2.6, 0.4), lens=29)
    R.render(os.path.join(STORE, "feature-graphic.png"), 1024, 500)


def icon_bg(top, bottom):
    """Full-bleed vertical gradient backdrop (a big plane behind the subject, unlit-ish)."""
    m = bpy.data.materials.new("bg")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    ramp.color_ramp.elements[0].color = (*hexcol(bottom), 1)
    ramp.color_ramp.elements[1].color = (*hexcol(top), 1)
    nt.links.new(tc.outputs["Generated"], sep.inputs[0])
    nt.links.new(sep.outputs["Z"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], em.inputs["Color"])
    nt.links.new(em.outputs["Emission"], out.inputs["Surface"])
    return m


def backdrop(cam_loc, look, top, bottom, dist=30):
    d = (mathutils.Vector(look) - mathutils.Vector(cam_loc)).normalized()
    c = mathutils.Vector(look) + d * dist
    bpy.ops.mesh.primitive_plane_add(size=200, location=c)
    o = bpy.context.active_object
    o.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    bpy.ops.object.transform_apply(rotation=True)
    # generated Z must run bottom->top of the frame: rotate so local Z of the bounding box follows world Z
    o.data.materials.append(icon_bg(top, bottom))
    return o


def icons():
    os.makedirs(ICONS, exist_ok=True)
    # c1 — the charge nurse, head + shoulders, on the brand teal with a soft lighter disc behind her head
    reset(bg=(0.13, 0.65, 0.63), sun=2.4)
    cam, look = (0.15, -2.6, 1.5), (0, 0, 1.40)
    backdrop(cam, look, "#2BB5AF", "#127370", dist=6)
    disc((0.0, 0.8, 1.47), 0.36, "#5CCFC8", 0.03).rotation_euler = (math.radians(90), 0, 0)
    R.char("nurse", (0, 0, 0), yaw=-8)
    R.camera(cam, look, ortho=0.8)
    R.render(os.path.join(ICONS, "c1.png"), 512, 512)
    # c2 — a ward bed seen from above at 3/4 on a teal floor, a green triage tag on the blanket: "the bed is ready"
    reset(bg=(0.81, 0.91, 0.87), sun=2.2, world=0.45)
    cam, look = (2.4, -2.6, 4.2), (0.0, 0.0, 0.35)
    plane((0, 0), (12, 12), "#2BA39A", 0)
    R.kit("bed", (0, 0, 0), yaw=-20)
    disc((0.05, -0.25, 0.62), 0.24, TAGS[0], 0.06)
    R.camera(cam, look, ortho=2.7)
    R.render(os.path.join(ICONS, "c2.png"), 512, 512)
    # c3 — the ambulance at 3/4 on a teal ground (plain stripe, no emblem)
    reset(bg=(0.16, 0.64, 0.62), sun=2.4, world=0.45)
    cam, look = (-4.4, -5.8, 3.0), (0.0, -0.3, 1.0)
    backdrop(cam, look, "#3CC2BB", "#167C78", dist=12)
    plane((0, 0), (30, 30), "#1E8F89", 0)
    R.kit("ambulance", (0, 0, 0), yaw=-15)
    R.camera(cam, look, ortho=5.4)
    R.render(os.path.join(ICONS, "c3.png"), 512, 512)


def main():
    os.makedirs(STORE, exist_ok=True)
    feature()
    icons()
    print("STORE_RENDER_DONE")


main()
