"""Semester Rush — Blender kit (fleet: buildings / machines / vehicles = Blender, characters = Meshy). Builds low-poly props
whose UVs point at swatches of the game palette (unity/Assets/Resources/materials/palette.png, order = SemesterRush.World.Pal),
so the whole town shares ONE material. Bevelled boxes + cylinders, 1 unit = 1 m, pivot bottom-centre, front = -Y in
Blender (Unity -Z after the FBX axis swap). Exports FBX into unity/Assets/Resources/kit/ and a contact sheet.
  blender -b --python semester-rush/tools/art/kit.py -- <unity dir> [<sheet.png>]
Budget (Low draws: one material, one mesh per prop): building ≤ 1500 tris, small prop ≤ 600.
Campus dressing (polish pass 2026-11-02) goes to Resources/kit/merge/: trees, lamps, benches, bike racks and the shortcut
gates. CampusWorld MERGES these into the zone / path meshes (readable import, 0 extra draws on every tier); Medium / High
use them, Low keeps the cheaper procedural trees and gates (Low must not get heavier).
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

# swatches (SemesterRush.World.Pal)
GRASS, PATH, STONE, CREAM, BRICK, BLUEWALL, ROOFRED, SLATE = 0, 2, 4, 5, 6, 7, 8, 9
BRAND, TRIM, WHITE, IVY, WINLIGHT, SAND, TREE, TRUNK, WINDOW, YELLOW, WOOD, GOLD, COPPER = 10, 11, 12, 13, 14, 16, 17, 18, 20, 25, 26, 27, 31


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


def gable(p, cx, cy, z, w, d, h, sw):
    """A gable roof as a thin rotated box pair (two slopes) over a w x d footprint at height z."""
    import math as m
    ang = m.degrees(m.atan2(h, d / 2))
    slope = m.hypot(h, d / 2)
    for side in (-1, 1):
        f = p.box((cx, cy + side * d / 4, z + h / 2 - 0.1), (w + 0.4, slope, 0.18), sw)
        import mathutils
        verts = list({v for face in f for v in face.verts})
        bmesh.ops.rotate(p.bm, verts=verts, cent=(cx, cy + side * d / 4, z + h / 2), matrix=mathutils.Matrix.Rotation(m.radians(-side * ang), 3, "X"))


def windows(p, w, d, h, sw=WINDOW, step=2.4, start=1.4):
    x = -w / 2 + 1.6
    while x < w / 2 - 1.0:
        y = start
        while y < h - 1.0:
            p.box((x, -d / 2 - 0.03, y), (1.1, 0.08, 1.5), sw)
            y += 2.6
        x += step


def hall(name, w, d, h, wall, roof, extra=None):
    p = Prop(name)
    p.box((0, 0, 0), (w, d, h), wall, top=SLATE, bevel=0.06)
    gable(p, 0, 0, h, w, d + 0.6, h * 0.32, roof)
    windows(p, w, d, h)
    p.box((0, -d / 2 - 0.8, 0), (w * 0.32, 1.6, 0.45), STONE, bevel=0.04)          # steps
    p.box((0, -d / 2 - 0.25, 0.45), (w * 0.22, 0.5, h * 0.55), TRIM, bevel=0.04)   # portal
    p.box((0, -d / 2 - 0.52, 0.5), (w * 0.12, 0.05, h * 0.38), WINDOW)            # door
    if extra:
        extra(p)
    return p.build()


def amphitheatre(p):
    p.box((0, 0, 9.0), (5, 5, 2.6), TRIM, top=COPPER, bevel=0.08)
    for x in (-5.0, -1.7, 1.7, 5.0):
        p.cyl((x, -7.2, 3.2), 0.45, 6.4, TRIM, segs=8)


def lab():
    p = Prop("lab")
    p.box((0, 0, 0), (14, 10, 7), BLUEWALL, top=SLATE, bevel=0.06)
    x = -5.5
    while x <= 5.6:
        p.box((x, -5.03, 1.8), (1.6, 0.08, 3.8), WINLIGHT)
        x += 2.2
    p.cyl((4.0, 2.0, 8.5), 0.6, 3.0, STONE, segs=8)                                  # fume stack
    p.cyl((-4.0, 2.0, 7.6), 1.4, 1.2, WHITE, segs=10)                                # tank
    p.box((0, -5.6, 0), (3.5, 1.2, 0.4), STONE)
    return p.build()


def library():
    def cols(p):
        for x in (-4.5, -1.5, 1.5, 4.5):
            p.cyl((x, -6.0, 3.25), 0.4, 6.5, TRIM, segs=8)
        p.box((0, -6.0, 6.6), (11, 1.4, 0.6), TRIM)
    return hall("library", 13, 10, 7, CREAM, SLATE, cols)


def canteen():
    p = Prop("canteen")
    p.box((0, 0, 0), (12, 9, 5), CREAM, top=ROOFRED, bevel=0.06)
    p.box((0, -5.2, 3.6), (12.4, 1.8, 0.3), BRAND)                                   # awning
    p.box((0, -4.53, 1.0), (9, 0.08, 2.4), WINLIGHT)
    for x in (-3.5, 0.0, 3.5):
        p.cyl((x, -7.5, 0.4), 0.9, 0.08, WOOD, segs=8)                               # terrace tables
        p.cyl((x, -7.5, 0.2), 0.08, 0.4, SLATE, segs=6)
    return p.build()


def dorm():
    p = Prop("dorm")
    p.box((0, 0, 0), (9, 9, 12), BRICK, top=SLATE, bevel=0.06)
    y = 1.5
    while y < 11:
        x = -3.0
        while x <= 3.0:
            p.box((x, -4.53, y), (1.0, 0.08, 1.3), WINDOW)
            x += 2.0
        y += 2.4
    p.box((0, -4.9, 0), (2.4, 0.8, 2.6), TRIM)
    p.box((3.0, 1.0, 12.0), (1.0, 1.0, 1.5), BRICK)                                  # chimney
    return p.build()


def admin():
    def tower(p):
        p.box((0, 0, 7.0), (2.6, 2.6, 4.0), TRIM, bevel=0.04)
        p.cyl((0, -1.33, 9.4), 0.7, 0.1, WHITE, segs=12, axis="Y")                    # clock face (no digits)
        p.box((0, 0, 11.0), (2.9, 2.9, 0.3), COPPER)
        p.cyl((0, 0, 12.0), 0.9, 1.8, COPPER, segs=8)
    return hall("admin", 11, 9, 7, CREAM, SLATE, tower)


def plot():
    p = Prop("plot")
    p.box((0, 0, 0), (11, 8, 0.25), SAND)
    for x in (-5.3, 5.3):
        for y in (-3.8, 3.8):
            p.box((x, y, 0), (0.25, 0.25, 1.2), WOOD)
    p.box((0, -3.8, 0.9), (10.6, 0.06, 0.12), YELLOW)
    return p.build()


def scaffold():
    p = Prop("scaffold")
    p.box((0, 0, 0), (12, 9, 0.4), STONE)
    p.box((-2, 0, 0.4), (5, 4, 3), BRICK, bevel=0.03)
    for x in (-5.5, 5.5):
        for y in (-4, 4):
            p.box((x, y, 0), (0.3, 0.3, 7), YELLOW)
    for z in (3.5, 6.8):
        p.box((0, -4, z), (11.4, 0.3, 0.3), YELLOW)
        p.box((0, 4, z), (11.4, 0.3, 0.3), YELLOW)
    p.box((4.0, 2.0, 0.4), (0.4, 0.4, 9), YELLOW)                                    # crane mast
    p.box((2.0, 2.0, 9.2), (6, 0.4, 0.4), YELLOW)
    return p.build()


def bus_stop():
    p = Prop("bus_stop")
    p.box((0, 0, 0), (6, 2.4, 0.2), STONE)
    for x in (-2.7, 2.7):
        p.box((x, 0.8, 0.2), (0.2, 0.2, 2.6), SLATE)
    p.box((0, 0.2, 2.8), (6.2, 2.2, 0.2), BRAND)
    p.box((0, 1.0, 0.2), (5.4, 0.08, 2.2), WINLIGHT)
    p.box((0, 0.4, 0.2), (4.0, 0.6, 0.5), WOOD)                                      # bench
    p.box((-7.0, -1.2, 0.3), (8, 2.6, 2.8), YELLOW, bevel=0.15)                      # a parked bus
    p.box((-7.0, -2.52, 1.7), (7.2, 0.06, 0.9), WINDOW)
    for x in (-9.5, -4.5):
        p.wheel(x, -1.2 - 1.2, r=0.45)
    return p.build()


# ---------------------------------------------------------------- campus dressing (merged into the zone / path meshes)


def blob(p, center, size, sw, rot=0.0):
    """A chunky canopy lump: a bevelled box (one bevel segment keeps it cheap)."""
    p.box(center, size, sw, bevel=min(size) * 0.22, rot=rot)


def tree_oak():
    p = Prop("tree_oak")
    p.cyl((0, 0, 1.3), 0.32, 2.6, TRUNK, segs=6)
    p.box((0.35, 0, 1.9), (0.9, 0.22, 0.22), TRUNK, rot=25)                          # a branch
    blob(p, (0, 0, 2.2), (3.4, 3.0, 2.2), TREE, rot=12)
    blob(p, (-0.9, 0.5, 3.6), (2.2, 2.0, 1.7), TREE, rot=-20)
    blob(p, (0.8, -0.4, 3.8), (2.0, 1.9, 1.6), IVY, rot=35)
    return p.build()


def tree_maple():
    """An autumn maple (the campus season colour, plan section 8): a taller oval crown in copper with a golden top."""
    p = Prop("tree_maple")
    p.cyl((0, 0, 1.4), 0.28, 2.8, TRUNK, segs=6)
    blob(p, (0, 0, 2.3), (2.8, 2.6, 2.4), COPPER, rot=20)
    blob(p, (0.2, -0.1, 4.2), (2.0, 1.9, 1.9), COPPER, rot=-15)
    blob(p, (-0.3, 0.3, 5.6), (1.2, 1.2, 1.1), YELLOW, rot=40)
    return p.build()


def lamp():
    p = Prop("lamp")
    p.box((0, 0, 0), (0.6, 0.6, 0.3), SLATE, bevel=0.05)
    p.cyl((0, 0, 1.9), 0.09, 3.4, SLATE, segs=6)
    p.box((0, 0, 3.55), (0.55, 0.55, 0.5), WINLIGHT, bevel=0.06)                     # the lantern
    p.box((0, 0, 4.05), (0.75, 0.75, 0.14), SLATE, bevel=0.03)
    return p.build()


def bench():
    p = Prop("bench")
    for x in (-0.8, 0.8):
        p.box((x, 0, 0), (0.12, 0.5, 0.42), SLATE)
    p.box((0, 0, 0.42), (2.0, 0.55, 0.08), WOOD, bevel=0.02)
    p.box((0, 0.28, 0.6), (2.0, 0.08, 0.42), WOOD, bevel=0.02)                      # back rest
    return p.build()


def bike_rack():
    """A rack with two parked bikes (plan section 8 'bikes'), faculty-neutral colours."""
    p = Prop("bike_rack")
    p.box((0, 0, 0), (3.2, 0.12, 0.12), SLATE)
    for i, (x, sw) in enumerate(((-0.8, BRAND), (0.8, YELLOW))):
        for y in (-0.55, 0.55):
            p.cyl((x, y, 0.36), 0.36, 0.06, WINDOW, segs=8, axis="Y")                # wheels
        p.box((x, 0, 0.36), (0.08, 1.0, 0.08), sw)                                   # frame
        p.box((x, 0.15, 0.36), (0.08, 0.08, 0.5), sw)
        p.box((x, 0.15, 0.86), (0.25, 0.3, 0.06), WINDOW)                             # saddle
        p.box((x, -0.45, 0.4), (0.5, 0.06, 0.5), SLATE)                               # bars
    return p.build()


def gate(open_):
    """The shortcut gate in an ivy hedge (open = the leaves swing aside). Front = -Y, the path runs along Y."""
    p = Prop("gate_open" if open_ else "gate_closed")
    for side in (-1, 1):
        p.box((side * 3.7, 0, 0), (3.6, 1.0, 1.5), IVY, bevel=0.25)                   # hedge (along X)
        p.box((side * 1.6, 0, 0), (0.6, 0.6, 2.2), STONE, bevel=0.05)                 # pillar
        p.box((side * 1.6, 0, 2.2), (0.75, 0.75, 0.18), TRIM, bevel=0.04)             # cap
    if open_:
        for side in (-1, 1):
            p.box((side * 1.4, -0.75, 0.1), (0.08, 1.4, 1.6), WINDOW)
            p.box((side * 1.4, -0.75, 1.55), (0.12, 1.45, 0.1), GOLD)
    else:
        p.box((0, 0, 0.1), (2.6, 0.08, 1.6), WINDOW)
        p.box((0, 0, 1.55), (2.65, 0.12, 0.1), GOLD)
        p.box((0, -0.06, 0.6), (0.3, 0.06, 0.3), GOLD)                               # lock
    return p.build()


def export(ob, sub=""):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    os.makedirs(os.path.join(OUT, sub), exist_ok=True)
    path = os.path.join(OUT, sub, ob.name + ".fbx")
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
    scene.render.resolution_x, scene.render.resolution_y = 2000, 500
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("w"); world.color = (0.85, 0.9, 0.92); scene.world = world
    x = -10.0
    for ob in objs:
        ob.location = (x, 0, 0)
        x += max(ob.dimensions.x, 6.0) + 3.0
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 190
    cam.location = (60, -120, 70)
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
    objs = [hall("hall_s", 10, 8, 6, BRICK, ROOFRED), hall("hall_m", 14, 10, 8, BRICK, ROOFRED), hall("hall_l", 18, 13, 9, CREAM, SLATE, amphitheatre),
            lab(), library(), canteen(), dorm(), admin(), plot(), scaffold(), bus_stop()]
    total = 0
    for ob in objs:
        ob.data.materials.append(mat)
        total += export(ob)
    merge = [tree_oak(), tree_maple(), lamp(), bench(), bike_rack(), gate(True), gate(False)]
    for ob in merge:
        ob.data.materials.append(mat)
        tris = export(ob, "merge")
        assert tris <= 600, ob.name + " over the 600-tri prop budget"
        total += tris
    print("KIT_DONE props", len(objs) + len(merge), "tris", total)
    if SHEET:
        sheet(objs + merge)


main()
