"""Semester Rush — renders the UI art from the repo's own models (Meshy characters in unity/Assets/Art/Meshy + the
Blender kit in unity/Assets/Resources/kit): the coach portrait (the dean), the three intro pager images, the boot curtain
and a placeholder app icon set (the owner picks the final icon at P7). EEVEE, headless.
  blender -b --python semester-rush/tools/art/render_ui.py -- <unity dir>
Outputs: Assets/Resources/ui/{coach_dean,intro_1,intro_2,intro_3,curtain}.png, Assets/Art/Icon/icon_*.png
"""
import math
import os
import sys

import bpy
import mathutils

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
UNITY = argv[0]
A = os.path.join(UNITY, "Assets")
UI = os.path.join(A, "Resources/ui")
ICON = os.path.join(A, "Art/Icon")
MINT = (0.78, 0.86, 0.93)   # a soft sky


def reset(bg=MINT, transparent=False):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.film_transparent = transparent
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (*bg, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    sc.world = w
    sc.view_settings.view_transform = "Standard"
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.2
    sun.rotation_euler = mathutils.Euler((math.radians(45), math.radians(8), math.radians(-30)))
    sc.collection.objects.link(sun)
    fill = bpy.data.objects.new("fill", bpy.data.lights.new("fill", "SUN"))
    fill.data.energy = 1.0
    fill.rotation_euler = mathutils.Euler((math.radians(70), 0, math.radians(150)))
    sc.collection.objects.link(fill)


def textured(obj, png, interp="Linear"):
    m = bpy.data.materials.new(os.path.basename(png))
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    tex = m.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(png)
    tex.interpolation = interp
    m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    for o in [obj] + list(obj.children_recursive):
        if o.type == "MESH":
            o.data.materials.clear()
            o.data.materials.append(m)


def load(path, png, loc=(0, 0, 0), yaw=0.0, scale=1.0, interp="Linear"):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    root = new[0]
    for o in new:
        if o.parent is None:
            root = o
    for o in new:
        if o.type == "MESH":
            textured(o, png, interp)
    root.location = loc
    root.rotation_euler = (root.rotation_euler.x, root.rotation_euler.y, math.radians(yaw))
    root.scale = (root.scale.x * scale, root.scale.y * scale, root.scale.z * scale)
    return root


def char(name, loc, yaw=0.0, scale=1.0):
    return load(os.path.join(A, "Art/Meshy/%s_lod0.fbx" % name), os.path.join(A, "Art/Meshy/%s_base.png" % name), loc, yaw, scale)


def kit(name, loc, yaw=0.0, scale=1.0):
    return load(os.path.join(A, "Resources/kit/%s.fbx" % name), os.path.join(A, "Resources/materials/palette.png"), loc, yaw, scale, "Closest")


def ground(size=40, color=(0.55, 0.78, 0.48)):
    bpy.ops.mesh.primitive_plane_add(size=size)
    p = bpy.context.active_object
    m = bpy.data.materials.new("ground")
    m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*color, 1)
    p.data.materials.append(m)


def camera(loc, look, lens=50, ortho=None):
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    bpy.context.scene.collection.objects.link(cam)
    cam.location = loc
    d = mathutils.Vector(look) - mathutils.Vector(loc)
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    if ortho:
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = ortho
    else:
        cam.data.lens = lens
    bpy.context.scene.camera = cam


def render(path, w, h):
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = w, h
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA" if sc.render.film_transparent else "RGB"
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("RENDER", path)


def path(x0, y0, x1, y1, w=0.9):
    import math as m
    bpy.ops.mesh.primitive_plane_add(size=1)
    p = bpy.context.active_object
    p.scale = (w, m.hypot(x1 - x0, y1 - y0), 1)
    p.location = ((x0 + x1) / 2, (y0 + y1) / 2, 0.01)
    p.rotation_euler = (0, 0, -m.atan2(x1 - x0, y1 - y0))
    mt = bpy.data.materials.new("path")
    mt.use_nodes = True
    mt.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.85, 0.80, 0.70, 1)
    p.data.materials.append(mt)


def main():
    os.makedirs(UI, exist_ok=True)
    os.makedirs(ICON, exist_ok=True)
    # coach portrait (head + shoulders, transparent; the bubble crops it into a circle)
    reset(transparent=True)
    char("dean", (0, 0, 0))
    camera((0.15, -1.6, 1.6), (0, 0, 1.52), lens=85)
    render(os.path.join(UI, "coach_dean.png"), 256, 256)
    # intro 1: the dean in front of the main hall
    reset()
    ground()
    path(0, -6, 0, 3)
    kit("hall_m", (0.6, 9.0, 0), scale=0.5)
    char("dean", (-0.9, -0.4, 0), yaw=20)
    camera((0.2, -6.0, 2.2), (0, 0, 1.4), lens=40)
    render(os.path.join(UI, "intro_1.png"), 720, 720)
    # intro 2: the bell — students walk the path to class
    reset()
    ground()
    path(-3, -4, 2, 4)
    kit("hall_s", (2.6, 5.5, 0), yaw=-15, scale=0.5)
    char("student", (-0.6, -1.0, 0), yaw=-30)
    char("student", (0.6, 0.6, 0), yaw=-35, scale=0.95)
    char("student", (-1.8, -2.6, 0), yaw=-25, scale=1.02)
    camera((1.4, -7.5, 2.6), (0.2, 0.4, 1.1), lens=38)
    render(os.path.join(UI, "intro_2.png"), 720, 720)
    # intro 3: grades -> prestige: the professor and a student at the library
    reset()
    ground()
    path(0, -6, 0, 3, w=1.4)
    kit("library", (0.0, 5.0, 0), scale=0.5)
    char("professor", (-0.8, -0.2, 0), yaw=20)
    char("student", (0.8, -0.6, 0), yaw=-25)
    camera((0.2, -6.2, 2.2), (0, 0.4, 1.3), lens=40)
    render(os.path.join(UI, "intro_3.png"), 720, 720)
    # boot curtain (portrait): the whole cast in front of the campus
    reset(bg=(0.36, 0.10, 0.14))
    ground(color=(0.30, 0.52, 0.30))
    path(0, -8, 0, 6, w=1.6)
    kit("admin", (-3.2, 7.0, 0), yaw=12, scale=0.5)
    kit("hall_m", (3.4, 8.0, 0), yaw=-12, scale=0.5)
    char("dean", (0.0, -0.4, 0), yaw=5)
    char("professor", (-1.3, 0.4, 0), yaw=25)
    char("student", (1.3, 0.2, 0), yaw=-25)
    camera((0.2, -8.2, 2.6), (0.0, 0.8, 1.6), lens=36)
    render(os.path.join(UI, "curtain.png"), 720, 1280)
    # placeholder icon set: the dean in front of the clock tower
    reset(bg=(0.56, 0.16, 0.23))
    ground(size=6, color=(0.35, 0.6, 0.32))
    kit("hall_s", (0.0, 4.5, 0), scale=0.3)
    char("dean", (0.0, 0.0, 0))
    camera((0, -6.0, 2.0), (0, 0.5, 1.2), ortho=2.6)
    render(os.path.join(ICON, "icon_full.png"), 1024, 1024)
    reset(transparent=True)
    char("dean", (0.0, 0.0, 0))
    camera((0, -6.0, 2.0), (0, 0, 1.1), ortho=3.0)   # inside the 66 dp adaptive safe zone
    render(os.path.join(ICON, "icon_adaptive_fg.png"), 432, 432)
    print("RENDER_DONE")


main()
