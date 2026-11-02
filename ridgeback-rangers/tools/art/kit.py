"""Ridgeback Rangers — Blender kit (fleet: buildings / vehicles = Blender, creatures + hero characters = Meshy). Builds
low-poly props whose UVs point at swatches of the game palette (unity/Assets/Resources/materials/palette.png, order =
RidgebackRangers.World.Pal), so the whole island shares ONE material. No film-franchise imagery (no gate with torches,
no black-and-yellow jeeps): a timber sanctuary gate, a ranger cabin, a red barn, a white vet bay, a hatchery dome. Bevelled
boxes + cylinders + jittered icospheres, 1 unit = 1 m, pivot bottom-centre, front = -Y in Blender (Unity -Z).
  blender -b --python ridgeback-rangers/tools/art/kit.py -- <unity dir> [<sheet.png>]
Outputs (unity/Assets/Resources/kit/):
  * <building>.fbx + tram_car.fbx — the instanced props (fallback path; the tram cars move, so they stay separate objects);
  * props.bytes — the MERGE BANK (P2 polish): every static prop (buildings, trees, tree ferns, rocks, fence posts / rails,
    shade sails, water points, dig sites, the volcano, hills, lamps, kiosks) as raw triangles already in Unity space
    (x, z, y swap + reversed winding), read by World/KitBank.cs and appended into the zone / paddock meshes, so the
    whole diorama stays ONE material and Low gets 0 extra draws. "<name>_low" twins carry no more triangles than the
    procedural MeshKit shape they replace (Low must not get heavier). Format: see write_bank().
Budget: building ≤ 700 tris, tram car ≤ 500, every other prop ≤ 600. The game draws kit props at 2.2×.
"""
import math
import os
import random
import struct
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
VROCK, LAVA, LEAF, CANVAS, BONE, MOSS, LAMP, BARK, TEAL = 40, 41, 44, 45, 46, 47, 48, 49, 50
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

    def face(self, pts, sw):
        """One polygon (convex, counter-clockwise seen from its front side)."""
        vs = [self.bm.verts.new(c) for c in pts]
        f = self.bm.faces.new(vs)
        f.normal_update()
        self._paint([f], sw)
        return f

    def face2(self, pts, sw):
        """A two-sided polygon (sails, awnings, fronds seen from below)."""
        self.face(pts, sw)
        self.face(list(reversed(pts)), sw)

    def lathe(self, center, profile, segs, sw, top=True, jitter=0.0, rng=None, swatches=None, phase=0.0, squash=(1.0, 1.0)):
        """A turned shape: profile = [(radius, z), ...] bottom to top; rings of segs quads, a top cap when the last radius > 0.
        swatches = one swatch per ring band (len(profile) - 1), else sw. jitter = random radius change per vertex (ridges)."""
        cx, cy, cz = center
        rings = []
        for i, (r, z) in enumerate(profile):
            ring = []
            for k in range(segs):
                a = phase + k * 2 * math.pi / segs
                rr = r * (1.0 + (rng.uniform(-jitter, jitter) if (jitter and rng and 0 < i) else 0.0))
                ring.append(self.bm.verts.new((cx + math.cos(a) * rr * squash[0], cy + math.sin(a) * rr * squash[1], cz + z)))
            rings.append(ring)
        out = []
        for i in range(len(rings) - 1):
            band = swatches[i] if swatches else sw
            for k in range(segs):
                a0, a1 = rings[i][k], rings[i][(k + 1) % segs]
                b0, b1 = rings[i + 1][k], rings[i + 1][(k + 1) % segs]
                if (a0.co - b0.co).length < 1e-6 and (a1.co - b1.co).length < 1e-6:
                    continue
                f = self.bm.faces.new((a0, a1, b1, b0))
                f.normal_update()
                self._paint([f], band)
                out.append(f)
        if top and profile[-1][0] > 1e-6:
            f = self.bm.faces.new(rings[-1])
            f.normal_update()
            self._paint([f], swatches[-1] if swatches else sw)
            out.append(f)
        return out

    def ico(self, center, radius, sw, sub=2, scale=(1.0, 1.0, 1.0), jitter=0.0, rng=None, top=None, top_dot=0.35):
        """A jittered icosphere blob (canopies, boulders); faces looking up get the lighter top swatch."""
        before = set(self.bm.faces)
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=sub, radius=radius)
        for v in res["verts"]:
            j = 1.0 + (rng.uniform(-jitter, jitter) if (jitter and rng) else 0.0)
            v.co.x = v.co.x * scale[0] * j + center[0]
            v.co.y = v.co.y * scale[1] * j + center[1]
            v.co.z = v.co.z * scale[2] * j + center[2]
        faces = [f for f in self.bm.faces if f not in before]
        for f in faces:
            f.normal_update()
        self._paint(faces, sw)
        if top is not None:
            self._paint([f for f in faces if f.normal.z > top_dot], top)
        return faces

    def frond(self, base, yaw, length, width, droop, sw, segs=3, rib=None):
        """A V-shaped leaf blade (tree ferns): spine + two edges, bending down along its length."""
        ca, sa = math.cos(yaw), math.sin(yaw)
        prev = None
        for i in range(segs + 1):
            t = i / segs
            d = length * t
            z = base[2] + length * 0.35 * t - droop * t * t
            w = width * (1.0 - 0.75 * t) if i < segs else 0.0
            spine = (base[0] + ca * d, base[1] + sa * d, z + 0.04)
            left = (spine[0] - sa * w, spine[1] + ca * w, z - 0.06)
            right = (spine[0] + sa * w, spine[1] - ca * w, z - 0.06)
            cur = (left, spine, right)
            if prev is not None:
                if i < segs:
                    self.face([prev[0], prev[1], cur[1], cur[0]], rib if (rib is not None and i == 1) else sw)
                    self.face([prev[1], prev[2], cur[2], cur[1]], sw)
                else:
                    self.face([prev[0], prev[1], cur[1]], sw)
                    self.face([prev[1], prev[2], cur[1]], sw)
            prev = cur

    def build(self, drop_bottom=True):
        if drop_bottom:
            dead = [f for f in self.bm.faces if f.normal.z < -0.99 and all(abs(v.co.z) < 1e-3 for v in f.verts)]
            if dead:
                bmesh.ops.delete(self.bm, geom=dead, context="FACES_ONLY")
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


