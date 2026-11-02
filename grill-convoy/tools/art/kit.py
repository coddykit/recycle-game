"""Grill Convoy — Blender kit (fleet: buildings / vehicles = Blender, characters / food heroes = Meshy). Builds low-poly
props whose UVs point at swatches of the game palette (unity/Assets/Resources/materials/palette.png, order =
GrillConvoy.World.Pal), so the whole city shares ONE material. Bevelled boxes + cylinders, 1 unit = 1 m, pivot
bottom-centre. Trucks: the cab leads toward +Y in Blender; the serving hatch + awning are on BOTH long sides (whichever
side faces the crowd; RegionWorld.KitYaw fixes the heading in one place if the FBX axis swap flips it). No text, no logos.
Exports FBX into unity/Assets/Resources/kit/ and a contact sheet.
  blender -b --python grill-convoy/tools/art/kit.py -- <unity dir> [<sheet.png>]
Budget (Low draws: one material, one mesh per prop): vehicle ≤ 900 tris, building ≤ 600.
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

# swatches (GrillConvoy.World.Pal)
GRASS, ROAD, PAVING, CREAM, TERRA, TEAL, ROOFRED, ROOFGREY = 0, 2, 4, 5, 6, 7, 8, 9
BRAND, STEEL, TCREAM, TTEAL, COMMISSARY, WINDOW, BUN, PATTY, CHEESE, WARN, YELLOW, LETTUCE = 10, 11, 12, 13, 14, 20, 21, 22, 23, 24, 25, 26
SUNSET, WOOD, TRUNK, TREE, WHITE, LIGHTS = 28, 31, 18, 17, 39, 42
GRASSDARK, WATER, SAND, GOLD, RETRO, SHIRTA, SHIRTB, STADIUM, TRUCKCREAM = 1, 15, 16, 27, 30, 33, 34, 43, 12


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

    def poly(self, verts, faces, sw):
        """Free-form part: verts (x, y, z) + faces (index tuples, counter-clockwise seen from outside)."""
        vs = [self.bm.verts.new(v) for v in verts]
        new = []
        for f in faces:
            face = self.bm.faces.new([vs[i] for i in f])
            face.normal_update()
            new.append(face)
        self._paint(new, sw)
        return new

    def prism(self, center, size, sw, gable=None, along="X"):
        """A pitched roof: triangular prism, ridge along X (or Y), bottom-centre at center. gable = end-triangle swatch."""
        cx, cy, cz = center
        sx, sy, sz = size
        if along == "X":
            v = [(cx - sx / 2, cy - sy / 2, cz), (cx + sx / 2, cy - sy / 2, cz), (cx + sx / 2, cy + sy / 2, cz), (cx - sx / 2, cy + sy / 2, cz),
                 (cx - sx / 2, cy, cz + sz), (cx + sx / 2, cy, cz + sz)]
            slopes = [(0, 1, 5, 4), (2, 3, 4, 5)]
            ends = [(3, 0, 4), (1, 2, 5)]
        else:
            v = [(cx - sx / 2, cy - sy / 2, cz), (cx + sx / 2, cy - sy / 2, cz), (cx + sx / 2, cy + sy / 2, cz), (cx - sx / 2, cy + sy / 2, cz),
                 (cx, cy - sy / 2, cz + sz), (cx, cy + sy / 2, cz + sz)]
            slopes = [(1, 2, 5, 4), (3, 0, 4, 5)]
            ends = [(0, 1, 4), (2, 3, 5)]
        vs = [self.bm.verts.new(p) for p in v]
        sl = [self.bm.faces.new([vs[i] for i in f]) for f in slopes]
        en = [self.bm.faces.new([vs[i] for i in f]) for f in ends]
        for f in sl + en:
            f.normal_update()
        self._paint(sl, sw)
        self._paint(en, gable if gable is not None else sw)

    def build(self):
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        for p in me.polygons:
            p.use_smooth = False
        return ob


def truck(name, big):
    """A burger truck: cab at +Y, kitchen box behind, serving hatch + striped awning + counter on both sides."""
    p = Prop(name)
    L, W, H = (7.2, 2.6, 3.0) if big else (6.0, 2.4, 2.7)
    body = TTEAL if big else BRAND
    cab_y = L * 0.36
    p.box((0, cab_y, 0.45), (W * 0.95, L * 0.26, H * 0.68), TCREAM, bevel=0.14)                 # cab
    p.box((0, cab_y + L * 0.13, 0.45 + H * 0.38), (W * 0.8, 0.05, H * 0.24), WINDOW)            # windscreen
    p.box((0, -L * 0.12, 0.45), (W, L * 0.72, H), body, top=STEEL, bevel=0.1)                   # kitchen box
    p.box((0, -L * 0.12, 0.45 + H * 0.1), (W + 0.02, L * 0.7, 0.12), WHITE)                     # skirt stripe
    for s in (-1, 1):
        x = s * (W * 0.5)
        p.box((x + s * 0.03, -L * 0.12, 0.45 + H * 0.42), (0.06, L * 0.46, H * 0.36), WINDOW)   # hatch
        p.box((x + s * 0.28, -L * 0.12, 0.45 + H * 0.38), (0.55, L * 0.46, 0.08), STEEL)         # counter
        for i in range(4):                                                                       # striped awning
            p.box((x + s * 0.6, -L * 0.12 - L * 0.2 + i * L * 0.135, 0.45 + H * 0.82), (1.1, L * 0.135, 0.1), SUNSET if i % 2 == 0 else WHITE)
        p.box((x + s * 0.04, L * 0.08, 0.45 + H * 0.45), (0.05, 0.6, 0.9), YELLOW)               # menu board (blank)
    p.box((0, -L * 0.3, 0.45 + H), (0.55, 0.55, 0.7), STEEL)                                     # vent hood
    p.cyl((0, -L * 0.3, 0.45 + H + 0.85), 0.18, 0.3, STEEL, segs=8)
    p.box((0, L * 0.02, 0.45 + H), (W * 0.65, 0.12, 0.85), YELLOW, bevel=0.03)                   # roof sign (no text)
    p.cyl((0, L * 0.02, 0.45 + H + 0.42), 0.32, 0.16, BUN, segs=10, axis="Y")                    # burger badge on the sign
    for y in (cab_y - 0.2, -L * 0.32):
        for x in (-W * 0.42, W * 0.42):
            p.wheel(x, y, r=0.46 if big else 0.42)
    return p.build()


def commissary():
    p = Prop("commissary")
    p.box((0, 0, 0), (26, 16, 9), COMMISSARY, top=ROOFGREY, bevel=0.1)
    p.box((-6, -8.05, 0), (8, 0.3, 5), STEEL)                                                   # loading door
    p.box((6, -8.05, 4), (9, 0.3, 2.4), WINDOW)                                                 # windows
    p.box((0, 2, 9), (4, 4, 3), STEEL, bevel=0.05)                                              # cold-room unit
    p.box((0, -8.3, 6.6), (14, 0.4, 1.6), BRAND)                                                # sign band (no text)
    p.cyl((-8, 3, 9 + 1.2), 0.5, 2.4, STEEL, segs=8)                                            # chimney
    for x in (-10, -2):
        p.box((x, -10, 0), (3.5, 2.5, 0.6), WOOD)                                               # pallets
    return p.build()


def parasol():
    p = Prop("parasol")
    p.cyl((0, 0, 1.6), 0.07, 3.2, STEEL, segs=6)
    p.cyl((0, 0, 3.15), 1.6, 0.2, SUNSET, segs=8)
    p.cyl((0, 0, 0.5), 0.6, 0.06, WOOD, segs=8)                                                 # table
    return p.build()


def menu_board():
    p = Prop("menu_board")
    p.box((0, 0, 0), (1.2, 0.5, 0.15), WOOD)
    p.box((0, 0, 0.15), (1.0, 0.12, 1.5), WINDOW, bevel=0.03)                                   # blank chalk board
    for i in range(3):
        p.box((-0.15, -0.07, 0.5 + i * 0.35), (0.5, 0.02, 0.06), WHITE)                          # chalk lines, no letters
        p.cyl((0.3, -0.08, 0.53 + i * 0.35), 0.08, 0.04, BUN, segs=8, axis="Y")
    return p.build()


# ---------------------------------------------------------------- district pieces (polish pass 2026-11-02)
# Merged by RegionWorld into ONE district mesh on Medium / High (0 extra draws; Low keeps the procedural blocks).
# Nominal sizes (width X × depth Y × height Z, metres) are what RegionWorld scales from — keep them in sync with
# RegionWorld.KitNominal. Front = -Y in Blender (= Unity +Z after the FBX axis swap). Facades are detailed on all four
# sides where the yaw of a placement is not controlled (street blocks). No text, no logos.


def windows_ring(p, w, d, z0, floors, floor_h, sw=None, inset=0.06, per_side=1, win_w=None):
    """Window bands (or single windows) on all four sides of a w × d block."""
    sw = WINDOW if sw is None else sw
    for f in range(floors):
        z = z0 + f * floor_h + floor_h * 0.3
        h = floor_h * 0.45
        ww = (win_w or w * 0.78)
        dw = (win_w or d * 0.72)
        if per_side == 1:
            for s in (-1, 1):
                p.box((0, s * (d / 2 + inset / 2), z), (ww, inset, h), sw)
                p.box((s * (w / 2 + inset / 2), 0, z), (inset, dw, h), sw)
        else:
            step = w / per_side
            for i in range(per_side):
                x = -w / 2 + step * (i + 0.5)
                for s in (-1, 1):
                    p.box((x, s * (d / 2 + inset / 2), z), (step * 0.55, inset, h), sw)


def office_a():
    """Plaza glass tower 16 × 14 × 40: dark glass, white floor bands, rooftop plant, entrance canopy."""
    p = Prop("d_office_a")
    W, D, H = 16, 14, 40
    p.box((0, 0, 0), (W, D, H), WINDOW, top=ROOFGREY, bevel=0.25)
    for i in range(1, 10):
        p.box((0, 0, i * 4.0 - 0.3), (W + 0.3, D + 0.3, 0.6), WHITE)                        # floor bands
    p.box((0, 0, H), (W - 1.0, D - 1.0, 0.8), STEEL)                                         # roof rim
    p.box((-3, 1, H + 0.8), (5, 5, 2.4), STEEL, bevel=0.15)                                  # rooftop plant
    p.box((4, -2, H + 0.8), (3, 3, 1.6), ROOFGREY, bevel=0.1)
    p.box((0, -D / 2 - 1.4, 3.6), (7, 2.8, 0.4), BRAND)                                      # entrance canopy
    p.box((0, -D / 2 - 0.05, 0), (4, 0.1, 3.4), STEEL)                                       # doors
    p.box((0, D / 2 + 0.05, 0), (4, 0.1, 3.4), STEEL)
    return p.build()


def office_b():
    """Plaza mid-rise with a setback 16 × 14 × 32: teal base with window rows, cream top, water tank."""
    p = Prop("d_office_b")
    W, D = 16, 14
    p.box((0, 0, 0), (W, D, 20), TEAL, top=ROOFGREY, bevel=0.2)
    windows_ring(p, W, D, 2.5, 4, 4.4)
    p.box((0, 0, 0), (W + 0.4, D + 0.4, 2.6), CREAM)                                         # ground floor plinth
    p.box((0, -D / 2 - 0.25, 0), (6, 0.2, 2.4), WINDOW)                                      # lobby glass
    p.box((0, 1, 20), (11, 10, 12), CREAM, top=ROOFGREY, bevel=0.2)                          # setback
    windows_ring(p, 11, 10, 20.5, 3, 3.8, per_side=1)
    p.cyl((3, 3, 33.2), 1.4, 2.4, WOOD, segs=8)                                              # water tank
    for x in (2, 4):
        p.box((x, 3, 32), (0.25, 0.25, 0.8), STEEL)
    return p.build()


def house_a():
    """Street townhouse 12 × 10 × 10 (walls 7 + pitched roof 3): two floors of windows, chimney, door both sides."""
    p = Prop("d_house_a")
    W, D = 12, 10
    p.box((0, 0, 0), (W, D, 7), CREAM, bevel=0.15)
    p.box((0, 0, 0), (W + 0.2, D + 0.2, 0.8), TERRA)                                         # base course
    p.prism((0, 0, 7), (W + 0.8, D + 0.8, 3.0), ROOFRED, gable=CREAM)
    for f in range(2):
        for x in (-3.4, 3.4):
            for s in (-1, 1):
                p.box((x, s * (D / 2 + 0.04), 1.4 + f * 3.0), (2.0, 0.08, 1.6), WINDOW)
        for y in (-2.4, 2.4):
            for s in (-1, 1):
                p.box((s * (W / 2 + 0.04), y, 1.4 + f * 3.0), (0.08, 1.8, 1.6), WINDOW)
    for s in (-1, 1):
        p.box((0, s * (D / 2 + 0.06), 0), (1.6, 0.12, 2.6), WOOD)                           # doors
    p.box((3.5, 2, 8), (1.0, 1.0, 3.2), TERRA)                                               # chimney
    return p.build()


def shop_a():
    """Street shop 12 × 10 × 8: shopfront glass + striped awning front and back, parapet, roof AC unit."""
    p = Prop("d_shop_a")
    W, D, H = 12, 10, 8
    p.box((0, 0, 0), (W, D, H), TERRA, top=ROOFGREY, bevel=0.15)
    p.box((0, 0, H), (W + 0.3, D + 0.3, 0.6), CREAM)                                          # parapet band
    for s in (-1, 1):
        p.box((0, s * (D / 2 + 0.05), 0.4), (W * 0.8, 0.1, 2.8), WINDOW)                     # shopfront
        for i in range(4):
            p.box((-W * 0.375 + i * W * 0.25, s * (D / 2 + 0.7), 3.4), (W * 0.25, 1.4, 0.25), SUNSET if i % 2 == 0 else WHITE)
        p.box((0, s * (D / 2 + 0.05), 5.0), (W * 0.7, 0.1, 1.6), WINDOW)                     # upper windows
    for s in (-1, 1):
        p.box((s * (W / 2 + 0.05), 0, 5.0), (0.1, D * 0.6, 1.6), WINDOW)
    p.box((-2.5, 1, H + 0.6), (2.4, 2.0, 1.2), STEEL, bevel=0.1)                             # AC unit
    return p.build()


def kiosk():
    """Boardwalk beach kiosk 12 × 9 × 6: wooden hut, serving hatch, striped roof, bun badge, surfboard."""
    p = Prop("d_kiosk")
    W, D = 12, 9
    p.box((0, 0, 0), (W, D, 4.2), WOOD, bevel=0.15)
    p.box((0, -D / 2 - 0.05, 1.6), (W * 0.6, 0.1, 1.8), WINDOW)                              # hatch
    p.box((0, -D / 2 - 0.6, 1.5), (W * 0.62, 1.2, 0.2), WHITE)                               # counter
    for i in range(6):                                                                       # striped roof
        p.box((-W / 2 - 0.3 + (i + 0.5) * (W + 0.6) / 6, 0, 4.2), ((W + 0.6) / 6, D + 2.4, 0.5), SUNSET if i % 2 == 0 else WHITE)
    p.box((0, 0, 4.7), (W * 0.5, 0.4, 1.6), YELLOW)                                          # roof sign (no text)
    p.cyl((0, -0.25, 5.5), 0.55, 0.2, BUN, segs=10, axis="Y")
    p.box((W / 2 + 0.4, -1.5, 0), (0.25, 0.9, 3.2), RETRO, rot=8)                            # surfboard
    p.box((-W / 2 - 0.8, -2, 0), (0.8, 0.8, 1.0), STEEL)                                     # cooler
    return p.build()


def lifeguard():
    """Beach lifeguard tower 4 × 4 × 7: stilts, hut, red roof, ramp, flag."""
    p = Prop("d_lifeguard")
    for x in (-1.5, 1.5):
        for y in (-1.5, 1.5):
            p.box((x, y, 0), (0.3, 0.3, 3.0), WOOD)
    p.box((0, 0, 3.0), (4.4, 4.4, 0.3), WOOD)                                                # deck
    p.box((0, 0.3, 3.3), (3.2, 2.8, 2.2), WHITE, bevel=0.1)                                  # hut
    p.box((0, -1.15, 4.1), (2.4, 0.08, 0.9), WINDOW)
    p.prism((0, 0.3, 5.5), (3.8, 3.4, 1.0), WARN, gable=WHITE)
    p.box((0, -3.2, 0), (1.0, 3.0, 0.15), WOOD, rot=0)                                       # ramp (flat)
    p.box((1.9, 1.9, 3.3), (0.12, 0.12, 4.6), STEEL)                                          # flag pole
    p.box((2.5, 1.9, 7.0), (1.2, 0.05, 0.7), WARN)
    return p.build()


def stand():
    """Stadium stand segment 22 × 10 × 16: five seat tiers rising to the back, back wall, cantilever roof. Seats face -Y."""
    p = Prop("d_stand")
    W, D = 22, 10
    for i in range(5):
        y = -D / 2 + i * 1.9 + 0.95
        h = 1.6 + i * 2.4
        p.box((0, y, 0), (W, 1.9, h), STADIUM)
        p.box((0, y - 0.5, h), (W - 0.6, 0.7, 0.35), RETRO if i % 2 == 0 else WHITE)          # seat row
    p.box((0, D / 2 - 0.4, 0), (W, 0.8, 16), STADIUM, top=STEEL, bevel=0.15)                 # back wall
    p.box((0, D / 2 + 0.05, 2), (W * 0.8, 0.1, 1.4), WINDOW)                                  # concourse openings (outside)
    p.box((0, D / 2 - 3.0, 16), (W + 0.6, 6.4, 0.4), STEEL)                                   # roof over the back rows
    p.box((0, D / 2 - 6.3, 15.2), (W + 0.6, 0.3, 1.2), BRAND)                                 # roof fascia
    for x in (-W / 2 + 1, W / 2 - 1):
        p.box((x, D / 2 - 1.0, 0), (0.5, 0.5, 16), STEEL)
    return p.build()


def floodlight():
    """Stadium floodlight mast 3 × 3 × 26: lattice-free pole, light bank facing -Y."""
    p = Prop("d_floodlight")
    p.cyl((0, 0, 12), 0.4, 24, STEEL, segs=6)
    p.box((0, 0, 0), (1.6, 1.6, 0.6), ROOFGREY)
    p.box((0, -0.4, 23.5), (4.2, 0.8, 2.6), STEEL, bevel=0.1)
    for i in range(3):
        p.box((-1.4 + i * 1.4, -0.85, 24.1), (1.1, 0.1, 1.4), LIGHTS)
    return p.build()


def planter():
    """Plaza planter 10 × 4 × 5: bevelled wooden box, low-poly tree, bench on the front."""
    p = Prop("d_planter")
    p.box((0, 0, 0), (10, 4, 1.2), WOOD, bevel=0.12)
    p.box((0, 0, 1.2), (9.4, 3.4, 0.05), GRASSDARK)
    p.cyl((0, 0, 2.2), 0.3, 2.2, TRUNK, segs=6)
    p.box((0, 0, 2.8), (3.6, 3.0, 2.0), TREE, bevel=0.3)
    p.box((0.3, 0.2, 4.6), (2.2, 2.0, 1.2), TREE, bevel=0.25)
    for s in (-3.0, 3.0):
        p.box((s, 0, 1.2), (1.4, 1.4, 0.8), LETTUCE, bevel=0.15)                              # shrubs
    p.box((0, -2.8, 0.5), (6, 0.9, 0.18), WOOD)                                               # bench seat
    for x in (-2.6, 2.6):
        p.box((x, -2.8, 0), (0.25, 0.7, 0.5), STEEL)
    return p.build()


def string_lights():
    """Evening string lights 20 × 1 × 6: two poles, a sagging wire of six segments, twelve warm bulbs."""
    p = Prop("d_lights")
    L, H = 20.0, 6.0
    for x in (-L / 2, L / 2):
        p.cyl((x, 0, H / 2), 0.15, H, STEEL, segs=6)
    pts = []
    for i in range(7):
        t = i / 6.0
        x = -L / 2 + L * t
        z = H - 0.2 - 1.6 * (1 - (2 * t - 1) ** 2)
        pts.append((x, z))
    for (x0, z0), (x1, z1) in zip(pts, pts[1:]):
        ang = math.degrees(math.atan2(z1 - z0, x1 - x0))
        import mathutils
        before = set(p.bm.faces)
        res = bmesh.ops.create_cube(p.bm, size=1.0)
        for v in res["verts"]:
            v.co.x *= math.hypot(x1 - x0, z1 - z0)
            v.co.y *= 0.06
            v.co.z *= 0.06
        bmesh.ops.rotate(p.bm, verts=res["verts"], cent=(0, 0, 0), matrix=mathutils.Matrix.Rotation(math.radians(-ang), 3, "Y"))
        bmesh.ops.translate(p.bm, verts=res["verts"], vec=((x0 + x1) / 2, 0, (z0 + z1) / 2))
        p._paint([f for f in p.bm.faces if f not in before], STEEL)
    for i in range(12):
        t = (i + 0.5) / 12.0
        x = -L / 2 + L * t
        z = H - 0.2 - 1.6 * (1 - (2 * t - 1) ** 2)
        p.box((x, 0, z - 0.45), (0.32, 0.32, 0.4), LIGHTS if i % 3 else SUNSET)
    return p.build()


def warehouse():
    """Harbour shed 24 × 14 × 11: brick-red walls, three saw-tooth roof bays, big doors both sides, high windows."""
    p = Prop("d_warehouse")
    W, D, H = 24, 14, 8
    p.box((0, 0, 0), (W, D, H), TERRA, bevel=0.15)
    for i in range(3):
        p.prism((-W / 3 + i * W / 3, 0, H), (W / 3, D + 0.4, 3.0), ROOFGREY, gable=TERRA, along="Y")
    for s in (-1, 1):
        for x in (-6, 6):
            p.box((x, s * (D / 2 + 0.06), 0), (5, 0.12, 5), STEEL)                              # roller doors
        p.box((0, s * (D / 2 + 0.06), 6), (W * 0.8, 0.12, 1.0), WINDOW)                         # high windows
    for s in (-1, 1):
        p.box((s * (W / 2 + 0.06), 0, 6), (0.12, D * 0.7, 1.0), WINDOW)
    p.box((-10, -D / 2 - 2, 0), (3, 2.4, 1.6), WOOD)                                            # crates
    p.box((-6.5, -D / 2 - 2, 0), (2.2, 2.2, 2.2), BRAND)
    return p.build()


def crane():
    """Harbour gantry crane 10 × 20 × 22: four legs, portal beam, boom reaching -Y over the water, cab, hook."""
    p = Prop("d_crane")
    for x in (-4, 4):
        for y in (-3, 3):
            p.box((x, y, 0), (0.8, 0.8, 16), YELLOW)
    for y in (-3, 3):
        p.box((0, y, 15), (9.6, 0.9, 1.0), YELLOW)
    p.box((0, -1, 16), (2.2, 22, 1.4), BRAND, bevel=0.1)                                       # boom
    p.box((0, 4.5, 17.4), (3.6, 4, 2.8), WHITE, bevel=0.15)                                     # machinery house
    p.box((1.4, -2.6, 13.4), (2.0, 2.2, 2.2), TRUCKCREAM, bevel=0.1)                            # cab
    p.box((1.4, -3.75, 14.0), (1.6, 0.08, 1.0), WINDOW)
    p.box((0, -10.5, 8), (0.1, 0.1, 8), STEEL)                                                  # cable
    p.box((0, -10.5, 7.2), (1.2, 1.2, 0.8), WARN)                                               # hook block
    p.box((0, 8, 16), (2.4, 3.0, 2.0), ROOFGREY)                                                # counterweight
    return p.build()


def boat():
    """Fishing boat 4 × 12 × 6: tapered hull, cabin, mast. Bow toward -Y."""
    p = Prop("d_boat")
    v = [(-2, 5, 0.6), (2, 5, 0.6), (2, -3, 0.6), (0, -6, 0.6), (-2, -3, 0.6),          # deck outline (z = 1.6 below)
         (-1.4, 4.6, -0.6), (1.4, 4.6, -0.6), (1.4, -2.8, -0.6), (0, -5.2, -0.6), (-1.4, -2.8, -0.6)]
    v = [(x, y, z + 1.0) for x, y, z in v]
    faces = [(0, 4, 3, 2, 1), (5, 6, 7, 8, 9), (0, 1, 6, 5), (1, 2, 7, 6), (2, 3, 8, 7), (3, 4, 9, 8), (4, 0, 5, 9)]
    hull = p.poly(v, faces, TEAL)
    p._paint([hull[0]], WOOD)
    p.box((0, -1.2, 0.95), (4.08, 9.0, 0.3), WHITE)                                              # rub rail stripe
    p.box((0, 2.2, 1.6), (2.6, 2.6, 1.8), WHITE, bevel=0.1)                                       # cabin
    p.box((0, 0.88, 2.3), (2.0, 0.06, 0.7), WINDOW)
    p.box((0, 2.2, 3.4), (2.8, 2.8, 0.2), WARN)                                                    # cabin roof
    p.box((0, -1.5, 1.6), (0.18, 0.18, 5.0), STEEL)                                                # mast
    return p.build()


def street_bits():
    """Pitch-side clutter 6 × 3 × 1.6: a bin with lid, two cones, a bollard (no text)."""
    p = Prop("d_bits")
    p.cyl((-2.2, 0, 0.55), 0.45, 1.1, TEAL, segs=8)
    p.cyl((-2.2, 0, 1.15), 0.5, 0.12, STEEL, segs=8)
    for x in (0.0, 1.3):
        before = set(p.bm.faces)
        res = bmesh.ops.create_cone(p.bm, cap_ends=True, segments=6, radius1=0.35, radius2=0.05, depth=0.9)
        bmesh.ops.translate(p.bm, verts=res["verts"], vec=(x, 0.2, 0.55))
        p._paint([f for f in p.bm.faces if f not in before], WARN)
        p.box((x, 0.2, 0), (0.8, 0.8, 0.1), WARN)
    p.cyl((2.6, -0.3, 0.5), 0.18, 1.0, STEEL, segs=6)
    return p.build()


DISTRICT = [office_a, office_b, house_a, shop_a, kiosk, lifeguard, stand, floodlight, planter, string_lights, warehouse, crane,
            boat, street_bits]


def export(ob, bake=False):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    path = os.path.join(OUT, ob.name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                             object_types={"MESH"}, path_mode="STRIP", embed_textures=False, mesh_smooth_type="FACE", add_leaf_bones=False,
                             bake_space_transform=bake)
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
    """Contact sheet: two rows (vehicles + small props, then district pieces), ortho 3/4 view."""
    import mathutils
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = 2000, 900
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("w"); world.color = (0.85, 0.9, 0.92); scene.world = world
    rows = [[o for o in objs if not o.name.startswith("d_")], [o for o in objs if o.name.startswith("d_")]]
    width = 0.0
    for r, row in enumerate(rows):
        x = 0.0
        for ob in row:
            w = max(v.co.x for v in ob.data.vertices) - min(v.co.x for v in ob.data.vertices)
            ob.location = (x + w / 2, r * 34.0, 0)
            x += w + 4.0
        width = max(width, x)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = width * 1.02
    cam.location = (width / 2, -80, 70)
    cam.rotation_euler = mathutils.Euler((math.radians(55), 0, 0))
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
    objs = [truck("truck_starter", False), truck("truck_box", True), parasol(), menu_board(), commissary()]
    total = 0
    for ob in objs:
        ob.data.materials.append(mat)
        total += export(ob)
    # district pieces: vertices baked into the Y-up FBX space (RegionWorld reads them to merge into the district mesh)
    over = []
    pieces = []
    for build in DISTRICT:
        ob = build()
        ob.data.materials.append(mat)
        t = export(ob, bake=True)
        total += t
        pieces.append(ob)
        if t > 600:
            over.append((ob.name, t))
    print("KIT_DONE props", len(objs) + len(pieces), "tris", total)
    if over:
        print("KIT_OVER_BUDGET", over)
        sys.exit(1)
    if SHEET:
        sheet(objs + pieces)


main()
