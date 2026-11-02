"""Bottle Route — Blender kit (fleet: buildings / machines / vehicles = Blender, characters = Meshy). Builds low-poly props
whose UVs point at swatches of the game palette (unity/Assets/Resources/materials/palette.png, order = BottleRoute.World.Pal),
so the whole town shares ONE material. Bevelled boxes + cylinders, 1 unit = 1 m, pivot bottom-centre, front = -Y in
Blender (Unity -Z after the FBX axis swap). Exports FBX into unity/Assets/Resources/kit/ and a contact sheet.
  blender -b --python bottle-route/tools/art/kit.py -- <unity dir> [<sheet.png>]
Budget (Low draws: one material, one mesh per prop): machine ≤ 400 tris, vehicle ≤ 900, building ≤ 600.

Polish pass (2026-11-02): district buildings (harbour / old town / university, two each), the stadium shell and the depot
compound. These are MERGED into the district / depot mesh at run time (RegionWorld → MeshKit.Append: 0 extra draws on
every tier), so they are written as `kit/<name>.bytes` (BRKM v1: Unity space, flat normals, palette UVs, uint16 indices,
clockwise = front) instead of FBX: the merge needs readable vertex data and a known axis (Blender x, y, z → Unity x, z, y;
front = Blender -X = the street side; the depot is authored in depot-local Unity x / z). Each has a `<name>_low` twin
(no bevels, no windows: Low tier). Wall swatch CREAM and roof swatch ROOFRED are palette-swapped per building in Unity.
"""
import math
import os
import struct
import sys

import bmesh
import bpy

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
UNITY = argv[0] if argv else os.path.join(os.path.dirname(__file__), "../../unity")
SHEET = argv[1] if len(argv) > 1 else ""
OUT = os.path.join(UNITY, "Assets/Resources/kit")
PALETTE = os.path.join(UNITY, "Assets/Resources/materials/palette.png")

# swatches (BottleRoute.World.Pal)
GRASS, ROAD, CONCRETE, CREAM, TERRA, BLUEWALL, ROOFRED, ROOFGREY = 0, 2, 4, 5, 6, 7, 8, 9
BRAND, PANEL, WHITE, TGREEN, DEPOT, WINDOW, PET, ALU, GLASS, WARN, YELLOW, SUNSET, TRUNK, TREE = 10, 11, 12, 13, 14, 20, 21, 22, 23, 24, 25, 28, 18, 17


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

    def panel(self, center, normal, u, v, sw):
        """One quad (2 tris) facing `normal` (axis tuple), half sizes u / v along the two other axes (window, door, sign)."""
        cx, cy, cz = center
        ax = [i for i in range(3) if normal[i] == 0]
        a, b = ax
        corners = []
        for du, dv in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            p = [cx, cy, cz]
            p[a] += du * u
            p[b] += dv * v
            corners.append(self.bm.verts.new(p))
        f = self.bm.faces.new(corners)
        f.normal_update()
        nx, ny, nz = normal
        if f.normal.x * nx + f.normal.y * ny + f.normal.z * nz < 0:
            bmesh.ops.reverse_faces(self.bm, faces=[f])
            f.normal_update()
        self._paint([f], sw)
        return f

    def prism(self, center, sx, sy, rise, sw_roof, sw_gable, overhang=0.4):
        """Gable roof: ridge along Y, slopes face -X (street) and +X, open bottom (6 tris)."""
        cx, cy, z = center
        hx, hy = sx / 2 + overhang, sy / 2 + overhang * 0.5
        bm = self.bm
        v = [bm.verts.new(p) for p in ((cx - hx, cy - hy, z), (cx + hx, cy - hy, z), (cx + hx, cy + hy, z), (cx - hx, cy + hy, z),
                                       (cx, cy - hy, z + rise), (cx, cy + hy, z + rise))]
        faces = [bm.faces.new((v[0], v[3], v[5], v[4])), bm.faces.new((v[1], v[4], v[5], v[2])),
                 bm.faces.new((v[0], v[4], v[1])), bm.faces.new((v[3], v[2], v[5]))]
        for f, out in zip(faces, ((-1, 0, 0), (1, 0, 0), (0, -1, 0), (0, 1, 0))):
            f.normal_update()
            if f.normal.x * out[0] + f.normal.y * out[1] + f.normal.z * out[2] < 0:
                bmesh.ops.reverse_faces(bm, faces=[f])
                f.normal_update()
        self._paint(faces[:2], sw_roof)
        self._paint(faces[2:], sw_gable)

    def drop_bottoms(self, z=0.0):
        """Remove faces lying flat on z (never seen; saves 2 tris per box)."""
        dead = [f for f in self.bm.faces if f.normal.z < -0.9 and all(abs(v.co.z - z) < 1e-4 for v in f.verts)]
        bmesh.ops.delete(self.bm, geom=dead, context="FACES_ONLY")

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