# ---------------------------------------------------------------------------------------------- P2 polish: merge props
# Every function returns a built object. "_low" twins: no bevels, fewest facets — never more triangles than the procedural
# MeshKit shape they replace (MeshKit.Tree 30, Fern 50, rock box 10, shade 20, water disc 10, volcano cone 42, hill 30).


def tree(low=False):
    rng = random.Random(11)
    p = Prop("tree_low" if low else "tree")
    if low:
        p.lathe((0, 0, 0), [(0.16, 0), (0.1, 2.0)], 4, TRUNK, top=False, phase=math.pi / 4)
        p.ico((0, 0, 2.75), 1.35, TREE, sub=1, scale=(1.0, 1.0, 0.85), jitter=0.12, rng=rng, top=LEAF, top_dot=0.6)
        return p.build()
    p.lathe((0, 0, 0), [(0.18, 0), (0.13, 1.5), (0.09, 2.3)], 6, TRUNK, top=False, swatches=[TRUNK, BARK])
    p.ico((0, 0, 2.95), 1.2, TREE, sub=2, scale=(1.0, 1.0, 0.85), jitter=0.1, rng=rng, top=LEAF)
    p.ico((0.62, 0.28, 2.35), 0.8, TREE, sub=1, jitter=0.12, rng=rng, top=LEAF)
    p.ico((-0.55, -0.3, 2.5), 0.85, TREE, sub=1, jitter=0.12, rng=rng, top=LEAF)
    p.ico((0.1, -0.55, 3.5), 0.7, TREE, sub=1, jitter=0.12, rng=rng, top=LEAF)
    return p.build()


def tree_tall(low=False):
    """A tall umbrella conifer (an old-growth island tree): bare trunk, tiered flat crowns."""
    rng = random.Random(23)
    p = Prop("tree_tall_low" if low else "tree_tall")
    if low:
        p.lathe((0, 0, 0), [(0.18, 0), (0.1, 3.6)], 4, BARK, top=False, phase=math.pi / 4)
        p.ico((0, 0, 3.9), 1.45, FOREST, sub=1, scale=(1.0, 1.0, 0.45), jitter=0.1, rng=rng, top=TREE, top_dot=0.6)
        return p.build()
    p.lathe((0, 0, 0), [(0.2, 0), (0.15, 2.0), (0.1, 4.2)], 6, BARK, top=False, swatches=[BARK, TRUNK])
    p.ico((0, 0, 4.2), 1.35, FOREST, sub=2, scale=(1.0, 1.0, 0.42), jitter=0.08, rng=rng, top=TREE)
    p.ico((0.15, 0.1, 3.35), 1.05, FOREST, sub=1, scale=(1.0, 1.0, 0.4), jitter=0.1, rng=rng, top=TREE)
    p.ico((-0.1, -0.1, 4.75), 0.7, FOREST, sub=1, scale=(1.0, 1.0, 0.5), jitter=0.1, rng=rng, top=TREE)
    return p.build()


