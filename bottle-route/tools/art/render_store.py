"""Bottle Route — store art from the repo's own models (polish pass 2026-11-02): the Play feature graphic (1024 × 500, no
text: the tagline in store/screenshots/en.json is composited later) and three icon candidates (512 × 512, full-bleed, no
text, bold silhouettes that still read at 48 px). Uses the Blender kit builders in kit.py (buildings, depot, props on the
palette UVs), the kit FBX props and the Meshy bales / characters. EEVEE, headless.
  blender -b --python bottle-route/tools/art/render_store.py -- <unity dir> <store dir>
Outputs: <store>/feature-graphic.png, <store>/icon-candidates/c1.png (deposit machine) · c2.png (collection van) ·
c3.png (bale stack). The placeholder app icon in unity/Assets/Art/Icon stays until the owner picks one (docs/ART.md).
"""
import math
import os
import random
import sys

import bpy
import mathutils

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit  # noqa: E402  (kit.py only builds geometry on import; its main() runs when it is the --python script)

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
UNITY, STORE = argv[0], argv[1]
A = os.path.join(UNITY, "Assets")
PALETTE = os.path.join(A, "Resources/materials/palette.png")
_mat = {}


def reset(bg):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mat.clear()
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.eevee.taa_render_samples = 32
    sc.eevee.use_soft_shadows = True
    sc.eevee.use_gtao = True
    sc.eevee.gtao_distance = 2.0
    sc.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (*bg, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.85
    sc.world = w
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.4
    sun.data.angle = math.radians(4)
    sun.rotation_euler = mathutils.Euler((math.radians(48), math.radians(10), math.radians(-38)))
    sc.collection.objects.link(sun)
    fill = bpy.data.objects.new("fill", bpy.data.lights.new("fill", "SUN"))
    fill.data.energy = 0.8
    fill.data.use_shadow = False
    fill.rotation_euler = mathutils.Euler((math.radians(65), 0, math.radians(150)))
    sc.collection.objects.link(fill)


def palette_mat():
    if "pal" not in _mat:
        m = bpy.data.materials.new("palette")
        m.use_nodes = True
        bsdf = m.node_tree.nodes["Principled BSDF"]
        tex = m.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(PALETTE)
        tex.interpolation = "Closest"
        m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.8
        _mat["pal"] = m
    return _mat["pal"]


def tex_mat(png):
    if png not in _mat:
        m = bpy.data.materials.new(os.path.basename(png))
        m.use_nodes = True
        bsdf = m.node_tree.nodes["Principled BSDF"]
        tex = m.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(png)
        m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.85
        _mat[png] = m
    return _mat[png]


def place(ob, loc, yaw=0.0, scale=1.0):
    ob.location = loc
    ob.rotation_euler = (ob.rotation_euler.x, ob.rotation_euler.y, math.radians(yaw))
    ob.scale = (ob.scale.x * scale, ob.scale.y * scale, ob.scale.z * scale)
    return ob


def built(builder, loc, yaw=0.0, scale=1.0, wall=None, roof=None, **kw):
    """A kit builder (kit.py) → a palette-textured object in this scene. The street side of buildings is local -X.
    wall / roof: the same palette swap the game does (CREAM → wall swatch, ROOFRED → roof swatch)."""
    ob = builder(**kw)
    ob.data.materials.append(palette_mat())
    swap = {}
    if wall is not None:
        swap[kit.CREAM] = wall
    if roof is not None:
        swap[kit.ROOFRED] = roof
    if swap:
        uvl = ob.data.uv_layers.active.data
        for l in uvl:
            u, v = l.uv
            sw = int((1.0 - v) * 8) * 8 + int(u * 8)
            if sw in swap:
                l.uv = kit.uv_of(swap[sw])
    return place(ob, loc, yaw, scale)


def fbx(path, mat, loc, yaw=0.0, scale=1.0):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    root = next(o for o in new if o.parent is None)
    for o in new:
        if o.type == "MESH":
            o.data.materials.clear()
            o.data.materials.append(mat)
    return place(root, loc, yaw, scale)


def kitprop(name, loc, yaw=0.0, scale=1.0):
    return fbx(os.path.join(A, "Resources/kit/%s.fbx" % name), palette_mat(), loc, yaw, scale)


def bale(m, loc, yaw=0.0, scale=1.0):
    return fbx(os.path.join(A, "Resources/kit/bale_%s_lod1.fbx" % m), tex_mat(os.path.join(A, "Resources/kit/bale_%s_base.png" % m)), loc, yaw, scale)


def char(name, loc, yaw=0.0, scale=1.0):
    return fbx(os.path.join(A, "Art/Meshy/%s_lod0.fbx" % name), tex_mat(os.path.join(A, "Art/Meshy/%s_base.png" % name)), loc, yaw, scale)


def slab(name, center, size, sw, rot=0.0):
    p = kit.Prop(name)
    p.box(center, size, sw, rot=rot)
    return built(lambda: p.build(), (0, 0, 0))


def tree(loc, s=1.0):
    p = kit.Prop("tree")
    p.box((0, 0, 0), (0.6, 0.6, 2.2), kit.TRUNK)
    p.box((0, 0, 2.0), (3.2, 3.2, 2.4), kit.TREE, bevel=0.15, rot=15)
    p.box((0, 0, 4.2), (2.0, 2.0, 1.6), kit.TREE, bevel=0.12, rot=40)
    return built(lambda: p.build(), loc, scale=s)


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
    cam.data.clip_end = 2000
    bpy.context.scene.camera = cam
    return cam


def render(path, w, h):
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = w, h
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGB"
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("RENDER", path)


def feature():
    """A harbour street in the morning: machines with citizens bringing bags, the van on its round, the depot with bales."""
    reset((0.62, 0.84, 0.86))
    slab("ground", (0, 30, -0.1), (300, 200, 0.1), kit.GRASS)
    slab("sea", (0, -130, -0.6), (400, 100, 0.1), 15)
    slab("sand", (0, -73, -0.1), (400, 8, 0.12), 16)
    slab("road", (0, -18, 0), (300, 9, 0.06), kit.ROAD)                       # the harbour front road (east-west)
    slab("road2", (6, 40, 0), (9, 110, 0.06), kit.ROAD)                       # a street up into town
    slab("road3", (0, 30, 0), (300, 8, 0.06), kit.ROAD)                       # a back street
    slab("plaza", (-14, -6, 0), (22, 14, 0.08), kit.CONCRETE)
    rnd = random.Random(4)
    walls = [kit.CREAM, kit.TERRA, kit.BLUEWALL, kit.CREAM]
    roofs = [kit.ROOFRED, kit.ROOFGREY]
    builders = [kit.bld_harbour_a, kit.bld_harbour_b, kit.bld_old_a, kit.bld_old_b]

    def b(i, loc, yaw):
        return built(builders[i % 4], loc, yaw=yaw, scale=1.0 + rnd.random() * 0.1, wall=walls[rnd.randrange(4)], roof=roofs[rnd.randrange(2)])
    for i, x in enumerate((-110, -96, -81, -66, -51, -36, 26, 40, 54, 68, 82, 96)):
        b(i, (x, -5.0, 0), 90)                                                # facing the front road
        if abs(x) > 30:
            b(i + 2, (x, 17.5, 0), -90)                                        # facing the back street
    for i, x in enumerate((-100, -82, -64, -46, 28, 46, 64, 82)):
        built((kit.bld_uni_a, kit.bld_uni_b, kit.bld_old_b, kit.bld_old_a)[i % 4], (x, 44, 0), yaw=90, wall=walls[rnd.randrange(4)], roof=roofs[i % 2])
    for i, y in enumerate((52, 68, 84)):
        b(i + 1, (-4.0, y, 0), 180)
        b(i + 3, (16.5, y, 0), 0)
    built(kit.stadium, (120, 95, 0), yaw=0, scale=0.9)
    for x, y in ((-30, 4), (34, 6), (-58, 6), (-74, 4), (60, 8), (-90, 6), (88, 5), (-6, 18), (20, 20), (-120, 40), (110, 40)):
        tree((x, y, 0), 1.1 + rnd.random() * 0.4)
    # the deposit corner: two machines + a shop, citizens with bags
    kitprop("machine_m", (-19, -9.0, 0.08), yaw=0, scale=2.2)
    kitprop("machine_s", (-12, -9.0, 0.08), yaw=0, scale=2.2)
    kitprop("shop", (-6, 4.5, 0), yaw=180, scale=1.5)
    char("citizen", (-23.0, -13.0, 0), yaw=-20, scale=2.6)
    char("citizen", (-8.0, -13.4, 0), yaw=25, scale=2.6)
    char("operator", (-15.5, -14.0, 0), yaw=5, scale=2.6)
    # trucks on the front road
    kitprop("van", (14, -19.5, 0), yaw=90, scale=1.7)
    kitprop("compactor", (-46, -16.5, 0), yaw=-90, scale=1.6)
    # depot compound between the road and the beach (authored in depot-local Unity x/z = Blender x/y)
    dep = (52, -48, 0)
    built(kit.depot_hall, dep)
    for k in range(12):
        col, row = k % 6, (k // 6) % 2
        bale("pet" if k % 3 else "alu", (dep[0] + 18 + col * 4.4, dep[1] + 10 + row * 4.4, 0), scale=3.6)
    for k in range(5):
        bale("pet" if k % 2 else "alu", (dep[0] + 20 + k * 4.4, dep[1] + 10, 3.6), scale=3.6)
    kitprop("van", (dep[0] - 20, dep[1] + 14, 0), yaw=0, scale=1.7)
    kitprop("bike", (dep[0] - 14, dep[1] + 14, 0), yaw=0, scale=1.7)
    camera((-2, -158, 96), (24, -18, 0), lens=42)
    render(os.path.join(STORE, "feature-graphic.png"), 1024, 500)


def icon_machine():
    reset((0.36, 0.78, 0.66))
    slab("disc", (0, 0, -0.05), (30, 30, 0.05), kit.GRASS)
    kitprop("machine_m", (0.45, 0.35, 0), yaw=-24)
    bale("pet", (-1.25, -0.6, 0), yaw=18, scale=0.95)
    camera((2.2, -6.2, 2.9), (-0.05, 0, 1.25), ortho=4.1)
    render(os.path.join(STORE, "icon-candidates/c1.png"), 512, 512)


def icon_van():
    reset((0.98, 0.78, 0.36))
    slab("road", (0, 0, -0.05), (40, 3.2, 0.05), kit.ROAD)
    slab("verge", (0, 3.2, -0.06), (40, 4, 0.05), kit.GRASS)
    kitprop("van", (0.3, -0.2, 0), yaw=-62)
    kitprop("machine_s", (-2.9, 2.6, 0), yaw=-20, scale=1.15)
    camera((5.2, -6.5, 4.2), (-0.5, 0.6, 1.1), ortho=6.9)
    render(os.path.join(STORE, "icon-candidates/c2.png"), 512, 512)


def icon_bales():
    reset((0.10, 0.42, 0.36))
    slab("pad", (0, 0, -0.05), (30, 30, 0.05), kit.TGREEN)
    for i, m in enumerate(("pet", "alu", "pet")):
        bale(m, ((i - 1) * 1.08, 0, 0), yaw=(i - 1) * 6)
    bale("alu", (-0.54, 0, 1.02), yaw=4)
    bale("pet", (0.54, 0, 1.02), yaw=-5)
    bale("pet", (0, 0, 2.04), yaw=10)
    camera((2.4, -6.4, 3.2), (0, 0, 1.45), ortho=3.9)
    render(os.path.join(STORE, "icon-candidates/c3.png"), 512, 512)


def main():
    os.makedirs(os.path.join(STORE, "icon-candidates"), exist_ok=True)
    feature()
    icon_machine()
    icon_van()
    icon_bales()
    print("STORE_RENDER_DONE")


main()
