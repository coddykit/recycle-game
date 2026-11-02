"""Semester Rush — store art from the repo's own models only (Meshy cast in unity/Assets/Art/Meshy, Blender kit in
unity/Assets/Resources/kit incl. the kit/merge campus dressing). EEVEE, headless, no text in any image.
  blender -b --python semester-rush/tools/art/render_store.py -- <game dir>
Outputs: store/feature-graphic.png (1024 x 500) and store/icon-candidates/c1..c3.png (512 x 512, full-bleed, readable at
48 px). The current placeholder icon in unity/Assets/Art/Icon stays until the owner picks a candidate (docs/ART.md).
"""
import math
import os
import sys

import bpy
import mathutils

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
GAME = argv[0] if argv else os.path.join(os.path.dirname(__file__), "../..")
A = os.path.join(GAME, "unity/Assets")
STORE = os.path.join(GAME, "store")
PALETTE = os.path.join(A, "Resources/materials/palette.png")
_mats = {}
MAROON = (0.36, 0.08, 0.12)     # brand maroon, darkened for the sky
GRASS = (0.30, 0.55, 0.26)
PATHC = (0.80, 0.74, 0.62)


def reset(bg, strength=1.0):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mats.clear()
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.view_settings.view_transform = "Standard"
    sc.eevee.use_gtao = True
    sc.eevee.use_soft_shadows = True
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (*bg, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = strength
    sc.world = w
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.4
    sun.data.color = (1.0, 0.95, 0.86)   # warm late-afternoon light
    sun.rotation_euler = mathutils.Euler((math.radians(52), math.radians(10), math.radians(-38)))
    sc.collection.objects.link(sun)
    fill = bpy.data.objects.new("fill", bpy.data.lights.new("fill", "SUN"))
    fill.data.energy = 0.9
    fill.data.color = (0.8, 0.86, 1.0)
    fill.rotation_euler = mathutils.Euler((math.radians(70), 0, math.radians(150)))
    sc.collection.objects.link(fill)


def mat_tex(png, interp):
    key = (png, interp)
    if key in _mats:
        return _mats[key]
    m = bpy.data.materials.new(os.path.basename(png))
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    tex = m.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(png, check_existing=True)
    tex.interpolation = interp
    m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    _mats[key] = m
    return m


def mat_flat(name, rgb):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*rgb, 1)
    m.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.95
    return m


def load(path, png, loc, yaw=0.0, scale=1.0, interp="Linear"):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    m = mat_tex(png, interp)
    root = new[0]
    for o in new:
        if o.parent is None:
            root = o
        if o.type == "MESH":
            o.data.materials.clear()
            o.data.materials.append(m)
    root.location = loc
    root.rotation_euler = (root.rotation_euler.x, root.rotation_euler.y, math.radians(yaw))
    root.scale = (root.scale.x * scale, root.scale.y * scale, root.scale.z * scale)
    return root


# shoulder pivots (x, z in metres) of the Meshy cast, measured from the LOD0 meshes (arms are modelled in a T / A pose)
SHOULDER = {"student": (0.20, 1.40), "professor": (0.19, 1.30), "dean": (0.21, 1.12)}


def lower_arms(root, name, deg):
    """Swings the modelled arms down by deg around the shoulders (render-only edit of the imported copy, no rig)."""
    xs, zs = SHOULDER[name]
    for o in [root] + list(root.children_recursive):
        if o.type != "MESH":
            continue
        for v in o.data.vertices:
            x, z = v.co.x, v.co.z
            ax = abs(x)
            if ax <= xs or z < zs - 0.25:
                continue
            w = min(1.0, (ax - xs) / 0.09)
            a = math.radians(deg) * w
            rx, rz = ax - xs, z - zs
            nx = rx * math.cos(a) + rz * math.sin(a)
            nz = -rx * math.sin(a) + rz * math.cos(a)
            v.co.x = math.copysign(xs + nx, x)
            v.co.z = zs + nz


def char(name, loc, yaw=0.0, scale=1.0, arms=0.0):
    root = load(os.path.join(A, "Art/Meshy/%s_lod0.fbx" % name), os.path.join(A, "Art/Meshy/%s_base.png" % name), loc, yaw, scale)
    if arms:
        lower_arms(root, name, arms)
    return root


def kit(name, loc, yaw=0.0, scale=1.0):
    return load(os.path.join(A, "Resources/kit/%s.fbx" % name), PALETTE, loc, yaw, scale, "Closest")


def oak(loc, yaw=0.0, scale=1.0):
    return load(os.path.join(A, "Resources/kit/oak.fbx"), os.path.join(A, "Resources/kit/oak_base.png"), loc, yaw, scale)


def plane(size, loc, rgb, name="ground", rot=0.0, sx=1.0, sy=1.0):
    bpy.ops.mesh.primitive_plane_add(size=size, location=loc)
    p = bpy.context.active_object
    p.scale = (sx, sy, 1)
    p.rotation_euler = (0, 0, rot)
    p.data.materials.append(mat_flat(name, rgb))
    return p


def path(x0, y0, x1, y1, w=1.2):
    plane(1, ((x0 + x1) / 2, (y0 + y1) / 2, 0.01), PATHC, "path", -math.atan2(x1 - x0, y1 - y0), w, math.hypot(x1 - x0, y1 - y0))