def machine(big):
    p = Prop("machine_m" if big else "machine_s")
    w, h = (2.0, 2.3) if big else (1.4, 2.0)
    p.box((0, 0, 0), (w, 1.0, h), BRAND, top=PANEL, bevel=0.06)
    p.box((0, -0.52, h * 0.30), (w * 0.72, 0.06, h * 0.30), PANEL, bevel=0.02)       # bottle door
    p.cyl((0, -0.56, h * 0.45), 0.16, 0.06, WINDOW, segs=12, axis="Y")               # the bottle hole
    p.box((0, -0.52, h * 0.78), (w * 0.5, 0.06, h * 0.14), WINDOW)                  # screen
    p.box((0, 0, h), (w + 0.25, 1.25, 0.12), PANEL)                                  # canopy
    for i, sw in enumerate((PET, ALU, GLASS)):                                       # three material stripes
        p.box(((i - 1) * w * 0.28, -0.53, h * 0.06), (w * 0.22, 0.04, 0.12), sw)
    return p.build()


def shop():
    p = Prop("shop")
    p.box((0, 0, 0), (5.5, 4.5, 3.6), CREAM, top=ROOFRED, bevel=0.05)
    p.box((0, -2.6, 2.5), (5.8, 1.4, 0.18), BRAND)                                     # awning
    p.box((-1.4, -2.4, 0), (1.6, 0.8, 1.0), PANEL)                                     # bag counter
    p.box((1.3, -2.27, 0.4), (1.8, 0.05, 1.6), WINDOW)
    p.box((-1.4, -2.4, 1.0), (1.2, 0.6, 0.6), PET)
    return p.build()


def horeca():
    p = Prop("horeca")
    p.box((0, 0, 0), (6.5, 5.5, 4.0), TERRA, top=ROOFGREY, bevel=0.05)
    p.box((0, -2.78, 1.3), (4.2, 0.05, 1.8), WINDOW)
    for x in (-2.0, 2.0):
        p.cyl((x, -4.4, 1.2), 0.07, 2.4, TRUNK, segs=6)
        p.cyl((x, -4.4, 2.45), 1.2, 0.18, SUNSET, segs=8)
        p.cyl((x, -4.4, 0.55), 0.55, 0.08, PANEL, segs=8)
    p.box((3.6, 0.8, 0), (1.2, 1.6, 1.3), GLASS, bevel=0.04)                            # glass bags crate
    return p.build()


def van():
    p = Prop("van")
    p.box((0, -1.55, 0.35), (2.0, 1.2, 1.7), WHITE, bevel=0.12)                        # cab
    p.box((0, 0.45, 0.35), (2.1, 3.0, 2.15), WHITE, bevel=0.08)                        # box
    p.box((0, -2.16, 1.25), (1.7, 0.04, 0.6), WINDOW)                                  # windscreen
    p.box((0, 0.45, 1.25), (2.14, 2.6, 0.45), BRAND)                                   # brand stripe
    for x in (-0.95, 0.95):
        for y in (-1.5, 1.3):
            p.wheel(x, y)
    return p.build()


def bike():
    p = Prop("bike")
    p.box((0, -0.7, 0.55), (1.2, 1.1, 1.0), WHITE, bevel=0.06)                         # cargo box in front
    p.box((0, -0.7, 1.05), (1.24, 1.14, 0.12), BRAND)
    p.box((0, 0.55, 0.35), (0.12, 1.4, 0.1), WINDOW)                                   # frame
    p.box((0, 0.9, 0.35), (0.1, 0.1, 0.9), WINDOW)                                     # seat post
    p.box((0, 0.9, 1.25), (0.3, 0.45, 0.08), WINDOW)
    for y in (-1.2, 1.25):
        p.cyl((0, y, 0.34), 0.34, 0.1, WINDOW, segs=12, axis="X")
    for x in (-0.55, 0.55):
        p.cyl((x, -1.0, 0.3), 0.3, 0.1, WINDOW, segs=10, axis="X")
    return p.build()


