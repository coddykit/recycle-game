"""Ridgeback Rangers — Blender kit (fleet: buildings / vehicles = Blender, creatures + hero characters = Meshy). Builds
low-poly props whose UVs point at swatches of the game palette (unity/Assets/Resources/materials/palette.png, order =
RidgebackRangers.World.Pal), so the whole island shares ONE material. No film-franchise imagery (no gate with torches,
no black-and-yellow jeeps): a timber sanctuary gate, a ranger cabin, a red barn, a white vet bay, a hatchery dome. Bevelled boxes + cylinders, 1 unit = 1 m, pivot bottom-centre, front = -Y in
Blender (Unity -Z after the FBX axis swap). Exports FBX into unity/Assets/Resources/kit/ and a contact sheet.
  blender -b --python ridgeback-rangers/tools/art/kit.py -- <unity dir> [<sheet.png>]
Budget (Low draws: one material, one mesh per prop): building ≤ 700 tris, tram car ≤ 500. The game draws kit props at 2.2×.
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

# swatches (RidgebackRangers.World.Pal)
GRASS, GRASSD, PATH, STONE, CREAM, TERRA, BLUEWALL, ROOFRED, SLATE = 0, 1, 2, 4, 5, 6, 7, 8, 9
BRAND, PANEL, WHITE, JUNGLE, WOOD, WATER, SAND, TREE, TRUNK = 10, 11, 12, 13, 14, 15, 16, 17, 18
DARK, STEEL, ROCK, FERN, WARN, YELLOW, MUD, GOLD, SUNSET, FOREST, GLACIER, KHAKI = 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31
EGG = 42
WINDOW = DARK


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


def gate():
    p = Prop("gate")
    for x in (-3.2, 3.2):
        p.box((x, 0, 0), (0.9, 0.9, 4.2), WOOD, top=BRAND, bevel=0.06)          # timber posts
        p.box((x, 0, 4.2), (1.2, 1.2, 0.35), ROCK)
    p.box((0, 0, 3.6), (7.4, 0.5, 0.7), WOOD, bevel=0.05)                       # lintel
    p.box((0, -0.3, 3.65), (3.4, 0.08, 0.5), BRAND)                              # sign board (blank, no text)
    p.box((0, 0, 4.3), (7.8, 1.2, 0.25), JUNGLE, rot=0)                          # leafy roof
    p.box((-4.8, 1.0, 0), (1.8, 1.8, 1.8), CREAM, top=ROOFRED, bevel=0.04)       # ticket booth
    p.box((-4.8, 0.08, 0.9), (1.0, 0.06, 0.6), WINDOW)
    return p.build()


def hq():
    p = Prop("hq")
    p.box((0, 0, 0), (5.6, 3.8, 0.4), STONE)
    p.box((0, 0, 0.4), (5.0, 3.4, 2.4), WOOD, bevel=0.04)
    p.box((0, 0, 2.8), (5.6, 4.0, 0.25), BRAND)                                  # roof slab
    p.box((0, -1.72, 1.2), (1.0, 0.06, 1.6), DARK)                               # door
    for x in (-1.6, 1.6):
        p.box((x, -1.72, 1.6), (1.0, 0.06, 0.7), WINDOW)
    p.cyl((2.2, 1.2, 4.2), 0.08, 2.8, STEEL, segs=6)                              # radio mast
    p.box((2.2, 1.2, 5.4), (0.6, 0.06, 0.4), BRAND)
    return p.build()


def barn():
    p = Prop("barn")
    p.box((0, 0, 0), (5.4, 4.0, 2.8), TERRA, bevel=0.04)
    p.box((0, 0, 2.8), (5.6, 4.2, 0.9), SLATE, bevel=0.2)                        # roof
    p.box((0, -2.02, 1.0), (2.2, 0.06, 2.0), WOOD)                               # barn door
    p.box((0, -2.06, 1.0), (2.2, 0.04, 0.12), WHITE)
    p.cyl((3.5, 0.6, 1.8), 0.9, 3.6, STEEL, segs=10)                              # feed silo
    p.cyl((3.5, 0.6, 3.75), 0.95, 0.3, ROOFRED, segs=10)
    for i in range(3):
        p.box((-3.5 + i * 0.75, -1.6, 0), (0.6, 0.6, 0.55), FERN, bevel=0.03)    # crates
    return p.build()


def vet():
    p = Prop("vet")
    p.box((0, 0, 0), (4.8, 3.6, 2.4), WHITE, top=GLACIER, bevel=0.05)
    p.box((0, -1.82, 1.0), (1.4, 0.06, 1.8), GLACIER)
    p.box((1.6, -1.82, 1.5), (1.0, 0.06, 0.7), WINDOW)
    p.box((0, -0.2, 2.45), (1.0, 0.25, 1.0), WARN)                               # plain red cross block (no text)
    p.box((0, -0.2, 2.45), (0.35, 0.3, 1.4), WARN)
    p.box((-3.0, 0, 0), (1.4, 3.0, 1.6), STEEL, top=WHITE, bevel=0.04)           # treatment pen
    return p.build()


def hatchery():
    p = Prop("hatchery")
    p.box((0, 0, 0), (4.6, 3.6, 2.0), CREAM, top=GLACIER, bevel=0.05)
    p.cyl((0, 0, 2.6), 1.6, 1.2, GLACIER, segs=12)                                # glass dome drum
    p.cyl((0, 0, 3.3), 1.0, 0.3, WHITE, segs=12)
    p.box((0, -1.82, 0.9), (1.2, 0.06, 1.6), WINDOW)
    for x in (-1.4, 1.4):
        p.cyl((x, -2.3, 0.35), 0.32, 0.7, EGG, segs=8)                            # eggs on display
    return p.build()


def station():
    p = Prop("station")
    p.box((0, 0, 0), (8.0, 3.2, 0.3), STONE)                                     # platform
    for x in (-3.4, 0, 3.4):
        p.box((x, 0.8, 0.3), (0.25, 0.25, 2.4), WOOD)
    p.box((0, 0.6, 2.7), (8.6, 2.6, 0.2), ROOFRED, bevel=0.05)
    for x in (-2.4, 2.4):
        p.box((x, 0.9, 0.3), (1.6, 0.5, 0.45), WOOD)                              # benches
    return p.build()


def view():
    p = Prop("view")
    for x in (-1.0, 1.0):
        for y in (-1.0, 1.0):
            p.box((x, y, 0), (0.25, 0.25, 2.8), WOOD)
    p.box((0, 0, 2.8), (2.8, 2.8, 0.2), WOOD)                                     # viewing deck
    p.box((0, 0, 3.0), (2.9, 0.1, 0.6), WOOD)
    p.box((0, 0, 4.2), (3.2, 3.2, 0.25), ROOFRED, bevel=0.1)
    p.box((3.0, -0.8, 0), (2.0, 1.4, 0.8), WOOD, top=FERN, bevel=0.03)            # the feeding station trough
    return p.build()


def tram_car():
    p = Prop("tram_car")
    p.box((0, 0, 0.35), (1.4, 3.0, 0.45), BRAND, bevel=0.06)                      # chassis
    p.box((0, 0, 0.8), (1.4, 3.0, 0.5), KHAKI, bevel=0.04)                        # open body
    for x in (-0.62, 0.62):
        for y in (-1.35, 1.35):
            p.box((x, y, 1.3), (0.1, 0.1, 0.9), WOOD)                             # canopy posts
    p.box((0, 0, 2.2), (1.55, 3.2, 0.12), WHITE)                                  # canopy
    for y in (-0.8, 0.2, 1.1):
        p.box((0, y, 0.95), (1.2, 0.35, 0.3), WOOD)                               # benches
    for x in (-0.65, 0.65):
        for y in (-1.0, 1.0):
            p.wheel(x, y, r=0.3, w=0.2)
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
        x += 9.0
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 80
    cam.location = (12, -40, 22)
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
    objs = [gate(), hq(), barn(), vet(), hatchery(), station(), view(), tram_car()]
    total = 0
    for ob in objs:
        ob.data.materials.append(mat)
        total += export(ob)
    print("KIT_DONE props", len(objs), "tris", total)
    if SHEET:
        sheet(objs)


main()