def camera(loc, look, lens=50, ortho=None):
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    bpy.context.scene.collection.objects.link(cam)
    cam.location = loc
    cam.rotation_euler = (mathutils.Vector(look) - mathutils.Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    if ortho:
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = ortho
    else:
        cam.data.lens = lens
    cam.data.clip_end = 500
    bpy.context.scene.camera = cam


def render(path_, w, h):
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = w, h
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGB"
    sc.render.filepath = path_
    bpy.ops.render.render(write_still=True)
    print("RENDER", path_)


def feature():
    """A late-afternoon bell change: the cast on the main path, halls, the clock tower and autumn trees behind."""
    reset((0.62, 0.74, 0.86), 1.0)
    plane(400, (0, 0, 0), GRASS)
    path(0, -12, 0, 30, w=2.6)
    path(-30, 9, 30, 9, w=2.0)
    kit("admin", (0.0, 26.0, 0), scale=0.85)
    kit("hall_m", (-17.0, 21.0, 0), yaw=18, scale=0.75)
    kit("library", (17.5, 21.0, 0), yaw=-18, scale=0.75)
    kit("hall_s", (-31.0, 14.0, 0), yaw=30, scale=0.7)
    kit("dorm", (31.0, 16.0, 0), yaw=-28, scale=0.65)
    kit("merge/tree_maple", (-8.5, 13.0, 0), scale=1.0)
    kit("merge/tree_maple", (9.0, 14.5, 0), yaw=40, scale=0.9)
    kit("merge/tree_oak", (-24.0, 6.0, 0), yaw=20, scale=1.15)
    kit("merge/tree_oak", (24.0, 5.0, 0), yaw=70, scale=1.2)
    kit("merge/tree_maple", (-38.0, 4.0, 0), yaw=10, scale=1.1)
    kit("merge/tree_oak", (38.0, 7.0, 0), scale=1.1)
    for x in (-3.4, 3.4):
        kit("merge/lamp", (x, 12.5, 0), scale=0.85)
    kit("merge/lamp", (-6.5, 6.6, 0), scale=0.85)
    kit("merge/bench", (-7.5, 11.0, 0), yaw=180, scale=0.9)
    kit("merge/bike_rack", (5.5, 15.5, 0), scale=0.9)
    # the cast: the dean and a professor in front, the bell-change crowd streaming to class behind them
    char("dean", (-1.0, -3.6, 0), yaw=12, scale=1.0)
    char("professor", (1.3, -2.6, 0), yaw=-20, scale=0.98, arms=72)
    crowd = ((-9.0, 9.4, 45), (-7.2, 8.6, 40), (-5.0, 9.6, 50), (-2.6, 8.8, 35), (2.4, 9.2, -40), (4.8, 8.7, -45), (7.4, 9.5, -35),
             (-0.6, 4.0, 10), (0.9, 6.2, -15), (-0.3, 11.6, 5), (9.6, 8.9, -50), (-11.4, 9.1, 40))   # yaw 0 = towards the camera
    for i, (x, y, yaw) in enumerate(crowd):
        char("student", (x, y, 0), yaw=yaw, scale=0.86 + (i % 3) * 0.04, arms=75)
    camera((0.4, -10.5, 2.6), (0.0, 6.0, 2.4), lens=30)
    render(os.path.join(STORE, "feature-graphic.png"), 1024, 500)


def icon_dean():
    """c1 — the dean, head and shoulders, the clock tower behind on brand maroon."""
    reset(MAROON, 1.0)
    plane(60, (0, 0, 0), GRASS)
    kit("admin", (0.4, 9.0, 0), scale=0.55)
    char("dean", (0.0, 0.0, 0), yaw=6, scale=1.0)
    camera((0.1, -3.4, 1.75), (0.0, 0.0, 1.55), lens=60)
    render(os.path.join(STORE, "icon-candidates/c1.png"), 512, 512)


def icon_tower():
    """c2 — the bell: the clock tower from below with an autumn maple and an oak, the gold gate open in front."""
    reset((0.95, 0.66, 0.36), 0.9)
    plane(200, (0, 0, 0), GRASS)
    path(0, -10, 0, 6, w=2.2)
    kit("admin", (0.0, 7.0, 0), scale=1.0)
    kit("merge/gate_open", (0.0, -1.5, 0), scale=1.0)
    kit("merge/tree_maple", (-6.2, 2.5, 0), yaw=25, scale=1.25)
    kit("merge/tree_oak", (6.4, 3.0, 0), yaw=80, scale=1.2)
    camera((0.0, -14.0, 1.4), (0.0, 4.0, 6.0), lens=26)
    render(os.path.join(STORE, "icon-candidates/c2.png"), 512, 512)


def icon_rush():
    """c3 — the bell change from above: a brick hall, paths crossing, students in faculty colours streaming in."""
    reset((0.36, 0.08, 0.12), 0.8)
    plane(80, (0, 0, 0), GRASS)
    path(-12, -12, 12, 12, w=2.0)
    path(-12, 12, 12, -12, w=2.0)
    kit("hall_m", (0.0, 7.5, 0), scale=0.55)
    kit("merge/tree_maple", (-6.5, 1.0, 0), scale=0.75)
    kit("merge/tree_oak", (6.5, 0.0, 0), scale=0.75)
    kit("merge/lamp", (1.6, -1.8, 0), scale=0.7)
    for i, (x, y, yaw) in enumerate(((-3.6, -3.4, 135), (-1.9, -1.6, 135), (3.4, -3.6, -135), (1.6, -1.9, -135), (-0.2, 0.4, 180), (0.4, 2.4, 180))):
        char("student", (x, y, 0), yaw=yaw, scale=0.85, arms=75)
    camera((0.0, -11.0, 10.5), (0.0, 1.2, 0.8), lens=40)
    render(os.path.join(STORE, "icon-candidates/c3.png"), 512, 512)


def main():
    os.makedirs(os.path.join(STORE, "icon-candidates"), exist_ok=True)
    feature()
    icon_dean()
    icon_tower()
    icon_rush()
    print("RENDER_STORE_DONE")


main()
