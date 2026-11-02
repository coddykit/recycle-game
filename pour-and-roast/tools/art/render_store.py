"""Pour & Roast — store art from the repo's own models (Blender kit incl. the venue shells + the Meshy characters/props):
  * store/feature-graphic.png (1024×500): the cart on the quay at the harbour, Mara at the machine, customers waiting;
    the left third stays calm for the tagline the store renderer lays over it later (no text here);
  * store/icon-candidates/c1..c3.png (512×512, full bleed, no text, big shapes that read at 48 px):
      c1 takeaway cup + croissant on teal (the current placeholder's idea, closer and bolder),
      c2 the drum roaster with its orange drum on deep navy,
      c3 a latte with heart art seen from above on walnut.
The owner picks the icon later (docs/ART.md); unity/Assets/Art/Icon keeps the placeholder until then.
EEVEE, headless. No text, no logos, no real brands.
  blender -b --python pour-and-roast/tools/art/render_store.py -- <game dir (pour-and-roast)>
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
PAL = os.path.join(A, "Resources/materials/palette.png")


def srgb(h):
    """#RRGGBB -> linear RGB (Blender colour inputs are linear)."""
    def lin(c):
        c = c / 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return tuple(lin(int(h[i:i + 2], 16)) for i in (1, 3, 5))


BRAND, TEAL, WALNUT, CREAM, COFFEE, CUP, FOAM, NAVY = "#D9693B", "#2C7A7B", "#6E4B35", "#F3E6CF", "#4B2E22", "#F4F1EA", "#F7EEDD", "#1F3B4D"


def reset(bg="#F3E6CF", strength=1.0):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.film_transparent = False
    sc.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (*srgb(bg), 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = strength
    sc.world = w
    for name, energy, rot in (("sun", 3.2, (45, 8, -30)), ("fill", 1.0, (70, 0, 150))):
        o = bpy.data.objects.new(name, bpy.data.lights.new(name, "SUN"))
        o.data.energy = energy
        o.rotation_euler = mathutils.Euler(tuple(math.radians(a) for a in rot))
        sc.collection.objects.link(o)


def flat(name, hexcol, rough=0.6, emit=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*srgb(hexcol), 1)
    b.inputs["Roughness"].default_value = rough
    if emit > 0:
        b.inputs["Emission Color"].default_value = (*srgb(hexcol), 1)
        b.inputs["Emission Strength"].default_value = emit
    return m


def textured(objs, png, interp):
    m = bpy.data.materials.new(os.path.basename(png))
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    t = m.node_tree.nodes.new("ShaderNodeTexImage")
    t.image = bpy.data.images.load(png)
    t.interpolation = interp
    m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.85
    for o in objs:
        if o.type == "MESH":
            o.data.materials.clear()
            o.data.materials.append(m)


def load(path, png, loc, yaw=0.0, scale=1.0, interp="Linear"):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    textured(new, png, interp)
    root = next((o for o in new if o.parent is None), new[0])
    root.location = loc
    root.rotation_euler = (root.rotation_euler.x, root.rotation_euler.y, math.radians(yaw))
    root.scale = tuple(c * scale for c in root.scale)
    return root


def kit(name, loc, yaw=0.0, scale=1.0):
    return load(os.path.join(A, "Resources/kit/%s.fbx" % name), PAL, loc, yaw, scale, "Closest")


def char(name, loc, yaw=0.0, scale=1.0):
    return load(os.path.join(A, "Art/Meshy/%s_lod0.fbx" % name), os.path.join(A, "Art/Meshy/%s_base.png" % name), loc, yaw, scale)


def cyl(loc, r, h, mat, verts=48):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=h, location=(loc[0], loc[1], loc[2] + h / 2))
    o = bpy.context.active_object
    o.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    return o


def backdrop(hexcol, size=60):
    """A big seamless card behind / under the subject so the icon is full bleed."""
    bpy.ops.mesh.primitive_plane_add(size=size, location=(0, 0, 0))
    floor = bpy.context.active_object
    floor.data.materials.append(flat("bg", hexcol, rough=0.9))
    bpy.ops.mesh.primitive_plane_add(size=size, location=(0, size * 0.25, 0), rotation=(math.radians(90), 0, 0))
    bpy.context.active_object.data.materials.append(flat("bg2", hexcol, rough=0.9))


def bean(loc, rot, s=0.09):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=s, location=loc, segments=16, ring_count=8)
    o = bpy.context.active_object
    o.scale = (1.0, 0.7, 0.45)
    o.rotation_euler = rot
    o.data.materials.append(flat("bean", COFFEE, rough=0.35))
    bpy.ops.object.shade_smooth()


