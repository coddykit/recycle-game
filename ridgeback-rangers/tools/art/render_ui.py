"""Ridgeback Rangers — renders the UI art from the repo's own models (the Meshy Chief Ranger + dinosaurs in
unity/Assets/Art/Meshy, textures in unity/Assets/Resources/kit, the Blender kit): the coach portrait, the three intro
pager images, the boot curtain and a placeholder app icon set (the owner picks the final icon at P7). EEVEE, headless.
  blender -b --python ridgeback-rangers/tools/art/render_ui.py -- <unity dir>
Outputs: Assets/Resources/ui/{coach_warden,intro_1,intro_2,intro_3,curtain}.png, Assets/Art/Icon/icon_*.png
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
MINT = (0.62, 0.86, 0.78)


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


def dino(family, loc, yaw=0.0, scale=1.0):
    return load(os.path.join(A, "Art/Meshy/dino_%s_lod0.fbx" % family), os.path.join(A, "Resources/kit/dino_%s_base.png" % family), loc, yaw, scale)


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


def main():
    os.makedirs(UI, exist_ok=True)
    os.makedirs(ICON, exist_ok=True)
    GREEN = (0.55, 0.78, 0.48)
    # coach portrait (head + shoulders, transparent; the bubble crops it into a circle)
    reset(transparent=True)
    char("warden", (0, 0, 0))
    camera((0.1, -1.6, 1.55), (0, 0, 1.47), lens=85)
    render(os.path.join(UI, "coach_warden.png"), 256, 256)
    # intro 1: the Chief Warden meets a triceratops (dinos are ~1 unit long: scale them up to a friendly size)
    reset(bg=(0.62, 0.84, 0.92))
    ground(color=GREEN)
    char("warden", (-0.9, 0.2, 0), yaw=20)
    dino("horned", (0.9, 0.6, 0), yaw=-120, scale=2.4)
    camera((0.2, -5.4, 1.9), (0.1, 0, 0.9), lens=42)
    render(os.path.join(UI, "intro_1.png"), 720, 720)
    # intro 2: pins = problems; a ranger cabin and a hungry herd at the barn
    reset(bg=(0.62, 0.84, 0.92))
    ground(color=GREEN)
    kit("barn", (-1.6, 2.4, 0), yaw=10, scale=0.45)
    dino("plated", (0.9, 0.4, 0), yaw=-100, scale=2.2)
    dino("duckbill", (-0.4, -0.2, 0), yaw=-60, scale=1.8)
    char("warden", (1.9, -0.9, 0), yaw=-35)
    camera((1.0, -7.0, 3.0), (0.2, 0.4, 0.9), lens=40)
    render(os.path.join(UI, "intro_2.png"), 720, 720)
    # intro 3: visitors ride the safari tram past a long-neck
    reset(bg=(0.62, 0.84, 0.92))
    ground(color=GREEN)
    kit("tram_car", (-1.0, -0.6, 0), yaw=90, scale=0.6)
    dino("longneck", (1.0, 1.0, 0), yaw=-130, scale=3.2)
    camera((0.6, -6.5, 2.6), (0.2, 0.3, 1.3), lens=40)
    render(os.path.join(UI, "intro_3.png"), 720, 720)
    # boot curtain (portrait): the cast in front of the island
    reset(bg=(0.06, 0.23, 0.18))
    ground(color=(0.2, 0.45, 0.35))
    kit("gate", (0.2, 2.6, 0), yaw=0, scale=0.5)
    dino("longneck", (1.5, 1.4, 0), yaw=-140, scale=3.0)
    dino("horned", (-1.4, 0.4, 0), yaw=-50, scale=2.0)
    char("warden", (0.2, -0.4, 0), yaw=10)
    camera((0.3, -8.0, 3.0), (0.2, 0.8, 1.4), lens=38)
    render(os.path.join(UI, "curtain.png"), 720, 1280)
    # placeholder icon set: a triceratops head-on with the warden's hat colours, green field
    reset(bg=(0.55, 0.80, 0.55))
    dino("horned", (0, 0, 0), yaw=-25, scale=2.6)
    camera((0.8, -4.0, 1.4), (0, 0, 0.6), ortho=2.2)
    render(os.path.join(ICON, "icon_full.png"), 1024, 1024)
    reset(transparent=True)
    dino("horned", (0, 0, 0), yaw=-25, scale=2.6)
    camera((0.8, -4.0, 1.4), (0, 0, 0.6), ortho=3.3)   # inside the 66 dp adaptive safe zone
    render(os.path.join(ICON, "icon_adaptive_fg.png"), 432, 432)
    print("RENDER_DONE")


main()
