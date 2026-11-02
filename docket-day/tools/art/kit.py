"""Docket Day — Blender kit (fleet: buildings / furniture = Blender, characters = Meshy). Builds low-poly office and
courthouse props whose UVs point at swatches of the game palette (unity/Assets/Resources/materials/palette.png, order =
DocketDay.World.Pal), so the whole street shares ONE material. Bevelled boxes + cylinders, 1 unit = 1 m, pivot
bottom-centre, front = -Y in Blender (Unity -Z after the FBX axis swap). No text, crests or real court symbols.
Exports FBX into unity/Assets/Resources/kit/ and a contact sheet.
  blender -b --python docket-day/tools/art/kit.py -- <unity dir> [<sheet.png>]
Budget (Low draws: one material, one mesh per prop): furniture ≤ 400 tris, street / tower pieces ≤ 600, the courthouse
≤ 1500, the skyline backdrop ≤ 600. The game merges the static ones into the street / tower / rooms meshes (0 extra draws).
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

# swatches (DocketDay.World.Pal)
GRASS, ROAD, CONCRETE, CREAM, BRICK, BLUEWALL, ROOFRED, ROOFGREY = 0, 2, 4, 5, 6, 7, 8, 9
BRAND, WOOD, PAPER, TEAL, STONE, WINDOW, CARPET, TREE, TRUNK = 10, 11, 12, 13, 14, 15, 16, 17, 18
DARK, FOLDER, METAL, LAMP, WARN, AMBER, ROBE, GOLD, CLASSIC, SAND, WHITE = 20, 21, 22, 23, 24, 25, 26, 27, 29, 31, 39


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

    def prism(self, center, size, sw, gable=None):
        """Gable roof: a triangular prism along X (ridge on top), bottom-centre at center. gable = swatch of the two ends."""
        cx, cy, cz = center
        sx, sy, sz = size
        x0, x1, y0, y1 = cx - sx / 2, cx + sx / 2, cy - sy / 2, cy + sy / 2
        v = [self.bm.verts.new(c) for c in ((x0, y0, cz), (x0, y1, cz), (x0, cy, cz + sz), (x1, y0, cz), (x1, y1, cz), (x1, cy, cz + sz))]
        slopes = [self.bm.faces.new((v[0], v[3], v[5], v[2])), self.bm.faces.new((v[4], v[1], v[2], v[5]))]
        ends = [self.bm.faces.new((v[0], v[2], v[1])), self.bm.faces.new((v[3], v[4], v[5]))]
        for f in slopes + ends:
            f.normal_update()
        self._paint(slopes, sw)
        self._paint(ends, sw if gable is None else gable)

    def blob(self, center, radius, sw, squash=1.0):
        """Faceted low-poly crown (icosphere, 1 subdivision = 20 tris)."""
        import mathutils
        before = set(self.bm.faces)
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=1, radius=radius)
        bmesh.ops.scale(self.bm, verts=res["verts"], vec=(1.0, 1.0, squash))
        bmesh.ops.translate(self.bm, verts=res["verts"], vec=center)
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


def desk():
    p = Prop("desk")
    p.box((0, 0, 0.72), (2.4, 1.2, 0.08), WOOD, bevel=0.02)                       # top
    for x in (-1.1, 1.1):
        p.box((x, 0, 0), (0.08, 1.1, 0.72), WOOD)                                    # side panels
    p.box((0.75, 0.05, 0.0), (0.7, 1.0, 0.68), WOOD, bevel=0.02)                    # drawer block
    p.box((-0.2, 0.3, 0.8), (0.9, 0.06, 0.55), DARK, bevel=0.02)                    # monitor
    p.box((-0.2, 0.36, 0.8), (0.12, 0.12, 0.08), DARK)
    p.box((0.75, -0.15, 0.8), (0.45, 0.35, 0.1), FOLDER)                            # file stack
    p.box((0.75, -0.15, 0.9), (0.42, 0.32, 0.06), PAPER)
    p.cyl((-0.85, -0.25, 0.86), 0.08, 0.12, PAPER, segs=8)                           # coffee cup
    p.box((0, -0.95, 0.0), (0.7, 0.7, 0.48), BRAND, bevel=0.05)                      # chair seat (front, -Y; the world turns the desk 180°)
    p.box((0, -1.25, 0.48), (0.7, 0.12, 0.7), BRAND, bevel=0.04)                     # chair back
    return p.build()


def reception():
    p = Prop("reception")
    p.box((0, 0, 0), (4.2, 1.1, 1.05), BRAND, top=WOOD, bevel=0.05)
    p.box((0, -0.58, 0.2), (3.8, 0.04, 0.5), GOLD)                                    # brass kick strip
    p.box((-1.3, 0.1, 1.05), (0.12, 0.12, 0.5), DARK)                                 # lamp stand
    p.cyl((-1.3, 0.1, 1.6), 0.22, 0.2, LAMP, segs=10)                                 # green banker's lamp shade
    p.box((0.9, 0.25, 1.05), (0.85, 0.06, 0.5), DARK)                                 # screen
    p.box((0.2, -0.2, 1.05), (0.5, 0.35, 0.05), PAPER)                                # sign-in sheet
    return p.build()


def bookshelf():
    p = Prop("bookshelf")
    p.box((0, 0, 0), (3.0, 0.8, 3.4), WOOD, bevel=0.03)
    cols = (CARPET, CLASSIC, BRAND, FOLDER, TEAL)
    for r in range(3):
        x = -1.25
        i = r
        while x < 1.2:
            w = 0.16 + 0.06 * ((i * 7) % 3)
            h = 0.6 + 0.08 * ((i * 5) % 3)
            p.box((x + w / 2, -0.38, 0.25 + r * 1.05), (w, 0.1, h), cols[i % len(cols)])
            x += w + 0.03
            i += 1
    return p.build()


def coffee():
    p = Prop("coffee")
    p.box((0, 0, 0), (2.0, 0.9, 0.95), WOOD, top=CONCRETE, bevel=0.04)
    p.box((-0.45, 0.05, 0.95), (0.7, 0.55, 0.85), METAL, top=DARK, bevel=0.05)        # the machine
    p.box((-0.45, -0.24, 1.15), (0.3, 0.06, 0.12), DARK)
    for i in range(3):
        p.cyl((0.35 + i * 0.22, -0.1, 1.01), 0.07, 0.12, PAPER, segs=8)               # cups
    return p.build()


def cabinet():
    p = Prop("cabinet")
    for i in (-1, 0, 1):
        p.box((i * 0.95, 0, 0), (0.85, 0.9, 2.2), METAL, bevel=0.03)
        for d in range(4):
            p.box((i * 0.95, -0.47, 0.25 + d * 0.5), (0.3, 0.04, 0.06), DARK)        # handles
    p.box((0.95, -0.2, 2.2), (0.5, 0.4, 0.25), FOLDER)
    return p.build()


def conference():
    p = Prop("conference")
    p.box((0, 0, 0.72), (4.6, 2.0, 0.1), WOOD, bevel=0.04)
    p.box((0, 0, 0), (0.6, 1.2, 0.72), WOOD)
    for x in (-1.4, 0.0, 1.4):
        for y in (-1.4, 1.4):
            p.box((x, y, 0), (0.6, 0.6, 0.45), DARK, bevel=0.04)
            p.box((x, y + (0.28 if y > 0 else -0.28), 0.45), (0.6, 0.1, 0.6), DARK)
    p.box((-0.8, 0.2, 0.82), (0.5, 0.35, 0.04), PAPER)
    p.box((0.9, -0.3, 0.82), (0.45, 0.3, 0.06), FOLDER)
    return p.build()


def boxes():
    p = Prop("boxes")
    p.box((0, 0, 0), (1.2, 0.9, 0.8), FOLDER, bevel=0.03, rot=8)
    p.box((1.15, 0.2, 0), (1.0, 0.9, 0.8), SAND, bevel=0.03, rot=-6)
    p.box((0.5, 0.1, 0.8), (1.0, 0.8, 0.7), FOLDER, bevel=0.03, rot=3)
    p.box((0.5, -0.32, 1.05), (0.4, 0.04, 0.25), PAPER)                                # label card (blank)
    return p.build()


def board():
    p = Prop("board")
    p.box((0, 0, 0.9), (3.2, 0.12, 2.0), WHITE, bevel=0.03)
    p.box((0, 0.06, 0.9), (3.3, 0.14, 0.08), METAL)
    for x in (-1.4, 1.4):
        p.box((x, 0, 0), (0.08, 0.3, 0.9), METAL)
    p.box((-0.8, -0.08, 1.6), (0.7, 0.02, 0.5), FOLDER)
    p.box((0.2, -0.08, 1.9), (0.6, 0.02, 0.45), WARN)
    p.box((0.9, -0.08, 1.3), (0.6, 0.02, 0.45), TEAL)
    return p.build()


def courthouse():
    p = Prop("courthouse")
    # stepped plinth
    for i, (d, h) in enumerate(((4.0, 0.4), (3.2, 0.8), (2.4, 1.2))):
        p.box((0, -1.0 + i * 0.4, 0), (26.0 - i * 0.8, d, h), STONE)
    p.box((0, 3.5, 0), (24.0, 3.0, 2.0), STONE)
    p.box((0, 9.0, 2.0), (24.0, 12.0, 11.0), STONE, top=ROOFGREY, bevel=0.1)       # the hall
    for i in range(6):                                                              # columns
        x = -10.0 + i * 4.0
        p.cyl((x, 3.6, 7.0), 0.65, 10.0, WHITE, segs=12)
        p.box((x, 3.6, 2.0), (1.6, 1.6, 0.4), STONE)
        p.box((x, 3.6, 11.6), (1.6, 1.6, 0.4), STONE)
    p.box((0, 4.0, 12.0), (25.0, 3.2, 1.4), STONE, bevel=0.05)                     # architrave
    # pediment: a low triangle prism made of stacked slabs (no crest, no inscription)
    for k in range(5):
        w = 22.0 - k * 4.2
        p.box((0, 4.3, 13.4 + k * 0.5), (w, 2.6, 0.5), STONE)
    p.box((0, 2.9, 2.0), (3.2, 0.2, 5.0), WOOD)                                     # the door
    for x in (-7.0, 7.0):
        p.box((x, 2.95, 4.0), (2.4, 0.1, 3.0), WINDOW)                              # tall windows behind the columns
    for x in (-13.5, 13.5):                                                        # lamps by the steps
        p.cyl((x, -1.6, 1.2), 0.12, 2.4, DARK, segs=6)
        p.cyl((x, -1.6, 2.55), 0.3, 0.4, AMBER, segs=8)
    return p.build()


def lamp():
    p = Prop("lamp")
    p.cyl((0, 0, 2.1), 0.12, 4.2, DARK, segs=8)
    p.box((0, 0, 0), (0.5, 0.5, 0.3), DARK)
    p.cyl((0, 0, 4.4), 0.35, 0.5, AMBER, segs=8)
    p.box((0, 0, 4.65), (0.7, 0.7, 0.12), DARK)
    return p.build()


def bench():
    p = Prop("bench")
    for x in (-0.9, 0.9):
        p.box((x, 0, 0), (0.12, 0.6, 0.45), DARK)
    p.box((0, 0, 0.45), (2.2, 0.6, 0.08), WOOD)
    p.box((0, 0.3, 0.55), (2.2, 0.08, 0.5), WOOD)
    return p.build()


# ---------------------------------------------------------------- polish pass (2026-11-02): street, tower floors, roof

def window(p, x, y, z, w=1.3, h=1.7, frame=WHITE, sill=CONCRETE, mullion=True):
    """A framed window on a -Y facade at plane y (pane slightly proud of the wall, frame + sill)."""
    p.box((x, y - 0.06, z), (w, 0.12, h), WINDOW)
    p.box((x, y - 0.14, z + h - 0.12), (w + 0.24, 0.16, 0.16), frame)              # head
    p.box((x, y - 0.18, z - 0.14), (w + 0.36, 0.3, 0.14), sill)                     # sill
    if mullion:
        p.box((x, y - 0.13, z), (0.1, 0.06, h - 0.1), frame)                        # mullion


def townhouse(name, floors, wall, roof, gabled, door_x=0.0):
    """Harbour row house, 8.5 m wide × 10 m deep, front (-Y) facade with framed windows, door, cornice and roof."""
    p = Prop(name)
    w, d, fh = 8.5, 10.0, 3.2
    h = floors * fh + 0.6
    p.box((0, 0, 0), (w, d, h), wall, top=ROOFGREY, bevel=0.08)
    p.box((0, -d / 2 - 0.05, 0), (w + 0.1, 0.3, 0.6), CONCRETE)                       # plinth
    for f in range(floors):
        z = 0.6 + f * fh + 0.7
        xs = (-2.7, 0.0, 2.7)
        for x in xs:
            if f == 0 and abs(x - door_x) < 0.1:
                continue
            window(p, x, -d / 2, z, w=1.3, h=1.6, mullion=floors < 4)
        if f > 0:
            p.box((0, -d / 2 - 0.08, 0.6 + f * fh - 0.1), (w + 0.05, 0.16, 0.18), CREAM if wall != CREAM else SAND)   # floor band
    p.box((door_x, -d / 2 - 0.04, 0.6), (1.3, 0.12, 2.3), BRAND if wall != BLUEWALL else WOOD)                     # door
    p.box((door_x, -d / 2 - 0.5, 0), (1.9, 1.0, 0.3), CONCRETE)                                                     # step
    p.box((door_x, -d / 2 - 0.55, 2.95), (2.0, 1.1, 0.12), roof)                                                    # door canopy
    if gabled:
        p.prism((0, 0, h), (w + 0.5, d + 0.6, 2.6), roof, gable=wall)
        p.box((2.4, 1.5, h + 0.6), (0.8, 0.8, 2.4), BRICK, top=DARK)                                                # chimney
    else:
        p.box((0, 0, h), (w + 0.5, d + 0.5, 0.45), roof, bevel=0.05)                                                # cornice
        p.box((-2.0, 1.8, h + 0.45), (1.4, 1.2, 0.9), METAL, top=DARK, bevel=0.04)                                 # roof hatch / AC
    return p.build()


def townhouse_a():
    return townhouse("townhouse_a", 3, CREAM, ROOFRED, True)


def townhouse_b():
    return townhouse("townhouse_b", 2, BRICK, ROOFGREY, True, door_x=-2.7)


def townhouse_c():
    return townhouse("townhouse_c", 4, BLUEWALL, ROOFGREY, False, door_x=2.7)


def tree():
    p = Prop("tree")
    p.cyl((0, 0, 1.1), 0.22, 2.2, TRUNK, segs=6)
    p.blob((0, 0, 2.9), 1.5, TREE, squash=0.85)
    p.blob((0.7, 0.3, 3.9), 1.0, TREE, squash=0.9)
    p.blob((-0.5, -0.4, 3.7), 0.9, GRASS, squash=0.9)
    return p.build()


def planter():
    p = Prop("planter")
    p.box((0, 0, 0), (1.6, 0.8, 0.6), CONCRETE, top=TRUNK, bevel=0.05)
    p.blob((-0.35, 0, 0.85), 0.45, TREE, squash=0.8)
    p.blob((0.35, 0.05, 0.8), 0.4, GRASS, squash=0.8)
    return p.build()


def hydrant():
    p = Prop("hydrant")
    p.cyl((0, 0, 0.35), 0.18, 0.7, WARN, segs=8)
    p.cyl((0, 0, 0.75), 0.13, 0.1, WARN, segs=8)
    p.cyl((0, 0, 0.45), 0.07, 0.5, WARN, segs=6, axis="X")
    p.box((0, 0, 0), (0.5, 0.5, 0.06), CONCRETE)
    return p.build()


def skyline():
    """Distant backdrop: a strip of plain tower silhouettes (no windows, no landmarks), one mesh."""
    p = Prop("skyline")
    import random
    rnd = random.Random(11)
    x = -70.0
    while x < 80.0:
        w = rnd.uniform(6, 11)
        h = rnd.uniform(7, 17)
        p.box((x + w / 2, rnd.uniform(-2, 2), 0), (w, 6, h), rnd.choice((BLUEWALL, ROOFGREY, STONE)), top=ROOFGREY)
        x += w + rnd.uniform(0.5, 3)
    return p.build()


def floor_shell():
    """One open-front office floor of the tower (28 m × 14.5 m × 6 m, pivot = bottom centre, open side = -Y): slab with a
    concrete edge, back wall with three framed windows, side walls with front pilasters, a trimmed cut-away lintel and a
    baseboard. CREAM = the office wall (the game remaps it to the office style), WOOD = trim (gold on Art Deco), CARPET =
    the floor (wood on the ground floor)."""
    p = Prop("floor_shell")
    w, d, h = 28.0, 14.5, 6.0
    p.box((0, 0, 0), (w, d, 0.4), CONCRETE, top=CARPET, bevel=0.06)                  # slab
    p.box((0, d / 2, 0.4), (w, 0.6, h - 0.4), CREAM)                                  # back wall
    for x in (-w / 2, w / 2):
        p.box((x, 0, 0.4), (0.6, d, h - 0.4), CREAM)                                  # side walls
        p.box((x, -d / 2 + 0.2, 0.4), (1.0, 0.8, h - 0.4), WOOD, bevel=0.06)         # front pilasters
    p.box((0, -d / 2 + 0.2, h - 0.7), (w + 0.4, 0.6, 0.7), WOOD, bevel=0.05)        # lintel (the cut-away edge)
    p.box((0, d / 2 - 0.36, 0.4), (w - 0.6, 0.1, 0.3), WOOD)                         # baseboard
    for x in (-8.5, 0.0, 8.5):                                                         # tall back windows (open side faces the camera)
        p.box((x, d / 2 - 0.34, 1.6), (5.2, 0.08, 2.8), WINDOW)
        p.box((x, d / 2 - 0.4, 4.3), (5.6, 0.14, 0.18), WOOD)                        # head
        p.box((x, d / 2 - 0.45, 1.42), (5.7, 0.3, 0.16), WOOD)                       # sill
        for mx in (-1.3, 1.3):
            p.box((x + mx, d / 2 - 0.4, 1.6), (0.1, 0.06, 2.8), WHITE)               # mullions
    return p.build()


def office_decor():
    """Per-floor clutter merged into the tower mesh (Medium/High), floor = slab top: two potted plants, a water cooler, a
    wall clock. Positions avoid the desk rows and the room props (RegionWorld.BuildRooms)."""
    p = Prop("office_decor")
    for x in (-13.0, 13.0):                                                            # front corners, clear of desks + rooms
        p.cyl((x, -5.6, 0.35), 0.35, 0.7, TRUNK, segs=8)
        p.blob((x, -5.6, 1.25), 0.6, TREE, squash=1.1)
    p.box((9.5, 6.2, 0), (0.6, 0.6, 1.1), WHITE, bevel=0.04)                         # cooler (back wall, between rooms)
    p.cyl((9.5, 6.2, 1.35), 0.24, 0.5, TEAL, segs=8)
    p.cyl((-4.3, 6.95, 4.6), 0.45, 0.08, WHITE, segs=12, axis="Y")                    # clock (no numbers)
    p.cyl((-4.3, 6.92, 4.6), 0.5, 0.06, DARK, segs=12, axis="Y")
    return p.build()


def roof_top():
    """The tower roof: slab with a parapet, two AC units, a water tank and the stair hut (28 m × 14.5 m)."""
    p = Prop("roof_top")
    w, d = 28.0, 14.5
    p.box((0, 0, 0), (w + 1.0, d + 1.0, 0.6), ROOFGREY, bevel=0.06)
    for y in (-(d + 1.0) / 2 + 0.2, (d + 1.0) / 2 - 0.2):
        p.box((0, y, 0.6), (w + 1.0, 0.4, 0.7), CONCRETE)
    for x in (-(w + 1.0) / 2 + 0.2, (w + 1.0) / 2 - 0.2):
        p.box((x, 0, 0.6), (0.4, d + 1.0, 0.7), CONCRETE)
    for x in (6.0, 9.0):
        p.box((x, 3.0, 0.6), (2.2, 1.6, 1.2), METAL, top=DARK, bevel=0.06)          # AC units
    p.cyl((-9.0, 3.5, 2.1), 1.3, 2.4, WOOD, segs=10)                                  # water tank
    for x in (-9.8, -8.2):
        p.box((x, 3.5, 0.6), (0.25, 0.25, 0.9), DARK)
    p.box((11.0, -3.0, 0.6), (3.0, 3.0, 2.6), CREAM, top=ROOFGREY, bevel=0.05)       # stair hut
    p.box((11.0, -4.55, 0.6), (1.1, 0.1, 2.0), WOOD)
    return p.build()


def entrance():
    """The Garage Front: rolled-up shutter box tucked under the lintel, a wide step, one planter and a doormat (pivot =
    the front step centre; kept low and clear of the office cat on the right, so it never hides the reception desk)."""
    p = Prop("entrance")
    p.box((0, 0, 0), (4.4, 2.2, 0.25), CONCRETE, bevel=0.04)                          # step
    p.box((0, 0.2, 0.25), (2.4, 1.2, 0.03), BRAND)                                     # doormat
    p.box((-2.9, 0.1, 0), (1.0, 1.0, 0.7), CONCRETE, top=TRUNK, bevel=0.05)           # planter (left; the cat sits right)
    p.blob((-2.9, 0.1, 1.05), 0.55, TREE, squash=0.85)
    p.box((0, 1.15, 5.0), (6.0, 0.3, 0.4), METAL, bevel=0.04)                          # shutter box
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
    scene.render.resolution_x, scene.render.resolution_y = 2400, 600
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("w"); world.color = (0.85, 0.9, 0.92); scene.world = world
    x = -20.0
    for ob in objs:
        if ob.name == "skyline":
            ob.location = (0, 120, 0)
            continue
        ob.location = (x, 0, 0)
        x += {"courthouse": 28.0, "skyline": 0.0, "floor_shell": 32.0, "roof_top": 32.0}.get(ob.name, 12.0 if ob.name.startswith("townhouse") else 5.0)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = (x + 20.0) * 1.02
    cam.location = ((x - 20.0) / 2 - 4.0, -60, 34)
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
    objs = [desk(), reception(), bookshelf(), coffee(), cabinet(), conference(), boxes(), board(), courthouse(), lamp(), bench(),
            townhouse_a(), townhouse_b(), townhouse_c(), tree(), planter(), hydrant(), skyline(), floor_shell(), office_decor(), roof_top(),
            entrance()]
    total = 0
    for ob in objs:
        ob.data.materials.append(mat)
        total += export(ob)
    print("KIT_DONE props", len(objs), "tris", total)
    if SHEET:
        sheet(objs)


main()