def compactor():
    p = Prop("compactor")
    p.box((0, -2.4, 0.5), (2.4, 1.6, 2.3), WHITE, bevel=0.12)
    p.box((0, 0.6, 0.5), (2.5, 4.4, 2.8), TGREEN, bevel=0.1)
    p.box((0, 3.0, 0.6), (2.3, 0.6, 2.4), PANEL, bevel=0.06)                           # rear loader
    p.box((0, -3.21, 1.7), (2.0, 0.04, 0.8), WINDOW)
    p.box((0, 0.6, 2.2), (2.54, 3.6, 0.3), PANEL)
    for x in (-1.15, 1.15):
        for y in (-2.2, 0.0, 1.6):
            p.wheel(x, y, r=0.5, w=0.35)
    return p.build()


def depot_baler():
    p = Prop("baler")
    p.box((0, 0, 0), (4.0, 3.0, 3.2), BRAND, top=ROOFGREY, bevel=0.05)
    p.box((0, -1.55, 0.2), (2.4, 0.1, 2.0), WINDOW)
    p.box((0, 0, 3.2), (1.0, 1.0, 1.6), PANEL)                                        # press cylinder housing
    p.box((2.4, 0, 0), (1.0, 2.2, 1.6), YELLOW, bevel=0.04)                            # feed hopper
    return p.build()

# ------------------------------------------------------------------ polish pass: buildings, stadium, depot (merged meshes)
# Blender X = depth (front / street side at -X), Y = frontage along the street, Z up. Exported as BRKM .bytes (see top).


def _windows(p, d, f, floors, cols, z0, fh, ww=1.1, wh=1.3, sides=(-1, 1)):
    """Window panes (one quad each) on the street (-X) and back (+X) faces."""
    for sx in sides:
        x = sx * (d / 2 + 0.05)
        for r in range(floors):
            for c in range(cols):
                y = (c - (cols - 1) / 2.0) * (f / cols)
                p.panel((x, y, z0 + r * fh), (sx, 0, 0), ww / 2, wh / 2, WINDOW)


def _door(p, d, h=2.2, w=1.4, sw=TRUNK, sides=(-1, 1)):
    for sx in sides:
        p.panel((sx * (d / 2 + 0.05), 0, h / 2), (sx, 0, 0), w / 2, h / 2, sw)


def bld_harbour_a(low=False):
    """Two-storey harbour house with a red gable roof."""
    p = Prop("bld_harbour_a" + ("_low" if low else ""))
    d, f, h = 11.0, 10.0, 6.5
    p.box((0, 0, 0), (d, f, h), CREAM, bevel=0 if low else 0.12)
    p.prism((0, 0, h), d, f, 3.4, ROOFRED, CREAM)
    if not low:
        p.box((2.4, 2.6, h + 0.6), (0.9, 0.9, 2.6), ROOFGREY)                          # chimney
        _windows(p, d, f, 2, 3, 1.9, 2.9)
        _door(p, d)
        p.box((-d / 2 - 0.6, 3.2, 0), (1.0, 1.4, 0.6), TREE)                          # planter by the door
    p.drop_bottoms()
    return p.build()


def bld_harbour_b(low=False):
    """Harbour shed / fish hall: low pitched grey roof, big door, awning, bottle crates."""
    p = Prop("bld_harbour_b" + ("_low" if low else ""))
    d, f, h = 13.0, 12.0, 6.0
    p.box((0, 0, 0), (d, f, h), CREAM, bevel=0 if low else 0.12)
    p.prism((0, 0, h), d, f, 1.9, ROOFGREY, CREAM, overhang=0.6)
    if not low:
        for sx in (-1, 1):
            p.panel((sx * (d / 2 + 0.05), 0, 2.3), (sx, 0, 0), 2.3, 2.3, PANEL)        # sliding door
            p.panel((sx * (d / 2 + 0.05), -4.0, 4.6), (sx, 0, 0), 1.1, 0.5, WINDOW)
            p.panel((sx * (d / 2 + 0.05), 4.0, 4.6), (sx, 0, 0), 1.1, 0.5, WINDOW)
        p.box((-d / 2 - 0.7, 0, 3.6), (1.4, 8.0, 0.2), SUNSET)                          # awning
        p.box((-d / 2 - 0.8, 4.6, 0), (1.2, 1.2, 1.0), GLASS, bevel=0.04)               # crates of returns
        p.box((-d / 2 - 0.8, -4.6, 0), (1.2, 1.2, 1.0), PET, bevel=0.04)
    p.drop_bottoms()
    return p.build()