def takeaway_cup(loc, r=0.42, h=0.78):
    cyl(loc, r, h, flat("cup", CUP))
    cyl((loc[0], loc[1], loc[2] + h * 0.3), r * 1.035, h * 0.34, flat("sleeve", BRAND))
    cyl((loc[0], loc[1], loc[2] + h), r * 1.06, 0.07, flat("lid", "#FFFFFF"))
    cyl((loc[0], loc[1], loc[2] + h + 0.07), r * 0.75, 0.05, flat("lid2", "#FFFFFF"))


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
    bpy.context.scene.camera = cam


def render(path, w, h):
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = w, h
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGB"
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("RENDER", path)


def feature():
    reset(bg="#A9CFE0", strength=1.0)
    kit("shell_cart", (0, -0.4, 0))
    kit("cart", (0, 0.6, 0))
    kit("espresso_1", (0.05, 0.75, 1.1))
    kit("grinder", (-0.95, 0.75, 1.1))
    char("barista", (1.8, 0.45, 0), yaw=-30)
    char("customer", (-1.55, -0.75, 0), yaw=-40)
    char("cat", (0.9, -1.25, 0), yaw=-25, scale=1.3)
    char("sack", (2.75, 1.7, 0), yaw=-20)
    char("plant", (-3.3, 1.9, 0), yaw=10)
    camera((-1.4, -8.6, 3.1), (-0.9, 0.6, 1.05), lens=34)
    render(os.path.join(STORE, "feature-graphic.png"), 1024, 500)


def icon_cup():
    reset(bg=TEAL)
    backdrop(TEAL)
    takeaway_cup((0.12, 0.15, 0))
    char("croissant", (-0.55, -0.3, 0), yaw=25, scale=1.7)
    for i, (x, y) in enumerate(((0.75, -0.35), (0.62, -0.62), (0.9, -0.62))):
        bean((x, y, 0.04), (0, 0, math.radians(35 * i)), s=0.1)
    camera((0, -5.0, 3.0), (0.0, 0.0, 0.45), ortho=1.75)
    render(os.path.join(STORE, "icon-candidates/c1.png"), 512, 512)


def icon_roaster():
    reset(bg=NAVY)
    backdrop(NAVY)
    kit("roaster", (0.0, 0.0, 0), yaw=-18)
    for i, (x, y) in enumerate(((-0.75, -0.55), (-0.95, -0.35), (-0.6, -0.3), (0.95, -0.7))):
        bean((x, y, 0.05), (0, 0, math.radians(40 * i)), s=0.09)
    camera((1.2, -5.2, 2.6), (0.2, 0.0, 0.85), ortho=2.15)
    render(os.path.join(STORE, "icon-candidates/c2.png"), 512, 512)


def icon_latte():
    reset(bg=WALNUT)
    backdrop(WALNUT)
    cyl((0, 0, 0), 0.95, 0.06, flat("saucer", CUP, rough=0.3))
    cyl((0, 0, 0.06), 0.7, 0.42, flat("cup", CUP, rough=0.3))
    bpy.ops.mesh.primitive_torus_add(major_radius=0.18, minor_radius=0.06, location=(0.82, 0, 0.3), rotation=(math.radians(90), 0, 0))
    bpy.context.active_object.data.materials.append(flat("handle", CUP, rough=0.3))
    cyl((0, 0, 0.48), 0.6, 0.02, flat("coffee", "#6B3F26", rough=0.5))
    foam = flat("foam", FOAM, rough=0.6)
    # heart: two round lobes + a triangle pointing down (towards the bottom of the frame), flat on the crema
    for x in (-0.16, 0.16):
        bpy.ops.mesh.primitive_cylinder_add(vertices=40, radius=0.2, depth=0.01, location=(x, 0.1, 0.505))
        bpy.context.active_object.data.materials.append(foam)
    me = bpy.data.meshes.new("tip")
    me.from_pydata([(-0.345, 0.06, 0.505), (0.345, 0.06, 0.505), (0.0, -0.4, 0.505)], [], [(0, 1, 2)])
    tip = bpy.data.objects.new("tip", me)
    bpy.context.scene.collection.objects.link(tip)
    me.materials.append(foam)
    for i, (x, y) in enumerate(((-0.95, 0.8), (-0.75, 0.95), (0.95, -0.85))):
        bean((x, y, 0.04), (0, 0, math.radians(50 * i)), s=0.11)
    camera((0, -0.35, 6.0), (0, 0, 0.3), ortho=2.25)
    render(os.path.join(STORE, "icon-candidates/c3.png"), 512, 512)


def main():
    os.makedirs(os.path.join(STORE, "icon-candidates"), exist_ok=True)
    feature()
    icon_cup()
    icon_roaster()
    icon_latte()
    print("RENDER_STORE_DONE")


main()