def tree_fern(low=False):
    """A cycad / tree fern: banded trunk and a crown of drooping fronds (the island's prehistoric look)."""
    p = Prop("tree_fern_low" if low else "tree_fern")
    if low:
        p.lathe((0, 0, 0), [(0.2, 0), (0.14, 1.4)], 4, TRUNK, top=False, phase=math.pi / 4)
        for k in range(5):
            p.frond((0, 0, 1.4), k * 2 * math.pi / 5 + 0.3, 1.5, 0.3, 0.9, FERN, segs=1)
        return p.build()
    p.lathe((0, 0, 0), [(0.24, 0), (0.21, 0.4), (0.19, 0.8), (0.16, 1.2), (0.14, 1.55)], 7, TRUNK, swatches=[BARK, TRUNK, BARK, TRUNK])
    for k in range(8):
        p.frond((0, 0, 1.5), k * 2 * math.pi / 8 + 0.2, 1.55 if k % 2 else 1.3, 0.3, 0.95, FERN, segs=3, rib=LEAF)
    p.ico((0, 0, 1.62), 0.22, JUNGLE, sub=1)
    return p.build()


def rock(low=False):
    rng = random.Random(5)
    p = Prop("rock_low" if low else "rock")
    if low:
        p.lathe((0, 0, 0), [(0.75, 0), (0.45, 0.85)], 5, ROCK, jitter=0.15, rng=rng, swatches=[ROCK, STONE])
        return p.build()
    p.ico((0, 0, 0.35), 0.75, ROCK, sub=2, scale=(1.0, 0.85, 0.7), jitter=0.12, rng=rng, top=STONE, top_dot=0.55)
    p.ico((0.75, 0.35, 0.18), 0.42, ROCK, sub=1, jitter=0.15, rng=rng, top=STONE, top_dot=0.55)
    p.ico((-0.6, -0.4, 0.12), 0.32, ROCK, sub=1, jitter=0.15, rng=rng, top=MOSS, top_dot=0.55)
    return p.build()


def fence_post(tier):
    """1 m tall (the game scales it by the fence height): wood with a chamfered cap, steel pipe, concrete pillar."""
    p = Prop("fence%d_post" % tier)
    if tier == 1:
        p.box((0, 0, 0), (0.25, 0.25, 0.9), WOOD, bevel=0.03)
        p.lathe((0, 0, 0.9), [(0.17, 0), (0.0, 0.12)], 4, TRUNK, phase=math.pi / 4)
    elif tier == 2:
        p.lathe((0, 0, 0), [(0.11, 0), (0.1, 0.95), (0.13, 0.95), (0.13, 1.0)], 6, STEEL, swatches=[STEEL, STEEL, DARK])
    else:
        p.box((0, 0, 0), (0.3, 0.3, 1.0), STONE, top=SLATE, bevel=0.03)
        p.box((0, -0.16, 0.86), (0.12, 0.04, 0.08), LAMP)
    return p.build()


def fence_rail(tier):
    """1 m long along Y (Unity Z, the game stretches it to the bay), two rails at 0.4 and 0.85 of the height."""
    p = Prop("fence%d_rail" % tier)
    if tier == 1:
        for z in (0.4, 0.85):
            p.box((0, 0, z - 0.07), (0.09, 1.0, 0.14), WOOD, bevel=0.015)
    elif tier == 2:
        for z in (0.4, 0.85):
            p.cyl((0, 0, z), 0.05, 1.0, STEEL, segs=6, axis="Y")
    else:
        p.cyl((0, 0, 0.9), 0.06, 1.0, STEEL, segs=6, axis="Y")
        p.box((0, 0, 0.1), (0.04, 1.0, 0.72), BRAND)   # mesh panel between the pillars
    return p.build(drop_bottom=False)