def bld_old_a(low=False):
    """Old Town: tall narrow three-storey house, steep red roof, balcony."""
    p = Prop("bld_old_a" + ("_low" if low else ""))
    d, f, h = 10.0, 9.0, 10.0
    p.box((0, 0, 0), (d, f, h), CREAM, bevel=0 if low else 0.1)
    p.prism((0, 0, h), d, f, 4.2, ROOFRED, CREAM, overhang=0.35)
    if not low:
        _windows(p, d, f, 3, 2, 2.0, 3.0, ww=1.0, wh=1.5)
        _door(p, d, w=1.2)
        p.box((-d / 2 - 0.6, 0, 3.6), (1.2, 4.2, 0.25), PANEL)                          # balcony slab
        p.box((-d / 2 - 1.15, 0, 3.85), (0.1, 4.2, 0.9), WINDOW)                        # railing
    p.drop_bottoms()
    return p.build()


def bld_old_b(low=False):
    """Old Town corner block: flat roof with cornice, arcade on the street, roof terrace."""
    p = Prop("bld_old_b" + ("_low" if low else ""))
    d, f, h = 12.0, 10.0, 9.0
    p.box((0, 0, 0), (d, f, h), CREAM, bevel=0 if low else 0.1)
    p.box((0, 0, h), (d + 0.6, f + 0.6, 0.5), PANEL)                                    # cornice / roof slab
    if not low:
        for y in (-3.2, 0.0, 3.2):
            p.panel((-d / 2 - 0.05, y, 1.4), (-1, 0, 0), 1.2, 1.4, WINDOW)              # arcade
        _windows(p, d, f, 2, 3, 4.4, 2.9, ww=1.0, wh=1.4)
        p.box((2.0, -2.0, h + 0.5), (3.2, 3.2, 0.15), TRUNK)                            # terrace deck
        p.box((3.0, 2.4, h + 0.5), (1.2, 1.2, 1.1), TREE)                               # potted tree
    p.drop_bottoms()
    return p.build()


def bld_uni_a(low=False):
    """University Hill: four-storey faculty block with window bands, roof plant and entrance canopy."""
    p = Prop("bld_uni_a" + ("_low" if low else ""))
    d, f, h = 14.0, 12.0, 15.0
    p.box((0, 0, 0), (d, f, h), CREAM, bevel=0 if low else 0.14)
    p.box((0, 0, h), (d + 0.4, f + 0.4, 0.4), PANEL)
    if not low:
        for r in range(4):
            z = 2.8 + r * 3.3
            for sx in (-1, 1):
                p.panel((sx * (d / 2 + 0.05), 0, z), (sx, 0, 0), f / 2 - 1.0, 0.75, WINDOW)
                p.panel((0, sx * (f / 2 + 0.05), z), (0, sx, 0), d / 2 - 1.2, 0.75, WINDOW)
        p.box((2.5, 1.5, h + 0.4), (4.0, 3.0, 2.0), ROOFGREY)                           # roof plant
        p.box((-d / 2 - 0.9, 0, 3.0), (1.8, 4.4, 0.3), BRAND)                           # entrance canopy
    p.drop_bottoms()
    return p.build()


def bld_uni_b(low=False):
    """University Hill: library / dorm tower with vertical glazing and a crown."""
    p = Prop("bld_uni_b" + ("_low" if low else ""))
    d, f, h = 11.0, 11.0, 24.0
    p.box((0, 0, 0), (d, f, h), CREAM, bevel=0 if low else 0.14)
    p.box((0, 0, h), (d + 0.6, f + 0.6, 1.2), PANEL)
    if not low:
        for k in (-3.0, 0.0, 3.0):
            for sx in (-1, 1):
                p.panel((sx * (d / 2 + 0.05), k, h * 0.52), (sx, 0, 0), 0.8, h * 0.4, WINDOW)
                p.panel((k, sx * (f / 2 + 0.05), h * 0.52), (0, sx, 0), 0.8, h * 0.4, WINDOW)
        p.box((0, 0, h + 1.2), (4.0, 4.0, 1.8), ROOFGREY)
    p.drop_bottoms()
    return p.build()


