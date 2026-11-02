"""Pour & Roast — Blender kit (fleet: buildings / machines / furniture = Blender, characters + organic hero props = Meshy).
Builds low-poly cafe props whose UVs point at swatches of the game palette (unity/Assets/Resources/materials/palette.png,
order = PourAndRoast.World.Pal), so the whole venue shares ONE material. Bevelled boxes + cylinders, 1 unit = 1 m, pivot
bottom-centre, front = -Y in Blender (Unity -Z after the FBX axis swap). Exports FBX into unity/Assets/Resources/kit/ and a
contact sheet. Same shapes as the procedural fallbacks in PourAndRoast.World.MeshKit (the game uses the kit when present).
  blender -b --python pour-and-roast/tools/art/kit.py -- <unity dir> [<sheet.png>]
Budget (Low draws: one material, one mesh per prop): machine ≤ 600 tris, furniture ≤ 500, cart ≤ 900. No text anywhere.
"""
import math
import os
import sys

import bmesh
import bpy

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
UNITY = argv[0] if argv else os.path.join(os.path.dirname(__file__), "../../unity")
SHEET = argv[1] if len(argv) > 1 else ""
OUT = os.path.join(UNITY, "Assets/Resources/kit")
PALETTE = os.path.join(UNITY, "Assets/Resources/materials/palette.png")

# swatches (PourAndRoast.World.Pal)
FLOOR, QUAY, CREAM, TRIM, TOP, WALNUT, BRAND, STEEL, STEELD, BLACK, COPPER = 0, 2, 4, 7, 8, 9, 10, 11, 12, 13, 14
AWNING, TEAL, PLANT, WINDOW, COFFEE, MILK, GREENB, SACK, PASTRY, CHALK, CUP, WHITE = 16, 17, 18, 20, 21, 22, 23, 26, 27, 28, 45, 47


def uv_of(sw):
    x, y = sw % 8, sw // 8
    return ((x + 0.5) / 8.0, 1.0 - (y + 0.5) / 8.0)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