def shade_sail(low=False):
    """3.6 × 2.7 m, centred on the pivot: canvas sail on leaning poles (the game puts it where MeshKit drew a box)."""
    p = Prop("shade_sail_low" if low else "shade_sail")
    if low:
        for x, y in ((-1.6, -1.1), (1.6, 1.1)):
            p.lathe((x, y, 0), [(0.08, 0), (0.06, 2.4)], 4, WOOD, top=False)
        p.face2([(-1.7, -1.2, 2.4), (1.7, -0.9, 2.15), (1.7, 1.2, 2.4), (-1.6, 1.0, 2.1)], CANVAS)
        return p.build()
    corners = [(-1.75, -1.25, 2.45), (1.8, -1.0, 2.05), (1.6, 1.3, 2.5), (-1.6, 1.1, 2.0)]
    for (x, y, z) in corners:
        p.lathe((x * 1.05, y * 1.05, 0), [(0.08, 0), (0.06, z + 0.05)], 5, WOOD)
    mid = (0.0, 0.05, 1.95)   # the sail sags in the middle
    for k in range(4):
        a, b = corners[k], corners[(k + 1) % 4]
        p.face2([a, b, mid], CANVAS if k % 2 == 0 else TEAL)
    return p.build()


def water_point(low=False):
    """An oval stone-rimmed drinking pool, 4.1 × 2.7 m, with a pump post."""
    p = Prop("water_point_low" if low else "water_point")
    sq = (1.0, 0.66)
    if low:
        p.lathe((0, 0, 0), [(2.05, 0.12), (1.6, 0.12)], 8, STONE, top=False, squash=sq)
        p.lathe((0, 0, 0), [(1.6, 0.1), (0.0, 0.1)], 8, WATER, top=False, squash=sq)
        return p.build()
    rng = random.Random(3)
    p.lathe((0, 0, 0), [(2.1, 0), (2.05, 0.18), (1.75, 0.22), (1.6, 0.12)], 12, STONE, top=False, squash=sq,
            jitter=0.03, rng=rng, swatches=[ROCK, STONE, STONE])
    p.lathe((0, 0, 0), [(1.6, 0.1), (0.0, 0.1)], 12, WATER, top=False, squash=sq)
    p.box((1.55, 0.95, 0), (0.22, 0.22, 0.75), WOOD, bevel=0.02)
    p.cyl((1.35, 0.95, 0.62), 0.04, 0.45, STEEL, segs=5, axis="X")
    return p.build()


def dig_site(low=False):
    """A fossil dig: an earth ring around the pit, rib bones showing, a tool crate, a canvas awning and a shovel."""
    p = Prop("dig_site_low" if low else "dig_site")
    if low:
        p.lathe((0, 0, 0), [(3.4, 0.0), (2.6, 0.35), (2.0, 0.12)], 6, MUD, top=False)
        p.box((2.9, -1.6, 0), (0.6, 0.6, 0.5), WOOD)
        return p.build()
    rng = random.Random(8)
    p.lathe((0, 0, 0), [(3.6, 0.0), (2.9, 0.38), (2.3, 0.3), (1.9, 0.06)], 12, MUD, top=False, jitter=0.06, rng=rng,
            swatches=[MUD, MUD, ROCK])
    p.lathe((0, 0, 0), [(1.9, 0.06), (0.0, 0.06)], 12, ROCK, top=False)
    # rib arcs + a spine (bone), half buried
    p.box((0, 0, 0.0), (0.16, 2.4, 0.16), BONE, bevel=0.03)
    for i, y in enumerate((-0.8, -0.3, 0.2, 0.7)):
        for side in (-1, 1):
            p.box((side * 0.42, y, 0.0), (0.7, 0.09, 0.09), BONE, rot=side * (12 + 4 * i))
    p.box((2.9, -1.7, 0), (0.75, 0.6, 0.55), WOOD, top=TRUNK, bevel=0.03)
    for x in (2.2, 3.4):
        p.lathe((x, 1.4, 0), [(0.05, 0), (0.05, 1.5)], 4, WOOD, top=False)
    p.face2([(1.9, 0.9, 1.45), (3.7, 0.9, 1.45), (3.7, 1.9, 1.75), (1.9, 1.9, 1.75)], CANVAS)
    p.box((-2.6, 1.5, 0), (0.06, 0.06, 1.0), WOOD, rot=20)          # shovel handle
    p.box((-2.6, 1.5, 0), (0.28, 0.04, 0.3), STEEL, rot=20)
    return p.build()