def stadium(low=False):
    """Stadium & Fairground: an oval bowl of stands around the pitch, two roof canopies, four floodlights. 60 × 40 m."""
    p = Prop("stadium" + ("_low" if low else ""))
    bm = p.bm
    n = 12 if low else 16
    A, B, a, b, top, lip = 30.0, 20.0, 19.0, 11.5, 13.0, 1.6
    ring = lambda rx, ry, z: [bm.verts.new((rx * math.cos(2 * math.pi * i / n), ry * math.sin(2 * math.pi * i / n), z)) for i in range(n)]
    ob_, ot, it, ib = ring(A, B, 0.0), ring(A, B, top), ring(a, b, lip), ring(a, b, 0.0)
    seats, walls = [], []
    for i in range(n):
        j = (i + 1) % n
        seats.append(bm.faces.new((it[i], it[j], ot[j], ot[i])))                          # seating slope (faces up / in)
        walls.append(bm.faces.new((ob_[i], ob_[j], ot[j], ot[i])))                        # outer wall
        walls.append(bm.faces.new((ib[i], it[i], it[j], ib[j])))                          # pitch-side wall
    for f in seats + walls:
        f.normal_update()
    # orient: seats up, outer wall out, inner wall in
    for f in seats:
        if f.normal.z < 0:
            bmesh.ops.reverse_faces(bm, faces=[f])
    for k, f in enumerate(walls):
        c = f.calc_center_median()
        outward = k % 2 == 0
        dot = f.normal.x * c.x / (A * A) + f.normal.y * c.y / (B * B)
        if (dot < 0) == outward:
            bmesh.ops.reverse_faces(bm, faces=[f])
    p._paint(seats, BRAND)
    p._paint(walls, CONCRETE)
    p.box((0, 0, 0), (2 * a - 1, 2 * b - 1, 0.25), GRASS)                              # pitch
    if not low:
        p.panel((0, 0, 0.27), (0, 0, 1), 0.15, b - 1.5, WHITE)                          # halfway line
        for sy in (-1, 1):
            p.box((0, sy * (B - 2.5), top - 0.2), (30.0, 6.0, 0.4), ROOFGREY)             # canopies
        for sx in (-1, 1):
            for sy in (-1, 1):
                p.box((sx * 24.0, sy * 15.5, 0), (0.6, 0.6, 18.0), PANEL)                 # floodlight mast
                p.box((sx * 24.0, sy * 15.5, 18.0), (2.6, 0.7, 1.6), YELLOW)
    p.drop_bottoms()
    return p.build()


def depot_hall(low=False):
    """The depot compound, authored in depot-local UNITY x / z (Blender x / y): hall with intake doors to the north
    (yard side), count-line roof unit, baler house + hopper + conveyor, yard pad under the bale stacks, glass bays,
    gate office, rooftop solar. Positions match RegionWorld (bales at +18..+40 / +10..+23, glass piles at x −30..−65)."""
    p = Prop("depot_hall" + ("_low" if low else ""))
    bv = 0 if low else 0.15
    p.box((-14, -6, 0), (26, 18, 11), DEPOT, bevel=bv)                                  # main hall
    p.box((-14, -6, 11), (26.6, 18.6, 0.6), ROOFGREY)
    p.box((10, -10, 0), (10, 9, 7), BRAND, bevel=0 if low else 0.1)                     # baler house
    p.box((10, -10, 7), (10.4, 9.4, 0.4), ROOFGREY)
    p.box((-14, 6, 0), (20, 8, 0.3), CONCRETE)                                           # intake apron
    p.box((29, 16.5, 0), (27, 18, 0.1), CONCRETE)                                        # bale yard pad
    p.box((-47.5, 17.6, 0), (41, 0.6, 2.4), CONCRETE)                                    # glass bays back wall
    p.box((-34, -9, 0), (6, 6, 4), CREAM)                                                # gate office
    if not low:
        p.box((-2, -6, 11.6), (6, 4, 3), PANEL)                                          # count line roof unit
        for x in (-22.0, -14.0, -6.0):
            p.panel((x, 3.05, 3.2), (0, 1, 0), 2.6, 3.2, PANEL)                          # roller doors (yard side)
            p.panel((x, 3.05, 6.9), (0, 1, 0), 2.6, 0.25, YELLOW)                        # door header stripe
        p.panel((-14, -15.05, 8.2), (0, -1, 0), 11.0, 0.8, WINDOW)                       # south window band
        p.panel((-14, -15.05, 5.0), (0, -1, 0), 4.0, 1.2, BRAND)                         # plain sign board (no text)
        p.panel((-14, 2.4, 0.32), (0, 0, 1), 10.0, 0.3, YELLOW)                          # hazard stripe at the doors
        for k, x in enumerate((-22.0, -16.5, -11.0, -5.5)):                               # rooftop solar
            p.box((x, -6, 11.6), (4.4, 10.0, 0.25), WINDOW, rot=0)
        p.panel((10, -14.55, 3.0), (0, -1, 0), 2.6, 2.0, WINDOW)                          # baler house window
        p.box((10, -4.4, 0), (3.2, 2.4, 2.8), YELLOW, bevel=0.06)                         # feed hopper
        p.box((2.0, -8.0, 3.4), (6.4, 1.3, 0.5), PANEL)                                   # conveyor hall -> baler
        p.box((10, -10, 7.4), (2.6, 2.6, 1.2), PANEL)                                     # press housing (ram above it)
        for x in (-27.5, -37.5, -47.5, -57.5, -67.5):
            p.box((x, 14.6, 0), (0.6, 6.0, 2.4), CONCRETE)                                # bay dividers
        for k, sw in enumerate((PET, ALU, GLASS)):                                        # sorted skips by the apron
            p.box((-2.0 + k * 3.2, 8.5, 0), (2.6, 2.2, 1.4), sw, bevel=0.05)
        p.panel((-34, -12.05, 2.4), (0, -1, 0), 2.0, 0.7, WINDOW)                         # office window
    p.drop_bottoms()
    return p.build()


