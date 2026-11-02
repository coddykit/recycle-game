"""Pour & Roast — renders the UI art from the repo's own models (Meshy hero characters / props in unity/Assets/Art/Meshy
and the Blender cafe kit in unity/Assets/Resources/kit): the coach portrait (Mara, the head barista), the three intro pager
images, the boot curtain and a placeholder app icon set (the owner picks the final icon at P7). EEVEE, headless.
No text, no logos, no real brands anywhere in the art.
  blender -b --python pour-and-roast/tools/art/render_ui.py -- <unity dir>
Outputs: Assets/Resources/ui/{coach_barista,intro_1,intro_2,intro_3,curtain}.png, Assets/Art/Icon/icon_*.png
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
CREAM = (0.95, 0.88, 0.76)


def reset(bg=CREAM, transparent=False):
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


def cup(loc, r=0.35, h=0.6):
    """A big takeaway cup for the icon: cream cup, burnt-orange sleeve, coffee on top (no print)."""
    def mat(name, rgb):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*rgb, 1)
        m.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.6
        return m
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=r, depth=h, location=(loc[0], loc[1], loc[2] + h / 2))
    c = bpy.context.active_object
    c.data.materials.append(mat("cup", (0.96, 0.94, 0.9)))
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=r * 1.03, depth=h * 0.32, location=(loc[0], loc[1], loc[2] + h * 0.45))
    bpy.context.active_object.data.materials.append(mat("sleeve", (0.85, 0.41, 0.23)))
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=r * 0.92, depth=0.02, location=(loc[0], loc[1], loc[2] + h - 0.03))
    bpy.context.active_object.data.materials.append(mat("coffee", (0.36, 0.2, 0.12)))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r * 0.28, location=(loc[0], loc[1], loc[2] + h - 0.03))
    s = bpy.context.active_object
    s.scale = (1, 1, 0.12)
    s.data.materials.append(mat("foam", (0.93, 0.85, 0.7)))


def ground(size=40, color=(0.62, 0.45, 0.31)):
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
    # coach portrait (head + shoulders, transparent; the bubble crops it into a circle)
    reset(transparent=True)
    char("barista", (0, 0, 0))
    camera((0.1, -1.6, 1.55), (0, 0, 1.47), lens=85)
    render(os.path.join(UI, "coach_barista.png"), 256, 256)
    # intro 1: the cart on the quay, Mara behind it, a commuter waiting
    reset(bg=(0.66, 0.78, 0.84))
    ground(color=(0.5, 0.48, 0.45))
    kit("cart", (0, 0.6, 0))
    kit("espresso_1", (0.05, 0.75, 1.1))
    char("barista", (1.75, 0.5, 0), yaw=-25)
    char("customer", (-1.7, -0.7, 0), yaw=-35)
    camera((0.3, -6.8, 2.4), (0, 0, 1.0), lens=38)
    render(os.path.join(UI, "intro_1.png"), 720, 720)
    # intro 2: the bar — espresso machine, pickup shelf, a croissant, a happy customer
    reset()
    ground()
    kit("counter", (0, 0.4, 0), scale=0.6)
    kit("espresso_2", (-0.5, 0.45, 1.0))
    kit("grinder", (-1.2, 0.45, 1.0))
    kit("pickup_shelf", (1.0, 0.4, 1.0))
    char("croissant", (0.35, 0.2, 1.0), yaw=20)
    char("barista", (-0.4, 1.3, 0), yaw=0)
    char("customer", (1.3, -0.9, 0), yaw=-20)
    camera((0.4, -6.0, 2.8), (0, 0.2, 1.1), lens=40)
    render(os.path.join(UI, "intro_2.png"), 720, 720)
    # intro 3: the roastery — drum roaster, sacks, the cafe cat
    reset()
    ground()
    kit("roaster", (0.2, 0.6, 0))
    char("sack", (-1.2, 0.4, 0), yaw=15)
    char("sack", (-1.8, 1.0, 0), yaw=-25)
    char("cat", (1.4, -0.4, 0), yaw=-30, scale=1.3)
    char("plant", (2.1, 1.0, 0), yaw=10)
    camera((0.3, -6.5, 2.6), (0.1, 0.2, 0.9), lens=40)
    render(os.path.join(UI, "intro_3.png"), 720, 720)
    # boot curtain (portrait): the whole cast on walnut
    reset(bg=(0.29, 0.23, 0.2))
    ground(color=(0.45, 0.32, 0.22))
    kit("cart", (0.2, 1.8, 0), yaw=-10)
    char("barista", (-0.4, 0.0, 0), yaw=10)
    char("customer", (0.9, -0.2, 0), yaw=-20)
    char("cat", (-1.3, -0.6, 0), yaw=30, scale=1.3)
    char("sack", (1.6, 0.9, 0), yaw=-15)
    camera((0.3, -8.0, 3.0), (0.2, 0.5, 1.3), lens=38)
    render(os.path.join(UI, "curtain.png"), 720, 1280)
    # placeholder icon set: a takeaway cup with a croissant, warm cream field
    reset()
    cup((0.15, 0.1, 0))
    char("croissant", (-0.45, -0.25, 0), yaw=25, scale=1.6)
    camera((0, -5.0, 3.4), (-0.05, 0, 0.36), ortho=1.6)
    render(os.path.join(ICON, "icon_full.png"), 1024, 1024)
    reset(transparent=True)
    cup((0.15, 0.1, 0))
    char("croissant", (-0.45, -0.25, 0), yaw=25, scale=1.6)
    camera((0, -5.0, 3.4), (-0.05, 0, 0.36), ortho=2.4)   # inside the 66 dp adaptive safe zone
    render(os.path.join(ICON, "icon_adaptive_fg.png"), 432, 432)
    print("RENDER_DONE")


main()