def volcano(low=False):
    """The island's volcano, 86 m across, 32 m high (the game draws it at 2.2× like everything else). Wooded foot,
    bare ash slopes, a glowing crater (the lava glow light is Medium/High only — this is just the swatch)."""
    rng = random.Random(41)
    p = Prop("volcano_low" if low else "volcano")
    if low:
        p.lathe((0, 0, 0), [(43.2, 0), (21.0, 21.0), (7.3, 31.8)], 9, VROCK, top=False, swatches=[GRASSD, VROCK])
        p.lathe((0, 0, 0), [(7.3, 31.3), (0.0, 31.3)], 9, LAVA, top=False)
        return p.build()
    prof = [(43.2, 0), (37.5, 5.5), (30.0, 12.0), (22.0, 19.0), (14.5, 25.5), (9.6, 30.2), (8.2, 31.8), (6.4, 30.6)]
    p.lathe((0, 0, 0), prof, 20, VROCK, top=False, jitter=0.07, rng=rng,
            swatches=[FOREST, GRASSD, VROCK, VROCK, VROCK, ROCK, VROCK])
    p.lathe((0, 0, 0), [(6.4, 30.6), (0.0, 30.4)], 20, LAVA, top=False)
    # two cooled lava tongues down the front face (dark rock) with a glowing seam
    for x0 in (-3.0, 4.0):
        p.face([(x0 - 1.4, -9.0, 30.0), (x0 + 1.4, -9.0, 30.0), (x0 * 1.6 + 2.2, -25.0, 17.0), (x0 * 1.6 - 2.2, -25.0, 17.0)], DARK)
        p.face([(x0 - 0.35, -9.4, 30.0), (x0 + 0.35, -9.4, 30.0), (x0 * 1.3 + 0.4, -18.0, 23.6), (x0 * 1.3 - 0.4, -18.0, 23.6)], LAVA)
    return p.build()


def hill(low=False):
    """A wooded hill, 50 m across, 15 m high (the game scales it to the old cone's radius / height)."""
    rng = random.Random(17)
    p = Prop("hill_low" if low else "hill")
    if low:
        p.lathe((0, 0, 0), [(25.0, 0), (9.0, 15.0)], 8, GRASSD, swatches=[GRASSD])
        return p.build()
    p.lathe((0, 0, 0), [(25.0, 0), (21.0, 6.0), (14.0, 11.5), (6.0, 14.6), (0.0, 15.2)], 12, GRASSD, top=False,
            jitter=0.08, rng=rng, swatches=[GRASSD, GRASSD, TREE, TREE])
    for (x, y, r) in ((-7, -6, 4.5), (6, -4, 4.0), (0, 7, 5.0)):
        p.ico((x, y, 10.0 + rng.uniform(0, 2)), r, FOREST, sub=1, scale=(1, 1, 0.6), jitter=0.1, rng=rng, top=TREE, top_dot=0.5)
    return p.build()


def lamp():
    """A path lamp (Medium/High only in the game; the glow is the swatch, no light)."""
    p = Prop("lamp")
    p.lathe((0, 0, 0), [(0.09, 0), (0.06, 0.15), (0.045, 1.9)], 6, DARK, top=False)
    p.box((0, 0, 1.85), (0.26, 0.26, 0.3), LAMP)
    p.lathe((0, 0, 2.15), [(0.22, 0), (0.0, 0.16)], 4, DARK, phase=math.pi / 4)
    return p.build()