MERGED = [bld_harbour_a, bld_harbour_b, bld_old_a, bld_old_b, bld_uni_a, bld_uni_b, stadium, depot_hall]


def export_bytes(ob):
    """BRKM v1 for MeshKit.Append: Unity space (x, z, y), flat normals, palette UVs, clockwise-front uint16 triangles."""
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    uvl = bm.loops.layers.uv.active
    verts, idx = [], []
    for f in bm.faces:
        f.normal_update()
        n = f.normal
        base = len(verts)
        for l in f.loops:
            co, uv = l.vert.co, l[uvl].uv
            verts.append((co.x, co.z, co.y, n.x, n.z, n.y, uv.x, uv.y))
        idx += [base, base + 2, base + 1]          # the x/z/y swap mirrors: reverse to keep the outside clockwise in Unity
    bm.free()
    assert len(verts) < 65536
    path = os.path.join(OUT, ob.name + ".bytes")
    with open(path, "wb") as fh:
        fh.write(b"BRKM" + struct.pack("<HHII", 1, 0, len(verts), len(idx)))
        for v in verts:
            fh.write(struct.pack("<8f", *v))
        fh.write(struct.pack("<%dH" % len(idx), *idx))
    tris = len(idx) // 3
    print("KIT", ob.name, "tris", tris, "->", path)
    return tris



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
        x += 6.0 if ob.name not in ("compactor", "horeca", "shop") else 8.5
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 56
    cam.location = (8, -40, 22)
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
    objs = [machine(False), machine(True), shop(), horeca(), van(), bike(), compactor(), depot_baler()]
    total = 0
    for ob in objs:
        ob.data.materials.append(mat)
        total += export(ob)
    print("KIT_DONE props", len(objs), "tris", total)
    merged = []
    for build in MERGED:
        for low in (False, True):
            ob = build(low)
            ob.data.materials.append(mat)
            export_bytes(ob)
            if low:
                bpy.data.objects.remove(ob)
            else:
                merged.append(ob)
    if SHEET:
        sheet(objs)
        if len(argv) > 2:
            for ob in objs:
                ob.hide_render = True
            sheet_merged(merged, argv[2])


def sheet_merged(objs, path):
    import mathutils
    x = -90.0
    for ob in objs:
        w = 75.0 if ob.name in ("stadium", "depot_hall") else 22.0
        ob.location = (x + w / 2, 0, 0)
        x += w
    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = 2000, 500
    cam = scene.camera
    cam.data.ortho_scale = 260
    cam.location = (x / 2 - 45, -150, 120)
    cam.rotation_euler = mathutils.Euler((math.radians(52), 0, 0))
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("SHEET", path)


if __name__ == "__main__":
    main()
