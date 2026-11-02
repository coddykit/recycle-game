"""Charge Nurse — renders the UI art from the repo's own models (Meshy characters in unity/Assets/Art/Meshy — nurse,
doctor, patient, paramedic — and the Blender kit in unity/Assets/Resources/kit): the coach portrait, the three intro
pager images, the boot curtain and a placeholder app icon set (the owner picks the final icon at P7). EEVEE, headless.
No text in any image (10 languages), nothing graphic.
  blender -b --python charge-nurse/tools/art/render_ui.py -- <unity dir>
Outputs: Assets/Resources/ui/{coach_nurse,intro_1,intro_2,intro_3,curtain}.png, Assets/Art/Icon/icon_*.png
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
MINT = (0.70, 0.88, 0.86)


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


def main():
    os.makedirs(UI, exist_ok=True)
    os.makedirs(ICON, exist_ok=True)
    floor = (0.52, 0.66, 0.64)
    # coach portrait: the charge nurse, head + shoulders (transparent; the bubble crops it into a circle)
    reset(transparent=True)
    char("nurse", (0, 0, 0))
    camera((0.12, -1.5, 1.5), (0, 0, 1.44), lens=85)
    render(os.path.join(UI, "coach_nurse.png"), 256, 256)
    # intro 1: you run the floor — the nurse at the desk, a patient arrives
    reset()
    ground(color=floor)
    kit("desk", (0.6, 0.9, 0), yaw=0)
    char("nurse", (0.4, 1.5, 0), yaw=-10)
    char("patient", (-0.9, -0.4, 0), yaw=-35)
    camera((0.2, -4.4, 2.0), (-0.1, 0.5, 1.0), lens=40)
    render(os.path.join(UI, "intro_1.png"), 720, 720)
    # intro 2: colours decide — waiting chairs, the patient and the nurse with the doctor
    reset()
    ground(color=floor)
    kit("chairs", (0.0, 1.4, 0))
    char("patient", (-0.6, 1.25, 0), yaw=0)
    char("nurse", (1.2, -0.2, 0), yaw=-30)
    char("doctor", (-1.5, -0.1, 0), yaw=25)
    camera((0.0, -5.0, 2.2), (0, 0.5, 1.0), lens=38)
    render(os.path.join(UI, "intro_2.png"), 720, 720)
    # intro 3: find the jam — beds, the doctor and the nurse keep them moving
    reset()
    ground(color=floor)
    kit("bay", (-1.3, 1.0, 0))
    kit("bed", (0.6, 1.0, 0))
    char("doctor", (-0.3, -0.5, 0), yaw=15)
    char("nurse", (1.5, -0.2, 0), yaw=-25)
    camera((0.2, -5.0, 2.6), (0, 0.4, 0.9), lens=38)
    render(os.path.join(UI, "intro_3.png"), 720, 720)
    # boot curtain (portrait): the whole cast in front of the ambulance
    reset(bg=(0.05, 0.25, 0.27))
    ground(color=(0.18, 0.42, 0.42))
    kit("ambulance", (0.2, 3.2, 0), yaw=75)
    char("paramedic", (-1.4, 0.8, 0), yaw=20)
    char("doctor", (1.3, 0.6, 0), yaw=-15)
    char("patient", (-0.4, 0.3, 0), yaw=10)
    char("nurse", (0.35, -0.6, 0), yaw=5)
    camera((0.2, -8.0, 2.6), (0.1, 0.6, 1.3), lens=36)
    render(os.path.join(UI, "curtain.png"), 720, 1280)
    # placeholder icon set: the charge nurse on a teal field
    reset(bg=(0.16, 0.64, 0.62))
    char("nurse", (0, 0, 0), yaw=-10)
    camera((0.1, -3.2, 1.35), (0, 0, 1.15), ortho=1.35)
    render(os.path.join(ICON, "icon_full.png"), 1024, 1024)
    reset(transparent=True)
    char("nurse", (0, 0, 0), yaw=-10)
    camera((0.1, -3.2, 1.35), (0, 0, 1.2), ortho=2.0)   # inside the 66 dp adaptive safe zone
    render(os.path.join(ICON, "icon_adaptive_fg.png"), 432, 432)
    print("RENDER_DONE")


if __name__ == "__main__":   # render_store.py imports the helpers
    main()