class Prop:
    """Accumulates parts in one bmesh; each part carries its swatch."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def _paint(self, faces, sw):
        u = uv_of(sw)
        for f in faces:
            for l in f.loops:
                l[self.uv].uv = u

    def box(self, center, size, sw, top=None, bevel=0.0, rot=0.0):
        import mathutils
        cx, cy, cz = center
        sx, sy, sz = size
        before = set(self.bm.faces)
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = res["verts"]
        for v in verts:
            v.co.x *= sx
            v.co.y *= sy
            v.co.z *= sz
        if rot:
            bmesh.ops.rotate(self.bm, verts=verts, cent=(0, 0, 0), matrix=mathutils.Matrix.Rotation(math.radians(rot), 3, "Z"))
        bmesh.ops.translate(self.bm, verts=verts, vec=(cx, cy, cz + sz / 2))
        if bevel > 0:
            edges = list({e for v in verts for e in v.link_edges})
            bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=1, affect="EDGES")
        faces = [f for f in self.bm.faces if f not in before]
        for f in faces:
            f.normal_update()
        self._paint(faces, sw)
        if top is not None:
            topz = max(v.co.z for f in faces for v in f.verts)
            self._paint([f for f in faces if f.normal.z > 0.9 and all(abs(v.co.z - topz) < 1e-4 for v in f.verts)], top)
        return faces

    def cyl(self, center, radius, depth, sw, segs=10, axis="Z"):
        before = set(self.bm.faces)
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=segs, radius1=radius, radius2=radius, depth=depth)
        verts = res["verts"]
        import mathutils
        if axis == "X":
            bmesh.ops.rotate(self.bm, verts=verts, cent=(0, 0, 0), matrix=mathutils.Matrix.Rotation(math.radians(90), 3, "Y"))
        elif axis == "Y":
            bmesh.ops.rotate(self.bm, verts=verts, cent=(0, 0, 0), matrix=mathutils.Matrix.Rotation(math.radians(90), 3, "X"))
        bmesh.ops.translate(self.bm, verts=verts, vec=center)
        self._paint([f for f in self.bm.faces if f not in before], sw)

    def wheel(self, x, y, r=0.42, w=0.3):
        self.cyl((x, y, r), r, w, WINDOW, segs=10, axis="X")

    def build(self):
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        for p in me.polygons:
            p.use_smooth = False
        return ob


def cart():
    p = Prop("cart")
    p.box((0, 0, 0.35), (2.6, 1.1, 0.75), WALNUT, top=TOP, bevel=0.04)
    for x in (-1.0, 1.0):
        p.cyl((x, -0.56, 0.32), 0.32, 0.08, BLACK, segs=12, axis="Y")
        p.cyl((x, -0.6, 0.32), 0.12, 0.02, STEELD, segs=8, axis="Y")
    for x in (-1.2, 1.2):
        p.box((x, 0.45, 1.1), (0.08, 0.08, 1.4), TRIM)
    p.box((0, 0.1, 2.5), (3.0, 1.6, 0.08), BRAND, top=AWNING)                         # awning
    for i in range(6):                                                               # awning stripes
        p.box((-1.25 + i * 0.5, -0.71, 2.38), (0.25, 0.04, 0.2), AWNING if i % 2 else BRAND)
    p.box((0, -0.57, 0.45), (1.6, 0.04, 0.4), CHALK)                                 # chalk board (blank)
    p.box((1.45, 0, 0.45), (0.3, 0.6, 0.06), TRIM)                                   # push handle
    return p.build()


def counter():
    p = Prop("counter")
    p.box((0, 0, 0), (5.0, 0.8, 1.0), WALNUT, top=TOP, bevel=0.03)
    p.box((0, -0.41, 0.05), (4.8, 0.02, 0.12), COPPER)
    for i in range(5):
        p.box((-2.0 + i * 1.0, -0.41, 0.35), (0.7, 0.02, 0.5), TRIM)                # panels
    return p.build()


def back_bar():
    p = Prop("back_bar")
    p.box((0, 0, 0), (5.0, 0.6, 0.9), TRIM, top=TOP, bevel=0.03)
    p.box((0, 0.2, 1.7), (5.0, 0.3, 0.06), WALNUT)
    for i in range(10):
        p.cyl((-2.2 + i * 0.48, 0.2, 1.81), 0.05, 0.1, CUP if i % 3 else BRAND, segs=6)   # cups on the shelf
    return p.build()


def espresso(heads):
    p = Prop("espresso_%d" % heads)
    w = 0.5 + 0.35 * heads
    p.box((0, 0, 0), (w, 0.5, 0.45), BLACK, top=STEEL, bevel=0.03)
    p.box((0, 0.05, 0.45), (w - 0.1, 0.4, 0.12), STEEL, bevel=0.02)
    p.box((0, -0.26, 0.02), (w - 0.1, 0.02, 0.08), STEELD)                           # drip tray
    for h in range(heads):
        x = -w / 2 + 0.42 + h * 0.35
        p.box((x, -0.28, 0.2), (0.12, 0.12, 0.08), STEELD)
        p.box((x, -0.36, 0.22), (0.03, 0.14, 0.02), BLACK)                           # portafilter handle
        p.cyl((x, -0.3, 0.06), 0.045, 0.08, CUP, segs=8)
    p.box((w / 2 + 0.04, -0.15, 0.25), (0.04, 0.04, 0.25), STEEL)                    # steam wand
    p.cyl((-w / 2 + 0.2, 0.1, 0.62), 0.05, 0.06, COPPER, segs=8)                       # pressure gauge
    return p.build()


def grinder():
    p = Prop("grinder")
    p.box((0, 0, 0), (0.22, 0.28, 0.35), BLACK, bevel=0.02)
    p.cyl((0, 0, 0.46), 0.11, 0.22, WINDOW, segs=10)
    p.cyl((0, 0, 0.42), 0.1, 0.12, COFFEE, segs=10)
    return p.build()


def fridge():
    p = Prop("fridge")
    p.box((0, 0, 0), (0.7, 0.55, 0.85), STEEL, top=TOP, bevel=0.02)
    p.box((0, -0.28, 0.12), (0.55, 0.02, 0.6), WINDOW)
    for i in range(3):
        p.box((-0.15 + i * 0.15, -0.18, 0.2), (0.1, 0.1, 0.22), MILK)
    return p.build()


def pastry_case():
    p = Prop("pastry_case")
    p.box((0, 0, 0), (1.0, 0.7, 1.0), WALNUT, top=TOP, bevel=0.03)
    p.box((0, 0, 1.0), (0.95, 0.6, 0.4), WINDOW)
    for i in range(3):
        p.box((-0.3 + i * 0.3, 0, 1.05), (0.18, 0.14, 0.08), PASTRY, bevel=0.03, rot=20 * i)
    return p.build()


def pickup_shelf():
    p = Prop("pickup_shelf")
    p.box((0, 0, 0), (0.8, 0.35, 0.05), WALNUT)
    p.box((0, 0, 0.3), (0.8, 0.35, 0.04), WALNUT)
    for x in (-0.38, 0.38):
        p.box((x, 0, 0), (0.04, 0.35, 0.35), TRIM)
    return p.build()


def ice_maker():
    p = Prop("ice_maker")
    p.box((0, 0, 0), (0.6, 0.6, 0.9), STEEL, top=STEELD, bevel=0.02)
    p.box((0, -0.31, 0.55), (0.4, 0.02, 0.2), WINDOW)
    return p.build()


def roaster():
    p = Prop("roaster")
    p.box((0, 0, 0), (1.1, 0.9, 0.7), BLACK, bevel=0.03)
    p.cyl((0, 0.05, 0.98), 0.42, 0.55, BRAND, segs=14, axis="Y")                      # drum (on its side)
    p.cyl((0, -0.25, 0.98), 0.3, 0.04, COPPER, segs=14, axis="Y")                     # drum door
    p.box((0, 0.05, 1.25), (0.36, 0.36, 0.25), COPPER, bevel=0.02)                    # hopper
    p.cyl((0.75, -0.1, 0.2), 0.45, 0.35, STEELD, segs=14)                             # cooling tray
    p.cyl((0.75, -0.1, 0.36), 0.4, 0.04, COFFEE, segs=14)
    p.cyl((-0.2, 0.3, 1.85), 0.07, 1.2, STEELD, segs=8)                               # chimney
    return p.build()


def table():
    p = Prop("table")
    p.cyl((0, 0, 0.36), 0.06, 0.72, TRIM, segs=8)
    p.cyl((0, 0, 0.745), 0.45, 0.05, TOP, segs=14)
    for x in (-0.7, 0.7):
        p.box((x, 0, 0), (0.4, 0.4, 0.45), TRIM, top=WALNUT, bevel=0.02)
        p.box((x * 1.26, 0, 0.45), (0.05, 0.4, 0.45), TRIM)
    p.cyl((0.12, 0, 0.815), 0.05, 0.09, CUP, segs=8)
    return p.build()


def rest_rack():
    p = Prop("rest_rack")
    for i in range(3):
        p.box((0, 0, i * 0.55), (1.2, 0.45, 0.04), STEELD)
    for x in (-0.58, 0.58):
        p.box((x, 0, 0), (0.04, 0.45, 1.2), STEEL)
    for i in range(4):
        p.box((-0.42 + i * 0.28, 0, 0.6), (0.2, 0.18, 0.3), SACK, top=COFFEE, bevel=0.03)
    return p.build()


def packing_bench():
    p = Prop("packing_bench")
    p.box((0, 0, 0), (1.4, 0.6, 0.85), WALNUT, top=TOP, bevel=0.03)
    p.box((-0.3, 0, 0.85), (0.3, 0.2, 0.35), SACK, top=COFFEE, bevel=0.03)
    p.box((0.25, 0, 0.85), (0.35, 0.3, 0.12), STEEL, bevel=0.02)                      # scale
    return p.build()


def export(ob):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    path = os.path.join(OUT, ob.name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                             object_types={"MESH"}, path_mode="STRIP", embed_textures=False, mesh_smooth_type="FACE", add_leaf_bones=False)
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    print("KIT", ob.name, "tris", tris, "->", path)
    return tris


def material():
    m = bpy.data.materials.new("palette")
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    tex = m.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(PALETTE)
    tex.interpolation = "Closest"
    m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.8
    return m


def sheet(objs):
    import mathutils
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = 1600, 400
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("w"); world.color = (0.85, 0.9, 0.92); scene.world = world
    x = -20.0
    for ob in objs:
        ob.location = (x, 0, 0)
        x += 5.5 if ob.name not in ("counter", "cart") else 7.0
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 62
    cam.location = (8, -30, 16)
    cam.rotation_euler = mathutils.Euler((math.radians(62), 0, 0))
    scene.camera = cam
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.5
    sun.rotation_euler = mathutils.Euler((math.radians(50), math.radians(10), math.radians(-35)))
    scene.collection.objects.link(sun)
    scene.render.filepath = SHEET
    bpy.ops.render.render(write_still=True)
    print("SHEET", SHEET)


def main():
    reset()
    os.makedirs(OUT, exist_ok=True)
    mat = material()
    objs = [cart(), counter(), back_bar()] + [espresso(h) for h in range(1, 7)] + [grinder(), fridge(), pastry_case(), pickup_shelf(),
                                                                                  ice_maker(), roaster(), table(), rest_rack(), packing_bench()]
    total = 0
    for ob in objs:
        ob.data.materials.append(mat)
        total += export(ob)
    print("KIT_DONE props", len(objs), "tris", total)
    if SHEET:
        sheet([o for o in objs if o.name in ("cart", "counter", "espresso_3", "grinder", "fridge", "pastry_case", "roaster", "table", "rest_rack", "packing_bench")])


main()