def kiosk(kind, low=False):
    """A snack kiosk (cafe) or a gift hut: timber hut, striped awning, a counter; tables / a display stand in front."""
    name = "kiosk_%s%s" % (kind, "_low" if low else "")
    wall, roof, stripe = (CREAM, ROOFRED, SUNSET) if kind == "cafe" else (TERRA, SLATE, TEAL)
    p = Prop(name)
    if low:
        p.box((0, 0, 0), (2.6, 2.0, 2.0), wall, top=roof)
        p.box((0, -1.25, 1.75), (2.6, 0.8, 0.12), stripe)
        return p.build()
    p.box((0, 0, 0), (2.6, 2.0, 2.1), wall, bevel=0.04)
    p.box((0, 0.1, 2.1), (3.0, 2.4, 0.22), roof, bevel=0.06)
    p.box((0, -1.02, 0), (2.2, 0.12, 0.95), WOOD, top=PANEL)                           # counter
    for i in range(5):                                                                  # striped awning, tilted forward
        x0 = -1.3 + i * 0.52
        p.face2([(x0, -1.05, 2.0), (x0 + 0.52, -1.05, 2.0), (x0 + 0.52, -1.85, 1.65), (x0, -1.85, 1.65)], stripe if i % 2 == 0 else WHITE)
    if kind == "cafe":
        for x in (-1.5, 1.5):
            p.lathe((x, -2.9, 0), [(0.32, 0.72), (0.0, 0.72)], 6, WOOD, top=False)         # table top
            p.lathe((x, -2.9, 0), [(0.05, 0), (0.05, 0.72)], 4, DARK, top=False)
            p.lathe((x, -2.9, 1.7), [(0.75, 0), (0.0, 0.35)], 6, stripe)                    # parasol
            p.lathe((x, -2.9, 0.72), [(0.02, 0), (0.02, 1.0)], 4, WHITE, top=False)
    else:
        p.box((1.75, -0.6, 0), (0.6, 0.6, 1.2), WOOD, bevel=0.02)                          # display stand with eggs
        for z, sw in ((1.2, EGG), (0.85, TEAL)):
            p.ico((1.75, -0.6, z + 0.12), 0.13, sw, sub=1, scale=(1, 1, 1.3))
    return p.build()


MERGE_BUILDINGS = ("gate", "hq", "barn", "vet", "hatchery", "station", "view")


def bank_entry(ob):
    """Blender (x, y, z) right-handed -> Unity (x, z, y) left-handed: swap y/z and reverse the winding; flat normals."""
    me = ob.data
    uvl = me.uv_layers.active.data
    verts, idx = [], []
    for poly in me.polygons:
        base = len(verts)
        nx, ny, nz = poly.normal
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            u, v = uvl[li].uv
            sw = min(7, max(0, int(u * 8))) + 8 * min(7, max(0, int((1.0 - v) * 8)))
            verts.append(((co.x, co.z, co.y), (nx, nz, ny), sw))
        for k in range(1, len(poly.loop_indices) - 1):
            idx += [base, base + k + 1, base + k]   # reversed fan (the y/z swap mirrors the mesh)
    return verts, idx


def write_bank(path, objs):
    """props.bytes, little endian: b'RRKB', u16 version (1), u16 count, then per prop: u8 name length, name (ascii),
    u16 vertex count, u16 index count, vertices (3 × i16 position in 1/500 m, 3 × i8 normal × 127, u8 swatch), indices (u16)."""
    out = bytearray(b"RRKB")
    out += struct.pack("<HH", 1, len(objs))
    report = []
    for ob in objs:
        verts, idx = bank_entry(ob)
        assert len(verts) < 65536 and len(idx) < 65536, ob.name
        name = ob.name.encode("ascii")
        out += struct.pack("<B", len(name)) + name + struct.pack("<HH", len(verts), len(idx))
        for (x, y, z), (nx, ny, nz), sw in verts:
            q = [int(round(c * 500)) for c in (x, y, z)]
            assert all(-32768 <= c <= 32767 for c in q), (ob.name, q)
            out += struct.pack("<hhhbbbB", *q, *(int(round(max(-1, min(1, c)) * 127)) for c in (nx, ny, nz)), sw)
        out += struct.pack("<%dH" % len(idx), *idx)
        report.append((ob.name, len(idx) // 3, len(verts)))
    with open(path, "wb") as f:
        f.write(out)
    for name, tris, nv in report:
        print("BANK", name, "tris", tris, "verts", nv)
    print("BANK_DONE", path, len(out), "bytes", len(objs), "props")
    return report


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
    merge = [ob for ob in objs if ob.name in MERGE_BUILDINGS]
    for low in (False, True):
        merge += [tree(low), tree_tall(low), tree_fern(low), rock(low), shade_sail(low), water_point(low), dig_site(low),
                  volcano(low), hill(low), kiosk("cafe", low), kiosk("gift", low)]
    merge += [fence_post(t) for t in (1, 2, 3)] + [fence_rail(t) for t in (1, 2, 3)] + [lamp()]
    for ob in merge:
        if not ob.data.materials:
            ob.data.materials.append(mat)
    write_bank(os.path.join(OUT, "props.bytes"), merge)
    if SHEET:
        sheet([ob for ob in merge if not ob.name.endswith("_low") and ob.name not in ("volcano", "hill")])


if __name__ == "__main__" or True:
    main()
