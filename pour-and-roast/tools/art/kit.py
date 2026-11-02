"""Pour & Roast — Blender kit (fleet: buildings / machines / furniture = Blender, characters + organic hero props = Meshy).
Builds low-poly cafe props whose UVs point at swatches of the game palette (unity/Assets/Resources/materials/palette.png,
order = PourAndRoast.World.Pal), so the whole venue shares ONE material. Bevelled boxes + cylinders, 1 unit = 1 m, pivot
bottom-centre, front = -Y in Blender (Unity -Z after the FBX axis swap). Exports FBX into unity/Assets/Resources/kit/ and a
contact sheet. Same shapes as the procedural fallbacks in PourAndRoast.World.MeshKit (the game uses the kit when present).
  blender -b --python pour-and-roast/tools/art/kit.py -- <unity dir> [<sheet.png>]
Budget (Low draws: one material, one mesh per prop): machine ≤ 600 tris, furniture ≤ 500, cart ≤ 900. No text anywhere.
Venue shells (polish pass 2026-11-02): shell_cart / shell_corner / shell_roastery + `_low` twins, each ONE mesh that replaces
the procedural shell (0 extra draws); every dressing piece in them ≤ ~250 tris (window 204, jar shelf 220, chalkboard 160, pendant 80).
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
FLOOR, FLOORD, QUAY, QUAYD, CREAM, SAGE, BRICK, TRIM, TOP, WALNUT, BRAND, STEEL, STEELD, BLACK, COPPER = 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14
WATER, AWNING, TEAL, PLANT, PLANTD, WINDOW, COFFEE, MILK, GREENB, GOLD, SACK, PASTRY, CHALK, SEA, CUP, WHITE = 15, 16, 17, 18, 19, 20, 21, 22, 23, 25, 26, 27, 28, 43, 45, 47


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

    def box(self, center, size, sw, top=None, bevel=0.0, rot=0.0, skip=()):
        """skip: faces nobody sees ("bottom", "back" = +Y / against the back wall, "left" = -X / against the left wall)."""
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
        if skip:
            dead = [f for f in faces if ("bottom" in skip and f.normal.z < -0.9) or ("back" in skip and f.normal.y > 0.9)
                    or ("left" in skip and f.normal.x < -0.9)]
            if dead:
                bmesh.ops.delete(self.bm, geom=dead, context="FACES")
                faces = [f for f in faces if f.is_valid]
        return faces

    def quad(self, x0, y0, x1, y1, z, sw):
        """A flat floor rectangle (x0..x1, y0..y1) at height z, facing up."""
        vs = [self.bm.verts.new(c) for c in ((x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z))]
        f = self.bm.faces.new(vs)
        f.normal_update()
        self._paint([f], sw)

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


# ---------------------------------------------------------------- venue shells (polish pass 2026-11-02)
# One mesh per venue = the floor, the two cutaway walls and all the static dressing (window frames, wainscot, chalkboard,
# pendant lamps, shelves; the quay, bollards, lamp post, crates and boats for the cart). It REPLACES the procedural shell
# 1:1 (CafeWorld spawns it at the origin) -> 0 extra draws on every tier. `_low` twins drop the bevels, the small dressing
# and the hidden faces, so the Low frame stays within ~+2 % prims. Coordinates are written as Unity (X, Z) = Blender (x, y),
# the same layout numbers as CafeWorld.LayoutFor. Theme cosmetics recolour CREAM (walls) / FLOOR / FLOORD / TRIM at runtime
# (CafeWorld.ThemeRemap), so no theme variants are exported.
CAFE = dict(x0=-6.0, x1=6.0, z0=-4.5, z1=5.0, cx=0.5, cw=5.0)
HALL = dict(x0=-7.0, x1=11.0, z0=-4.5, z1=5.0, cx=0.0, cw=6.0)
CART = dict(x0=-5.0, x1=5.0, z0=-4.5, z1=4.0)
WALL_H = 3.2


def window(p, x, z, hi):
    """A framed window on the left wall (x = wall face), centred at depth z: glass, frame, cross mullion, sill."""
    b = 0.015 if hi else 0.0
    sk = () if hi else ("left", "bottom")
    p.box((x + 0.02, z, 1.2), (0.04, 1.6, 1.3), WINDOW, skip=("left",))
    for dz in (-0.84, 0.84):                                                   # side jambs
        p.box((x + 0.05, z + dz, 1.15), (0.1, 0.1, 1.4), TRIM, bevel=b, skip=sk)
    p.box((x + 0.05, z, 2.5), (0.1, 1.78, 0.1), TRIM, bevel=b, skip=sk)        # head
    p.box((x + 0.09, z, 1.08), (0.18, 1.9, 0.08), TRIM, top=TOP, bevel=b, skip=sk)   # sill
    if hi:
        p.box((x + 0.05, z, 1.2), (0.05, 0.05, 1.3), TRIM, skip=("left", "bottom"))  # mullion
        p.box((x + 0.05, z, 1.83), (0.05, 1.6, 0.05), TRIM, skip=("left",))          # transom


def wainscot(p, x0, x1, z, hi, along="x"):
    """Panelled lower wall + dado rail. along="x": back wall (face at y=z); along="y": left wall (face at x=z)."""
    n = max(1, int((x1 - x0) / 1.3))
    step = (x1 - x0) / n
    if along == "x":
        p.box(((x0 + x1) / 2, z - 0.02, 0.0), (x1 - x0, 0.04, 0.9), TRIM, skip=("back", "bottom"))
        p.box(((x0 + x1) / 2, z - 0.05, 0.88), (x1 - x0, 0.08, 0.06), WALNUT, bevel=0.012 if hi else 0.0, skip=("back", "bottom"))
        if hi:
            for i in range(n):
                p.box((x0 + step * (i + 0.5), z - 0.05, 0.16), (step - 0.3, 0.03, 0.56), WALNUT, skip=("back", "bottom"))
    else:
        p.box((z + 0.02, (x0 + x1) / 2, 0.0), (0.04, x1 - x0, 0.9), TRIM, skip=("left", "bottom"))
        p.box((z + 0.05, (x0 + x1) / 2, 0.88), (0.08, x1 - x0, 0.06), WALNUT, bevel=0.012 if hi else 0.0, skip=("left", "bottom"))
        if hi:
            for i in range(n):
                p.box((z + 0.05, x0 + step * (i + 0.5), 0.16), (0.03, step - 0.3, 0.56), WALNUT, skip=("left", "bottom"))


def chalkboard(p, x, z, w, hi):
    """Blank chalk menu board on the back wall (face at y=z) with a walnut frame and a chalk ledge (no text)."""
    p.box((x, z - 0.02, 1.5), (w, 0.04, 1.2), CHALK, skip=("back",))
    b = 0.01 if hi else 0.0
    sk = ("back",) if hi else ("back", "bottom")
    p.box((x, z - 0.05, 2.66), (w + 0.16, 0.06, 0.08), WALNUT, bevel=b, skip=sk)
    p.box((x, z - 0.05, 1.44), (w + 0.16, 0.06, 0.08), WALNUT, bevel=b, skip=sk)
    for dx in (-(w / 2 + 0.04), w / 2 + 0.04):
        p.box((x + dx, z - 0.05, 1.5), (0.08, 0.06, 1.16), WALNUT, skip=sk)
    if hi:
        p.box((x, z - 0.1, 1.42), (w * 0.8, 0.12, 0.03), WALNUT, skip=("back",))   # ledge
        for i, sw in enumerate((CREAM, AWNING, TEAL)):
            p.box((x - 0.4 + i * 0.12, z - 0.12, 1.45), (0.06, 0.02, 0.02), sw)       # chalk sticks


def pendant(p, x, z, drop=0.85):
    """A pendant lamp hanging from the ceiling line: cord, copper shade, warm bulb."""
    p.box((x, z, WALL_H - drop + 0.2), (0.02, 0.02, drop - 0.2), BLACK)
    p.cyl((x, z, WALL_H - drop + 0.12), 0.07, 0.14, COPPER, segs=8)
    import mathutils
    before = set(p.bm.faces)
    res = bmesh.ops.create_cone(p.bm, cap_ends=False, segments=10, radius1=0.24, radius2=0.07, depth=0.2)
    bmesh.ops.translate(p.bm, verts=res["verts"], vec=(x, z, WALL_H - drop))
    p._paint([f for f in p.bm.faces if f not in before], BRAND)
    p.cyl((x, z, WALL_H - drop - 0.06), 0.06, 0.06, AWNING, segs=6)


def jar_shelf(p, x, z, w, y=1.95):
    """A wall shelf on the back wall (face at y=z) with coffee jars and two small sacks."""
    p.box((x, z - 0.14, y), (w, 0.28, 0.04), WALNUT, bevel=0.01)
    for dx in (-w / 2 + 0.15, w / 2 - 0.15):
        p.box((x + dx, z - 0.05, y - 0.22), (0.04, 0.1, 0.22), TRIM, skip=("back",))
    k = int(w / 0.28)
    for i in range(k):
        sw = (COFFEE, GREENB, COPPER, CUP)[i % 4]
        if i % 3 == 2:
            p.box((x - w / 2 + 0.2 + i * 0.27, z - 0.14, y + 0.04), (0.16, 0.12, 0.22), SACK, top=COFFEE, bevel=0.02)
        else:
            p.cyl((x - w / 2 + 0.2 + i * 0.27, z - 0.14, y + 0.15), 0.06, 0.22, sw, segs=8)


def crown(p, x0, x1, z, along="x"):
    if along == "x":
        p.box(((x0 + x1) / 2, z - 0.05, WALL_H - 0.12), (x1 - x0, 0.1, 0.12), TRIM, bevel=0.015, skip=("back",))
    else:
        p.box((z + 0.05, (x0 + x1) / 2, WALL_H - 0.12), (0.1, x1 - x0, 0.12), TRIM, bevel=0.015, skip=("left",))


def cafe_room(p, L, hi):
    """Floor + the back and left walls of a cafe venue (procedural twin: CafeWorld.Build, non-cart branch)."""
    x0, x1, z0, z1 = L["x0"], L["x1"], L["z0"], L["z1"]
    p.quad(x0 - 15, z0 - 18, x1 + 15, z1 + 6, -0.02, QUAY)                     # street (deeper in front: CafeWorld's orientation check)
    p.quad(x0, z0, x1, z1, 0.0, FLOOR)
    n = int((x1 - x0) / (0.6 if hi else 1.2))
    for i in range(1, n):                                                        # floorboard seams
        xx = x0 + i * (x1 - x0) / n
        p.quad(xx - 0.012, z0, xx + 0.012, z1, 0.003, FLOORD)
    p.box(((x0 + x1) / 2, z1 + 0.1, 0.0), (x1 - x0, 0.2, WALL_H), CREAM, top=TRIM, skip=("bottom",))      # back wall
    p.box((x0 - 0.1, (z0 + z1) / 2, 0.0), (0.2, z1 - z0, WALL_H), CREAM, top=TRIM, skip=("bottom",))      # left wall
    wainscot(p, x0, x1, z1, hi, "x")
    wainscot(p, z0, z1, x0, hi, "y")
    for i in range(3):
        window(p, x0, z0 + 1.6 + i * 2.4, hi)
    chalkboard(p, x0 + 2.2, z1, 2.2, hi)
    if hi:
        crown(p, x0, x1, z1, "x")
        crown(p, z0, z1, x0, "y")
    # flat rugs (0 draws, 6 tris, also on Low): a runner where the queue stands + the door mat
    p.quad(L["cx"] - 1.25, z0 + 0.7, L["cx"] + 1.25, -0.3, 0.004, BRAND)
    p.quad(L["cx"] - 1.12, z0 + 0.83, L["cx"] + 1.12, -0.43, 0.006, SAGE)
    p.quad(x1 - 1.6, z0 + 0.1, x1 - 0.2, z0 + 1.0, 0.004, TEAL)


def shell_cafe(hi):
    L = CAFE
    p = Prop("shell_corner" + ("" if hi else "_low"))
    cafe_room(p, L, hi)
    if hi:
        for dx in (-1.6, 0.4, 2.4):
            pendant(p, L["cx"] + dx, 1.7)
        jar_shelf(p, 4.2, L["z1"], 1.6)
    return p.build()


def shell_hall(hi):
    L = HALL
    p = Prop("shell_roastery" + ("" if hi else "_low"))
    cafe_room(p, L, hi)
    # half wall to the back room (procedural twin: x 5.6, z -0.6..5.0, 2.4 high)
    p.box((5.6, 2.2, 0.0), (0.15, 5.6, 2.4), TRIM, top=WALNUT, skip=("bottom",))
    if hi:
        p.box((5.6, 2.2, 2.4), (0.24, 5.7, 0.08), WALNUT, bevel=0.015)            # cap rail
        for zz in (-0.6, 5.0 - 0.1):
            p.box((5.6, zz, 0.0), (0.22, 0.22, 2.6), WALNUT, bevel=0.02, skip=("bottom",))   # posts
        p.box((8.3, L["z1"] - 0.06, 0.0), (5.2, 0.12, 1.4), BRICK, top=TRIM, skip=("back", "bottom"))     # brick band in the back room
        for dx in (-1.6, 0.4, 2.4):
            pendant(p, L["cx"] + dx, 1.7)
        for xx in (7.4, 9.6):
            pendant(p, xx, 2.2, drop=0.7)
        # extraction duct: from the roaster chimney (8.0, 3.5) up, along to the back wall
        p.cyl((8.0, 3.5, 2.75), 0.09, 0.7, STEELD, segs=8)
        import mathutils
        before = set(p.bm.faces)
        res = bmesh.ops.create_cone(p.bm, cap_ends=True, segments=8, radius1=0.09, radius2=0.09, depth=1.4)
        bmesh.ops.rotate(p.bm, verts=res["verts"], cent=(0, 0, 0), matrix=mathutils.Matrix.Rotation(math.radians(90), 3, "X"))
        bmesh.ops.translate(p.bm, verts=res["verts"], vec=(8.0, 4.2, 3.05))
        p._paint([f for f in p.bm.faces if f not in before], STEELD)
        # a bag shelf above the burlap sacks
        jar_shelf(p, 6.9, L["z1"], 1.5, y=1.75)
    return p.build()


def shell_cart(hi):
    L = CART
    x0, x1, z0, z1 = L["x0"], L["x1"], L["z0"], L["z1"]
    p = Prop("shell_cart" + ("" if hi else "_low"))
    p.quad(x0 - 12, z0 - 12, x1 + 12, z1 + 0.85, 0.0, QUAY)                     # quay (runs on past the frame)
    for i in range(6):                                                            # paving stripes (procedural twin)
        xx = x0 + 1 + i * 1.7
        p.quad(xx - 0.75, z0 + 0.5, xx + 0.75, z0 + 1.1, 0.005, QUAYD)
    p.quad(-30, z1 + 0.85, 30, z1 + 25, -0.3, SEA)                                # harbour water (deeper behind: orientation check)
    p.box((0, z1 + 0.85, -0.6), (x1 - x0 + 24, 0.1, 0.6), QUAYD, skip=("back", "bottom"))   # quay wall face down to the water
    if hi:
        n = 11
        w = (x1 - x0 + 24) / n
        for i in range(n):                                                        # stone coping blocks
            p.box((x0 - 12 + w * (i + 0.5), z1 + 0.6, 0.0), (w - 0.04, 0.5, 0.18), QUAY if i % 2 else QUAYD, bevel=0.03, skip=("bottom",))
    else:
        p.box((0, z1 + 0.6, 0.0), (x1 - x0 + 24, 0.5, 0.18), QUAYD, skip=("bottom",))
    for sx in (-1, 1):                                                            # bollards
        if hi:
            p.cyl((sx * 4.2, z1 + 0.3, 0.3), 0.16, 0.6, BLACK, segs=12)
            p.cyl((sx * 4.2, z1 + 0.3, 0.64), 0.21, 0.08, BLACK, segs=12)
            p.cyl((sx * 4.2, z1 + 0.3, 0.12), 0.19, 0.1, STEELD, segs=12)
        else:
            p.cyl((sx * 4.2, z1 + 0.3, 0.35), 0.16, 0.7, BLACK, segs=6)
    if hi:
        # lamp post on the quay
        p.box((-4.6, 3.2, 0.0), (0.3, 0.3, 0.25), BLACK, bevel=0.03, skip=("bottom",))
        p.cyl((-4.6, 3.2, 1.6), 0.06, 2.8, BLACK, segs=8)
        p.box((-4.6, 3.2, 2.95), (0.3, 0.3, 0.36), WINDOW, skip=("bottom",))
        p.box((-4.6, 3.2, 3.31), (0.4, 0.4, 0.06), BLACK, bevel=0.02)
        p.box((-4.6, 3.2, 3.37), (0.18, 0.18, 0.1), BLACK)
        # crates + a milk churn by the cart
        p.box((4.3, 2.9, 0.0), (0.7, 0.6, 0.55), WALNUT, top=TOP, bevel=0.03, skip=("bottom",))
        p.box((3.55, 3.0, 0.0), (0.6, 0.55, 0.5), WALNUT, top=TOP, bevel=0.03, rot=12, skip=("bottom",))
        p.box((4.2, 2.9, 0.55), (0.55, 0.5, 0.45), SACK, top=COFFEE, bevel=0.04, rot=-8, skip=("bottom",))
        p.cyl((-3.9, 2.4, 0.3), 0.18, 0.6, STEEL, segs=10)
        p.cyl((-3.9, 2.4, 0.66), 0.11, 0.12, STEEL, segs=10)
        # flower boxes along the quay front
        for xx in (-4.2, 4.4):
            p.box((xx, z0 + 0.2, 0.0), (1.0, 0.35, 0.35), WALNUT, top=PLANTD, bevel=0.02, skip=("bottom",))
            for k in range(3):
                p.box((xx - 0.3 + k * 0.3, z0 + 0.2, 0.35), (0.2, 0.2, 0.18), PLANT if k % 2 else PLANTD, rot=25 * k)
        # two small boats moored in the harbour (static backdrop; no animation)
        for bx, by, sw in ((-2.6, 7.5, TEAL), (3.4, 9.5, BRAND)):
            p.box((bx, by, -0.45), (2.6, 0.9, 0.45), sw, top=WALNUT, bevel=0.06, skip=("bottom",))
            p.box((bx - 0.3, by, 0.0), (0.9, 0.6, 0.45), WHITE, top=TRIM, bevel=0.03, skip=("bottom",))
            p.box((bx + 0.6, by, 0.0), (0.05, 0.05, 1.6), TRIM, skip=("bottom",))
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
    shells = [f(hi) for f in (shell_cart, shell_cafe, shell_hall) for hi in (True, False)]
    for ob in shells:
        ob.data.materials.append(mat)
        export(ob)
        bpy.data.objects.remove(ob)          # not on the contact sheet (they sit at the venue origin)
    if SHEET:
        sheet([o for o in objs if o.name in ("cart", "counter", "espresso_3", "grinder", "fridge", "pastry_case", "roaster", "table", "rest_rack", "packing_bench")])


main()
