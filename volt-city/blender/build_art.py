"""Volt City P2 model set (deterministic): blender -b --factory-startup -P blender/build_art.py  (blender/build.sh)

Target: Unity 6 LTS (URP, Android, portrait) — owner decision 2026-10-27. Writes, per model, into
unity/Assets/Art/Models/<group>/: <id>.fbx (Unity's preferred import), <id>.glb (same mesh, for glTFast / checks) and
<id>.json (sidecar: material, palette key -> material code / colour ids, UV channel meaning, triangles, expected Unity
bounds). Plus Models/layout.json (the city plan the game reads, Unity space) and Models/manifest.json (sha256 of every
FBX / GLB / sidecar + the layout hash: two builds must give the same manifest).

Authoring space (builders): X east, Y up, +Z south (towards the portrait camera), metres. Unity space = (x, y, -z):
X east, Y up, Z north; the camera sits south of the city looking north. Every model = one mesh, one material, vertex
colour = palette x baked AO (sRGB-encoded), alpha = material code (vckit.CODE), UV0 / UV1 = data (vckit module doc).

Groups (one Unity folder each; the importer script VcArtImport.cs covers every folder under Art/Models):
  world/    terrain_city, sky_dome, district_<id> x 8, grid_lines        (city space, placed at the origin)
  plants/   plant_<type>_l<1..3> x 18                                    (city space; the game merges the built set)
  grid/     substation, substation_smart                                 (local; merged into the grid draw)
  props/    truck_crew, car, fault_marker, tree, wind_blades             (local; GPU-instanced, one draw each)

Original designs only (DESIGN section 11): fictional city, generic plant / truck / pylon shapes, no real utility,
plant, dam, stadium or brand; no lightning-bolt-in-circle logo; no nuclear plant.
"""
import hashlib
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kit_buildings as K  # noqa: E402
import layout as L  # noqa: E402
import vckit  # noqa: E402
from vckit import Model, rng  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "unity/Assets/Art/Models"
H = L.height


def r2(v):
    return round(v, 3)


# ---------------------------------------------------------------------------------------------------- terrain
def _axis(lo, hi, dense_lo, dense_hi, step, coarse):
    """Grid lines: `step` m inside [dense_lo, dense_hi], growing by x1.35 per cell outside up to `coarse`."""
    out = []
    x = dense_lo
    while x < dense_hi - 1e-6:
        out.append(x)
        x += step
    out.append(dense_hi)
    s = step
    x = dense_hi
    while x < hi:
        s = min(coarse, s * 1.35)
        x = min(hi, x + s)
        out.append(x)
    s = step
    x = dense_lo
    left = []
    while x > lo:
        s = min(coarse, s * 1.35)
        x = max(lo, x - s)
        left.append(x)
    return sorted(left) + out


ROAD_LINES = {}


def road_strip(m, pts, half, key="asphalt", lift=.32, seg=14.0, bridge_y=2.4, name=""):
    """A road ribbon following the terrain (+ lift); over water it becomes a bridge deck with side skirts.
    UV = (lateral m, metres along) for the shader's lane markings."""
    dense = []
    for a, b in zip(pts, pts[1:]):
        n = max(1, int(math.dist(a, b) / seg))
        for i in range(n):
            t = i / n
            dense.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    dense.append(pts[-1])
    along = 0.0
    prev = None
    rows = []
    centre = ROAD_LINES.setdefault(name, []) if name else []
    for i, (x, z) in enumerate(dense):
        if prev is not None:
            along += math.dist(prev, (x, z))
        prev = (x, z)
        a = dense[max(0, i - 1)]
        b = dense[min(len(dense) - 1, i + 1)]
        dx, dz = b[0] - a[0], b[1] - a[1]
        ln = math.hypot(dx, dz) or 1.0
        nx, nz = -dz / ln, dx / ln
        yl = H(x + nx * half, z + nz * half)
        yr = H(x - nx * half, z - nz * half)
        y = max(H(x, z), yl, yr) + lift
        wet = min(H(x, z), yl, yr) < .3
        if wet:
            y = max(y, bridge_y)
        rows.append(((x + nx * half, y, z + nz * half), (x - nx * half, y, z - nz * half), along, wet))
        centre.append((r2(x), r2(y), r2(z)))
    for (l0, r0, s0, w0), (l1, r1, s1, w1) in zip(rows, rows[1:]):
        # CCW seen from above: l0, r0, r1, l1 depends on the travel direction; poly() then checks the normal
        quad = [l0, l1, r1, r0]
        uv = [(-half, s0), (-half, s1), (half, s1), (half, s0)]
        n = (Vector(quad[1]) - Vector(quad[0])).cross(Vector(quad[2]) - Vector(quad[0]))
        if n.y < 0:
            quad.reverse()
            uv.reverse()
        m.poly(quad, key, uv=uv)
        if w0 or w1:
            for e0, e1 in ((l0, l1), (r0, r1)):
                lo0 = (e0[0], -1.0, e0[2])
                lo1 = (e1[0], -1.0, e1[2])
                side = [e0, lo0, lo1, e1]
                mid = ((e0[0] + e1[0]) / 2, (e0[2] + e1[2]) / 2)
                cen = ((l0[0] + r0[0]) / 2, (l0[2] + r0[2]) / 2)
                nn = (Vector(side[1]) - Vector(side[0])).cross(Vector(side[2]) - Vector(side[0]))
                if nn.x * (mid[0] - cen[0]) + nn.z * (mid[1] - cen[1]) < 0:
                    side.reverse()
                m.poly(side, "concrete")


def terrain_city(m):
    xs = _axis(-1500, 1500, -300, 320, 16.0, 260.0)
    zs = _axis(-2200, 1300, -470, 400, 16.0, 260.0)

    farm = rng("farm")
    patch = {}

    def key(cx, cz):
        # farmland patchwork (fields ~2 x 3 grid cells) on the open plains; darker grass in the hills
        if cz < -250:
            return "grassD"
        fx, fz = int((cx + 2000) // 36), int((cz + 2000) // 52)
        open_plain = (cz > 200 and cx > 120) or (cz > 330) or (cx > 300 and cz > -200) or (cx < -250 and cz < 0)
        if not open_plain:
            return "grass"
        if (fx, fz) not in patch:
            patch[(fx, fz)] = farm.choice(["field", "wheat", "meadow", "grass", "field", "meadow"])
        return patch[(fx, fz)]
    m.smooth = True
    m.grid(0, 0, 0, 0, 0, 0, lambda x, z: H(x, z), key, xs=xs, zs=zs)
    for name, pts in L.ROADS:
        road_strip(m, pts, L.ROAD_HALF, name=name)
    # harbour quay wall + piers (they read at every zoom; the basin itself is terrain below the water)
    m.boxr(-216, -3, 100, -206, 1.4, 200, "concreteD", skip="b")
    for z in (120, 150, 180):
        m.boxr(-246, -2, z - 3, -216, 1.0, z + 3, "pier", skip="b")
    m.ao_strength = 0.0


def sky_dome(m):
    """Inward hemisphere (radius 6 km); data UV.y = height factor 0 (horizon) .. 1 (zenith) for the gradient."""
    R = 6000.0
    m.ell((40, -300, -60), (R, R * .55, R), "sky", n=16, rings=5, bottom=-.12, top=1.0, cap=False)
    bm = m.bm
    bmesh_faces = list(bm.faces)
    import bmesh
    bmesh.ops.reverse_faces(bm, faces=bmesh_faces)
    m.uvfn = lambda key, const, p: (0.0, max(0.0, min(1.0, (p.y + 300) / (R * .55))))
    m.ao_strength = 0.0


# ---------------------------------------------------------------------------------------------------- districts
STREET_W = 8.0
PAD_LIFT = .35      # block pavements above the pad (streets at +0.2): depth-safe at 2 km with near = 20 m


class District:
    def __init__(self, m, did):
        spec = next(d for d in L.DISTRICTS if d[0] == did)
        self.m = m
        self.id = did
        (self.cx, self.cz), (self.hx, self.hz), self.y, self.sub = spec[1], spec[2], spec[3], spec[4]
        self.x0, self.x1 = self.cx - self.hx, self.cx + self.hx
        self.z0, self.z1 = self.cz - self.hz, self.cz + self.hz
        self.lamps = []
        self.trees = []
        self.parks = []
        self.rnd = rng("district", did)
        sx, sz = self.sub
        self.R = max(math.hypot(x - sx, z - sz) for x in (self.x0, self.x1) for z in (self.z0, self.z1))

    def uvfn(self, key, const, p):
        sx, sz = self.sub
        u = max(0.0, min(1.0, math.hypot(p.x - sx, p.z - sz) / self.R))
        if key == "e_pool":
            s = 0.0
            for lx, lz in self.lamps:
                d2 = (p.x - lx) ** 2 + (p.z - lz) ** 2
                if d2 < 400:
                    s += math.exp(-d2 / 50.0)
            return (u, min(1.0, s))
        return (u, const[1])

    def streets(self, xs, zs):
        """Street grid: xs / zs = fractions (0..1) of the district box where streets run (N-S at xs, E-W at zs).
        Returns the blocks between them. Streets are subdivided every ~6 m so the baked lamp pools read."""
        m = self.m
        y = self.y + .2
        sx = [self.x0 + (self.x1 - self.x0) * f for f in xs]
        sz = [self.z0 + (self.z1 - self.z0) * f for f in zs]
        hw = STREET_W / 2
        # lamps first (the pool needs them): staggered every 18 m along each street
        for x in sx:
            n = int((self.z1 - self.z0) / 18)
            for i in range(n + 1):
                z = self.z0 + (self.z1 - self.z0) * i / max(1, n)
                self.lamps.append((x + (hw + .6) * (1 if i % 2 else -1), z))
        for z in sz:
            n = int((self.x1 - self.x0) / 18)
            for i in range(n + 1):
                x = self.x0 + (self.x1 - self.x0) * i / max(1, n)
                if any(abs(x - q) < hw + 2 for q in sx):
                    continue
                self.lamps.append((x, z + (hw + .6) * (1 if i % 2 else -1)))
        for x in sx:
            m.grid(x - hw, x + hw, self.z0, self.z1, 1, max(1, int((self.z1 - self.z0) / 6)), lambda a, b: y, "e_pool")
        for z in sz:
            cuts = [self.x0] + sum([[q - hw, q + hw] for q in sx], []) + [self.x1]
            for a, b in zip(cuts[0::2], cuts[1::2]):
                if b - a > .5:
                    m.grid(a, b, z - hw, z + hw, max(1, int((b - a) / 6)), 1, lambda a_, b_: y, "e_pool")
        for lx, lz in self.lamps:
            K.lamp(m, lx, lz, self.y + PAD_LIFT)
        # street trees between the lamps on the opposite kerb (instanced; layout.json)
        for x in sx:
            n = int((self.z1 - self.z0) / 14)
            for i in range(1, n):
                z = self.z0 + (self.z1 - self.z0) * i / n
                if not any(abs(z - q) < hw + 3 for q in sz):
                    self.trees.append((x + (hw + 1.6) * (-1 if i % 2 else 1), self.y + PAD_LIFT, z))
        for z in sz:
            n = int((self.x1 - self.x0) / 14)
            for i in range(1, n):
                x = self.x0 + (self.x1 - self.x0) * i / n
                if not any(abs(x - q) < hw + 3 for q in sx):
                    self.trees.append((x, self.y + PAD_LIFT, z + (hw + 1.6) * (-1 if i % 2 else 1)))
        bx = [self.x0] + sum([[q - hw, q + hw] for q in sx], []) + [self.x1]
        bz = [self.z0] + sum([[q - hw, q + hw] for q in sz], []) + [self.z1]
        blocks = []
        for i in range(0, len(bx), 2):
            for j in range(0, len(bz), 2):
                blocks.append((bx[i], bz[j], bx[i + 1], bz[j + 1]))
        return blocks

    def pavement(self, b, key="concreteL"):
        x0, z0, x1, z1 = b
        y = self.y + PAD_LIFT
        self.m.poly([(x0, y, z1), (x1, y, z1), (x1, y, z0), (x0, y, z0)], key)

    def park(self, b, n_trees=8):
        self.pavement(b, "grassD")
        self.parks.append(b)
        x0, z0, x1, z1 = b
        for _ in range(n_trees):
            self.trees.append((self.rnd.uniform(x0 + 2, x1 - 2), self.y + PAD_LIFT, self.rnd.uniform(z0 + 2, z1 - 2)))

    def lot(self, b, inset=1.6):
        x0, z0, x1, z1 = b
        return (x0 + inset, z0 + inset, x1 - inset, z1 - inset)

    @property
    def base(self):
        return self.y + PAD_LIFT


def split(b, n, along_x=True, gap=1.0):
    x0, z0, x1, z1 = b
    out = []
    for i in range(n):
        if along_x:
            a = x0 + (x1 - x0) * i / n
            c = x0 + (x1 - x0) * (i + 1) / n
            out.append((a + gap / 2, z0, c - gap / 2, z1))
        else:
            a = z0 + (z1 - z0) * i / n
            c = z0 + (z1 - z0) * (i + 1) / n
            out.append((x0, a + gap / 2, x1, c - gap / 2))
    return out


def halves(b, along_x=False, gap=1.4):
    return split(b, 2, along_x, gap)


def d_old_harbor(d):
    m, r = d.m, d.rnd
    blocks = d.streets([.30, .64], [.48])
    for b in blocks:
        d.pavement(b)
    for b in blocks:
        x0, z0, x1, z1 = d.lot(b)
        if x0 < d.x0 + 10 and z0 < d.cz:     # a cargo ship moored along the quay, north half
            K.cargo_ship(m, d.x0 - 10, (z0 + z1) / 2 + 4, -1.0, length=64, yaw=90)
        if x0 < d.x0 + 10:                   # quay blocks: containers + cranes on the water side
            K.containers(m, x0 + 14, z0 + 2, max(1, int((z1 - z0 - 4) / 6.6)), 3, d.base, r)
            for cz in (z0 + (z1 - z0) * .3, z0 + (z1 - z0) * .75):
                K.harbour_crane(m, d.x0 + 6, cz, d.base, yaw=90)
        elif x1 > d.x1 - 10:                 # east: old row houses towards Riverside
            for hb in halves((x0, z0, x1, z1), along_x=False):
                K.row_houses(m, *hb, d.base, r, ["brick", "brickL", "plasterY", "plaster", "brickD"],
                             ["roofR", "roofT", "roofK"], along_x=True, floors=(2, 4))
        else:                                # warehouses
            for wb in halves((x0, z0, x1, z1), along_x=False, gap=3):
                K.warehouse(m, *wb, d.base, r.uniform(8, 12), r.choice(["brick", "brickD", "concrete", "galv"]),
                            r.choice(["roofK", "roofL", "roofG"]), r.random(), saw=r.random() < .6)


def d_riverside(d):
    m, r = d.m, d.rnd
    blocks = d.streets([.34, .68], [.36, .72])
    for i, b in enumerate(blocks):
        if i == 4:
            d.park(b, 10)
            continue
        d.pavement(b)
        x0, z0, x1, z1 = d.lot(b)
        if i % 3 == 2:
            for ab in halves((x0, z0, x1, z1), along_x=True, gap=2):
                K.apartment(m, *ab, d.base, r, floors=(4, 6))
        else:
            for hb in halves((x0, z0, x1, z1), along_x=False):
                K.row_houses(m, *hb, d.base, r, ["plaster", "plasterY", "plasterP", "plasterB", "plasterG", "cream"],
                             ["roofR", "roofT", "roofG", "roofB"], along_x=True, floors=(2, 3))
    # river promenade trees along the south edge
    for i in range(10):
        d.trees.append((d.x0 + 6 + i * (2 * d.hx - 12) / 9, d.base, d.z1 + 5))


def d_hilltop(d):
    m, r = d.m, d.rnd
    blocks = d.streets([.5], [.5])
    for b in blocks:
        d.pavement(b, "grassD")
        x0, z0, x1, z1 = d.lot(b, 3)
        for gx in range(2):
            for gz in range(2):
                cx = x0 + (x1 - x0) * (gx + .5) / 2 + r.uniform(-2, 2)
                cz = z0 + (z1 - z0) * (gz + .5) / 2 + r.uniform(-2, 2)
                K.villa(m, cx, cz - 2, d.base, r)
                if r.random() < .6:
                    K.villa_pool(m, cx + r.uniform(-2, 2), cz + 7.5, d.base)
                d.trees.append((cx + r.choice((-9, 9)), d.base, cz + r.uniform(-6, 6)))


def d_market_square(d):
    m, r = d.m, d.rnd
    blocks = d.streets([.3, .7], [.3, .7])
    for i, b in enumerate(blocks):
        d.pavement(b, "stone" if i == 4 else "concreteL")
        x0, z0, x1, z1 = d.lot(b)
        if i == 4:                           # the festival square: market hall + lamps around
            K.market_hall(m, (x0 + x1) / 2 - 4, (z0 + z1) / 2, d.base, r.random())
            for k in range(8):
                a = 2 * math.pi * k / 8
                d.lamps.append(((x0 + x1) / 2 + math.cos(a) * 15, (z0 + z1) / 2 + math.sin(a) * 13))
                K.lamp(m, d.lamps[-1][0], d.lamps[-1][1], d.base, 4.5)
        elif i in (1, 7):
            K.shop_row(m, x0, z0, x1, (z0 + z1) / 2 - .8, d.base, r)
            K.shop_row(m, x0, (z0 + z1) / 2 + .8, x1, z1, d.base, r)
        else:
            for ab in split((x0, z0, x1, z1), 3, along_x=True, gap=1.5):
                K.apartment(m, *ab, d.base, r, floors=(3, 8), style="shop")


def d_tech_park(d):
    m, r = d.m, d.rnd
    blocks = d.streets([.5], [.42])
    for i, b in enumerate(blocks):
        d.pavement(b)
        x0, z0, x1, z1 = d.lot(b, 2.4)
        if i == 0:
            K.tower(m, (x0 + x1) / 2 - 10, (z0 + z1) / 2 - 4, 18, 18, d.base, 62, "glass", "roofL", "curtain", r.random(), tiers=3)
            K.tower(m, x1 - 8, z0 + 9, 12, 12, d.base, 38, "glassD", "roofL", "curtain", r.random(), tiers=2)
            K.tower(m, x1 - 9, z1 - 10, 14, 12, d.base, 28, "steel", "roofK", "office", r.random(), tiers=1)
            K.parking(m, x0, z1 - 12, x0 + 26, z1, d.base, r)
        else:
            for hb in halves((x0, z0, x1 - 12 if i == 3 else x1, z1), along_x=i % 2 == 1, gap=5):
                K.data_hall(m, *hb, d.base, r.random(), h=r.uniform(8, 11))
            if i == 3:
                K.parking(m, x1 - 11, z0, x1, z1, d.base, r)


def d_stadium_row(d):
    m, r = d.m, d.rnd
    blocks = d.streets([.62], [])
    d.pavement(blocks[0], "stone")
    d.pavement(blocks[1])
    x0, z0, x1, z1 = d.lot(blocks[0], 0)
    K.stadium(m, (x0 + x1) / 2, d.cz, d.base, rx=min(34.0, (x1 - x0) / 2 - 3), rz=min(30.0, d.hz - 12))
    for k in range(6):                       # fan-zone lamps on the forecourt
        lx = x0 + 6 + k * (x1 - x0 - 12) / 5
        for lz in (z0 + 4, z1 - 4):
            d.lamps.append((lx, lz))
            K.lamp(m, lx, lz, d.base, 5.0)
    x0, z0, x1, z1 = d.lot(blocks[1])
    for hb in split((x0, z0, x1, z1), 4, along_x=False, gap=6):
        K.row_houses(m, *hb, d.base, r, ["brick", "brickL", "plasterY", "plaster"], ["roofR", "roofK"],
                     along_x=True, floors=(3, 4))


def d_hospital_hill(d):
    m, r = d.m, d.rnd
    blocks = d.streets([.62], [.55])
    for i, b in enumerate(blocks):
        if i != 3:
            d.pavement(b)
    x0, z0, x1, z1 = d.lot(blocks[0], 0)
    K.hospital(m, (x0 + x1) / 2, (z0 + z1) / 2 + 2, d.base, r.random())
    d.park(blocks[3], 9)
    x0, z0, x1, z1 = d.lot(blocks[1])
    K.parking(m, x0, z0, x1, (z0 + z1) / 2 - 1, d.base, r, fill=.8)
    K.apartment(m, x0, (z0 + z1) / 2 + 1, x1, z1, d.base, r, floors=(3, 5))
    for b in (blocks[2],):
        x0, z0, x1, z1 = d.lot(b)
        for ab in halves((x0, z0, x1, z1), along_x=True, gap=3):
            K.apartment(m, *ab, d.base, r, floors=(3, 5))


def d_airport_flats(d):
    m, r = d.m, d.rnd
    blocks = d.streets([.18], [])
    d.pavement(blocks[0])
    x0, z0, x1, z1 = d.lot(blocks[0])
    K.terminal(m, x0, z0 + 6, x1, z1 - 26, d.base, r.random())
    K.control_tower(m, (x0 + x1) / 2, z1 - 14, d.base, r.random())
    # airfield: apron, runway with edge lights (lamps = ripple), two hangars
    ax0 = blocks[1][0] + 2
    y = d.y + .25
    m.poly([(ax0, y, d.z1 - 2), (d.x1 - 2, y, d.z1 - 2), (d.x1 - 2, y, d.z0 + 2), (ax0, y, d.z0 + 2)], "concreteD")
    rz0, rz1 = d.cz + 6, d.cz + 22
    m.poly([(ax0 + 4, y + .1, rz1), (d.x1 - 4, y + .1, rz1), (d.x1 - 4, y + .1, rz0), (ax0 + 4, y + .1, rz0)], "asphaltD")
    for k in range(int((d.x1 - ax0 - 8) / 10) + 1):
        lx = ax0 + 4 + k * 10
        for lz in (rz0 - .8, rz1 + .8):
            m.boxr(lx - .7, y, lz - .7, lx + .7, y + .6, lz + .7, "e_lamp", skip="b")
        m.poly([(lx - 2.5, y + .15, d.cz + 14.4), (lx + 2.5, y + .15, d.cz + 14.4), (lx + 2.5, y + .15, d.cz + 13.6),
                (lx - 2.5, y + .15, d.cz + 13.6)], "white")
    for hx0 in (ax0 + 10, ax0 + 46):
        K.hangar(m, hx0, d.z0 + 6, hx0 + 28, d.z0 + 30, d.base)
    K.airliner(m, ax0 + 104, d.z0 + 20, d.y + .3, yaw=180)
    K.airliner(m, ax0 + 136, d.z0 + 20, d.y + .3, yaw=180)
    K.parking(m, x0, z1 - 24, x1, z1 - 2, d.base, r)


DISTRICT_FILL = {"old_harbor": d_old_harbor, "riverside": d_riverside, "hilltop": d_hilltop,
                 "market_square": d_market_square, "tech_park": d_tech_park, "stadium_row": d_stadium_row,
                 "hospital_hill": d_hospital_hill, "airport_flats": d_airport_flats}
DISTRICT_INFO = {}


def district(m, did):
    d = District(m, did)
    m.uvfn = d.uvfn
    DISTRICT_FILL[did](d)
    m.ao_distance = 5.0
    m.ao_strength = .5
    DISTRICT_INFO[did] = d


# ---------------------------------------------------------------------------------------------------- plants
def _plant_data(m, t):
    m.cur_uv = (0.0, L.PLANT_TYPES.index(t) + .5)


def chimney(m, x, z, base, h, r=3.2):
    m.cyl((x, base, z), r, h, "concrete", n=8, r2=r * .72, cap_bottom=False)
    m.cyl((x, base + h * .82, z), r * .78, h * .08, "red", n=8, caps=False)
    m.cyl((x, base + h * .93, z), r * .75, h * .07, "white", n=8, caps=False)
    m.boxr(x - .5, base + h, z - .5, x + .5, base + h + 1.0, z + .5, "l_red", skip="b")


def smoke(m, x, z, y, k=1.0):
    for i, (dx, dy, dz, s) in enumerate(((0, 2, 0, 4.2), (3, 7, -2, 5.4), (7, 13, -5, 6.6))):
        m.ell((x + dx * k, y + dy * k, z + dz * k), (s * k, s * .8 * k, s * k), "smoke", n=6, rings=2, cap=False)


def plant_steam(m, lvl):
    x, z, y, _ = L.PLANTS["steam"]
    _plant_data(m, "steam")
    if True:
        m.boxr(x - 22, y, z - 18, x + 6, y + 24, z + 6, "brickD", skip="b")                # boiler house
        m.prism([(z - 19, y + 24), (z + 7, y + 24), (z - 6, y + 29)], x - 23, x + 7, "roofK", plane="zy")
        m.boxr(x + 6, y, z - 14, x + 40, y + 15, z + 4, "brick", skip="b")                 # turbine hall
        for k in range(5):                                                               # lit windows (plant code)
            m.boxr(x + 9 + k * 6, y + 8, z + 4, x + 12.5 + k * 6, y + 11.5, z + 4.3, "l_warm", skip="bk")
        # coal yard + conveyor
        for k in range(3):
            m.lathe([(7, 0), (0, 5)], "charcoal", n=6, c=(x - 34 + k * 2, y, z + 22 - k * 13), cap_bottom=False)
        m.bar((x - 30, y + 2, z + 8), (x - 20, y + 18, z - 4), .9, "galv", n=4)
        chimney(m, x - 12, z - 30, y, 62)
        smoke(m, x - 12, z - 30, y + 64)
        if lvl >= 2:
            m.boxr(x + 40, y, z - 14, x + 58, y + 15, z + 4, "brick", skip="b")
            chimney(m, x + 8, z - 30, y, 66)
            smoke(m, x + 8, z - 30, y + 68)
        if lvl >= 3:
            m.boxr(x - 22, y, z + 8, x + 6, y + 20, z + 26, "brickD", skip="b")
            m.poly([(x - 22, y + 20, z + 26), (x + 6, y + 20, z + 26), (x + 6, y + 20, z + 8), (x - 22, y + 20, z + 8)], "roofK")
            chimney(m, x + 28, z - 30, y, 70)
            smoke(m, x + 28, z - 30, y + 72)
            m.boxr(x + 44, y + 15, z - 2, x + 56, y + 17, z + 2, "cyan", skip="b")


def plant_peaker(m, lvl):
    x, z, y, _ = L.PLANTS["peaker"]
    _plant_data(m, "peaker")
    units = lvl + 1
    for u in range(units):
        ux = x - 22 + u * 15
        m.boxr(ux, y, z - 8, ux + 12, y + 9, z + 8, "lgrey", skip="b")
        m.prism([(z - 8.5, y + 9), (z + 8.5, y + 9), (z, y + 11)], ux - .3, ux + 12.3, "roofL", plane="zy")
        m.cyl((ux + 6, y + 9, z - 12), 1.8, 14, "galv", n=6, cap_bottom=False)
        m.boxr(ux + 2, y + 4, z + 8, ux + 10, y + 6, z + 8.3, "l_warm", skip="bk")
        m.boxr(ux + 4, y, z - 13, ux + 8, y + 9, z - 8, "steelD", skip="b")
    for k in range(lvl):
        m.cyl((x + 28, y, z - 14 + k * 12), 4.6, 7, "white", n=10)
    m.boxr(x - 24, y, z + 12, x + 20, y + 1.0, z + 22, "concreteD", skip="b")


def plant_hydro(m, lvl):
    x, z, y, _ = L.PLANTS["hydro"]
    _plant_data(m, "hydro")
    (ax, az), (bx, bz) = L.DAM
    top = L.RESERVOIR_Y + 3.0
    # dam wall: a slightly curved concrete arch between the valley shoulders, spillway face on the downstream side
    n = 10
    pts = []
    for i in range(n + 1):
        t = i / n
        px = ax - 8 + (bx - ax + 16) * t
        pz = az - 10 * math.sin(math.pi * t)
        pts.append((px, pz))
    for (p0x, p0z), (p1x, p1z) in zip(pts, pts[1:]):
        m.poly([(p0x, -4, p0z + 6), (p1x, -4, p1z + 6), (p1x, top, p1z), (p0x, top, p0z)], "concrete")      # face
        m.poly([(p0x, top, p0z), (p1x, top, p1z), (p1x, top, p1z - 3), (p0x, top, p0z - 3)], "concreteL")  # crest
        m.poly([(p0x, top, p0z - 3), (p1x, top, p1z - 3), (p1x, -2, p1z - 3), (p0x, -2, p0z - 3)], "concreteD")
    # reservoir surface (code 0.45: the shader lowers it 0..6 m with the level uniform)
    res = L.RESERVOIR
    yres = L.RESERVOIR_Y
    cxr = sum(p[0] for p in res) / len(res)
    czr = sum(p[1] for p in res) / len(res)
    for (p0x, p0z), (p1x, p1z) in zip(res, res[1:] + res[:1]):
        tri = [(cxr, yres, czr), (p1x, yres, p1z), (p0x, yres, p0z)]
        nn = (Vector(tri[1]) - Vector(tri[0])).cross(Vector(tri[2]) - Vector(tri[0]))
        if nn.y < 0:
            tri.reverse()
        m.poly(tri, "water")
    # powerhouse at the foot + penstocks; L2 / L3 add turbines (more lit bays) and a second penstock
    m.boxr(x - 16, -1, z + 10, x + 16, 9, z + 24, "concreteL", skip="b")
    m.poly([(x - 16, 9, z + 24), (x + 16, 9, z + 24), (x + 16, 9, z + 10), (x - 16, 9, z + 10)], "roofL")
    for k in range(lvl + 1):
        m.boxr(x - 13 + k * 7, 3, z + 24, x - 9 + k * 7, 7, z + 24.3, "l_warm", skip="bk")
    for k in range(lvl):
        m.bar((x - 8 + k * 8, top - 2, z + 2), (x - 8 + k * 8, 4, z + 10), 1.4, "steelD", n=6)
    m.boxr(x + 18, -1, z + 12, x + 26, 6, z + 20, "galv", skip="b")
    m.boxr(x + 19, 6, z + 15, x + 25, 6.6, z + 17, "cyan", skip="b")


WIND_SITES = []


def _wind_sites():
    if WIND_SITES:
        return WIND_SITES
    r = rng("wind_sites")
    cx, cz, _y, _yaw = L.PLANTS["wind"]
    for row in range(3):
        for k in range(8):
            px = cx - 112 + k * 32 + (16 if row == 1 else 0) + r.uniform(-3, 3)
            pz = cz - 4 + row * 34 + r.uniform(-4, 4)
            WIND_SITES.append((px, H(px, pz), pz))
    # level order: middle row first (closest to the ridge crest), then front, then back
    order = list(range(8, 16)) + list(range(16, 24)) + list(range(0, 8))
    WIND_SITES[:] = [WIND_SITES[i] for i in order]
    return WIND_SITES


HUB_Y = 46.0


def plant_wind(m, lvl):
    _plant_data(m, "wind")
    for (px, py, pz) in _wind_sites()[:8 * lvl]:
        m.cyl((px, py - 1, pz), 1.5, HUB_Y + 1, "turbine", n=6, r2=.8, cap_bottom=False)
        m.boxr(px - 1.3, py + HUB_Y - 1.2, pz - 4.5, px + 1.3, py + HUB_Y + 1.4, pz + 1.6, "turbine")
        m.boxr(px - .3, py + HUB_Y + 1.4, pz - 4.2, px + .3, py + HUB_Y + 1.8, pz - 3.6, "l_red", skip="b")


def plant_solar(m, lvl):
    x, z, y, _ = L.PLANTS["solar"]
    _plant_data(m, "solar")
    rows = (6, 12, 24)[lvl - 1]
    for k in range(rows):
        rz = z - 44 + (k % 12) * 7.5
        x0, x1 = (x - 54, x - 3) if k < 12 else (x + 3, x + 54)
        # tilted panel row facing south (+Z, the camera side): front edge low, back edge high; a frame lip in front
        m.poly([(x0, y + .8, rz + 2.6), (x1, y + .8, rz + 2.6), (x1, y + 2.6, rz), (x0, y + 2.6, rz)], "solar")
        m.poly([(x0, y + .2, rz + 2.7), (x1, y + .2, rz + 2.7), (x1, y + .8, rz + 2.6), (x0, y + .8, rz + 2.6)], "solarF")
    m.boxr(x - 4, y, z - 48, x + 4, y + 3.4, z - 42, "galv", skip="b")
    m.boxr(x - 3, y + 3.4, z - 46, x + 3, y + 3.8, z - 44, "cyan", skip="b")


def plant_battery(m, lvl):
    x, z, y, _ = L.PLANTS["battery"]
    _plant_data(m, "battery")
    n = 4 * lvl
    for k in range(n):
        cx = x - 20 + (k % 6) * 7.4
        cz = z - 10 + (k // 6) * 10
        m.boxr(cx, y, cz, cx + 6, y + 3.0, cz + 2.6 + 4, "battery", skip="b")
        # charge strip on the street side (+Z face): data u = position along the strip 0..1
        sx0, sx1 = cx + .5, cx + 5.5
        ys = y + 1.9
        m.poly([(sx0, ys, cz + 6.62), (sx1, ys, cz + 6.62), (sx1, ys + .6, cz + 6.62), (sx0, ys + .6, cz + 6.62)],
               "batt_strip", uv=[(0, L.PLANT_TYPES.index("battery") + .5), (1, L.PLANT_TYPES.index("battery") + .5),
                              (1, L.PLANT_TYPES.index("battery") + .5), (0, L.PLANT_TYPES.index("battery") + .5)])
    m.boxr(x + 24, y, z - 10, x + 30, y + 4, z - 4, "lgrey", skip="b")
    m.boxr(x + 25, y + 4, z - 8, x + 29, y + 4.4, z - 6, "cyan", skip="b")


PLANT_FN = {"steam": plant_steam, "peaker": plant_peaker, "hydro": plant_hydro, "wind": plant_wind,
            "solar": plant_solar, "battery": plant_battery}


# ---------------------------------------------------------------------------------------------------- grid
PYLON_H = 24.0
PYLON_GAP = 54.0


def _inside_district(x, z, margin=2.0):
    for _id, (cx, cz), (hx, hz), _y, _s in L.DISTRICTS:
        if abs(x - cx) < hx + margin and abs(z - cz) < hz + margin:
            return True
    return False


def _ground(x, z):
    return max(L.WATER_Y + .5, H(x, z))


GRID_INFO = {}


def pylon(m, x, z, yaw):
    y = _ground(x, z)
    with m.at((x, y, z), ry=yaw):
        m.lathe([(2.2, 0), (1.1, PYLON_H * .55), (.55, PYLON_H)], "pylon", n=4, phase=math.pi / 4, cap_bottom=False)
        m.boxr(-6.0, PYLON_H * .72, -.35, 6.0, PYLON_H * .72 + .7, .35, "pylon", skip="")
        m.boxr(-4.2, PYLON_H * .92, -.3, 4.2, PYLON_H * .92 + .6, .3, "pylon", skip="")
        m.cone((0, PYLON_H, 0), .55, 2.4, "pylon", n=4, phase=math.pi / 4, cap=False)
    # conductor attachment points in city space (left, mid, right)
    c, s = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    pts = []
    for lx, ly in ((-5.6, PYLON_H * .72 - .8), (0.0, PYLON_H * .92 - .8), (5.6, PYLON_H * .72 - .8)):
        pts.append((x + lx * c, y + ly, z - lx * s))
    return pts


def span(m, a, b, sag=2.6, width=.55, segs=4):
    """One conductor between attachment points a, b: a flat ribbon (faces up) with a parabolic sag."""
    ax, ay, az = a
    bx, by, bz = b
    dx, dz = bx - ax, bz - az
    ln = math.hypot(dx, dz) or 1.0
    nx, nz = -dz / ln * width / 2, dx / ln * width / 2
    prev = None
    for i in range(segs + 1):
        t = i / segs
        p = (ax + dx * t, ay + (by - ay) * t - sag * 4 * t * (1 - t), az + dz * t)
        if prev is not None:
            q = [(prev[0] - nx, prev[1], prev[2] - nz), (prev[0] + nx, prev[1], prev[2] + nz),
                 (p[0] + nx, p[1], p[2] + nz), (p[0] - nx, p[1], p[2] - nz)]
            nn = (Vector(q[1]) - Vector(q[0])).cross(Vector(q[2]) - Vector(q[0]))
            if nn.y < 0:
                q.reverse()
            m.poly(q, "wire")
        prev = p


def gantry_points(x, z, y):
    """Conductor ends on a substation gantry (substation(): beam at local z = -5.2, 8.5-9.1 m)."""
    return [(x - 3.0, y + 8.4, z - 5.2), (x, y + 8.6, z - 5.2), (x + 3.0, y + 8.4, z - 5.2)]


def _sub_y(name):
    for d in L.DISTRICTS:
        if d[0] == name:
            return d[3] + PAD_LIFT
    for h in L.HUBS:
        if h[0] == name:
            return h[2]
    return None


def grid_lines(m):
    for idx, c in enumerate(L.CORRIDORS):
        pts = L.corridor_points(c)
        # pylons every ~PYLON_GAP m along the path, skipped inside district boxes (cables run underground there)
        dense = []
        for a, b in zip(pts, pts[1:]):
            n = max(1, round(math.dist(a, b) / PYLON_GAP))
            for i in range(n):
                t = i / n
                dense.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
        dense.append(pts[-1])
        towers = []
        with m.data(u=idx, v=0.0):
            for i, (x, z) in enumerate(dense):
                if i == 0 or i == len(dense) - 1:
                    continue
                if _inside_district(x, z, 3.0):
                    continue
                a = dense[i - 1]
                b = dense[i + 1]
                yaw = math.degrees(math.atan2(b[0] - a[0], b[1] - a[1])) + 90
                towers.append(pylon(m, x, z, yaw))
            # end points: substation gantries (district / hub) or the plant's tie point (plant switchyard)
            ends = []
            for name, (x, z) in ((c[1], dense[0]), (c[2], dense[-1])):
                y = _sub_y(name)
                if y is None:
                    y = _ground(x, z)
                    m.boxr(x - 2.5, y, z - 2.5, x + 2.5, y + 6.5, z + 2.5, "galv", skip="b")
                ends.append(gantry_points(x, z, y))
            chain = [ends[0]] + towers + [ends[1]]
            for p, q in zip(chain, chain[1:]):
                for k in range(3):
                    span(m, p[k], q[k], sag=2.2 if math.dist(p[k], q[k]) > 30 else .8)
        GRID_INFO[c[0]] = {"index": idx, "pylons": len(towers), "points": [[r2(p[0]), r2(p[1])] for p in pts]}
    m.ao_strength = .35
    m.ao_distance = 3.0


def substation(m, smart):
    """Fenced substation yard 18 x 14 m (local, ground pivot): transformers, a gantry, a cyan flow ring.
    smart = + a control kiosk with an antenna mast and a second ring (reroute capable)."""
    m.cur_uv = (-1.0, 0.0)
    m.boxr(-9, 0, -7, 9, .2, 7, "concreteD", skip="b")
    m.boxr(-9, .2, 6.85, 9, 2, 7, "galv", skip="bk")
    m.boxr(-9, .2, -7, 9, 2, -6.85, "galv", skip="bf")
    m.boxr(-9, .2, -7, -8.85, 2, 7, "galv", skip="br")
    m.boxr(8.85, .2, -7, 9, 2, 7, "galv", skip="bl")
    for k in range(2):
        tx = -4.5 + k * 9
        m.boxr(tx - 2.2, .2, -2, tx + 2.2, 4.2, 2.5, "steelD", skip="b")
        for f in range(3):
            m.boxr(tx - 1.8 + f * 1.6, 4.2, -.4, tx - 1.4 + f * 1.6, 6.2, 0, "insul", skip="b")
    for sx in (-4, 4):
        m.boxr(sx - .3, .2, -5.5, sx + .3, 9.5, -4.9, "galv", skip="b")
    m.boxr(-4.3, 8.5, -5.5, 4.3, 9.1, -4.9, "galv")
    m.torus((0, .5, 4.6), 2.2, .35, "cyan", nu=10, nv=3)
    if smart:
        m.boxr(5.0, .2, 3.0, 8.4, 3.2, 6.4, "white", skip="b")
        m.bar((6.7, 3.2, 4.7), (6.7, 12.0, 4.7), .18, "galv", n=4)
        m.boxr(6.3, 12.0, 4.3, 7.1, 12.8, 5.1, "l_cyan", skip="b")
        m.torus((0, .5, 4.6), 3.4, .3, "cyan", nu=12, nv=3)
    m.ao_strength = .4
    m.ao_distance = 1.5


# ---------------------------------------------------------------------------------------------------- props
def wheel(m, x, y, z, r, w, side, caps=True):
    """Wheel with its axis along X: inner face at x, extends outwards by w (side = +1 right, -1 left)."""
    m.cyl((x, y, z), r, w, "tyre", n=6, rot=(0, 0, -90) if side > 0 else (0, 0, 90), caps=caps)


def truck_crew(m):
    """Generic utility bucket truck, 7.2 m (no real fleet's livery: the body is the 'truck' livery code).
    Boom + bucket vertices carry data UV.x = 1 (the shader raises them about the pivot (0, 2.9, -1.6))."""
    m.boxr(-1.15, .55, -3.5, 1.15, 1.0, 3.4, "charcoal")                        # chassis
    m.boxr(-1.2, .9, 1.4, 1.2, 3.0, 3.5, "truck", bevel=.12)                     # cab
    m.boxr(-1.12, 2.0, 3.45, 1.12, 2.8, 3.52, "carglass", skip="bk")             # windscreen
    m.boxr(-1.25, 2.15, 1.6, 1.25, 2.75, 3.2, "carglass", skip="fktb")           # side windows
    m.boxr(-1.2, .9, -3.6, 1.2, 2.3, 1.2, "truck")                               # utility body
    m.boxr(-1.22, 1.5, -3.4, 1.22, 1.75, 1.0, "truckD", skip="tb")               # stripe
    m.boxr(-.5, 3.0, 2.3, .5, 3.25, 2.9, "l_amber", skip="b")                    # beacon bar
    m.boxr(-1.0, 1.4, 3.52, -.6, 1.75, 3.56, "l_head", skip="k")
    m.boxr(.6, 1.4, 3.52, 1.0, 1.75, 3.56, "l_head", skip="k")
    m.boxr(-1.1, 1.0, -3.66, -.7, 1.3, -3.6, "l_red", skip="f")
    m.boxr(.7, 1.0, -3.66, 1.1, 1.3, -3.6, "l_red", skip="f")
    for z in (2.4, -1.4, -2.6):
        for sx in (-1, 1):
            wheel(m, sx * .85, .5, z, .5, .3, sx)
    with m.data(u=1.0):
        m.boxr(-.5, 2.3, -2.2, .5, 2.9, -1.0, "steelD")                          # turret
        m.boxr(-.22, 2.75, -1.9, .22, 3.05, 2.6, "boom")                         # boom (stowed forward)
        m.boxr(-.6, 2.5, 2.6, .6, 3.5, 3.4, "truck")                             # bucket
    m.ao_strength = .4


def car(m):
    """Generic small hatchback, 4.0 m. Data UV: x = 1 on the roof (raised for vans), y = 1 on the tail (shortened
    for city cars) — three silhouettes from one mesh = one MultiMesh draw. Body = paint code (instance colour)."""
    m.boxr(-.85, .3, -2.0, .85, .95, 2.0, "carbody", bevel=.15)
    with m.data(u=1.0):
        m.loft([[(-.75, .95, -1.5), (.75, .95, -1.5), (.66, 1.5, -1.2), (-.66, 1.5, -1.2)],
                [(-.75, .95, .9), (.75, .95, .9), (.66, 1.5, .4), (-.66, 1.5, .4)]], "carglass",
               keys=lambda i, j: "carglass" if (j < 0 or j >= 2) else ("carbody" if i in (0, 2) else "carglass"))
    with m.data(v=1.0):
        m.boxr(-.8, .3, -2.25, .8, .9, -1.9, "carbody")
        m.boxr(-.75, .6, -2.27, -.45, .8, -2.24, "l_red", skip="f")
        m.boxr(.45, .6, -2.27, .75, .8, -2.24, "l_red", skip="f")
    m.boxr(-.75, .6, 2.0, -.45, .8, 2.04, "l_head", skip="k")
    m.boxr(.45, .6, 2.0, .75, .8, 2.04, "l_head", skip="k")
    for z in (1.3, -1.3):
        for sx in (-1, 1):
            wheel(m, sx * .7, .33, z, .33, .2, sx, caps=False)
    m.ao_strength = .35


def fault_marker(m):
    """Spark + ring: a crossed star card (two-sided) over a ground ring, always lit (red / amber)."""
    pts = []
    for k in range(8):
        a = math.pi * k / 4 + math.pi / 8
        r = 3.2 if k % 2 == 0 else 1.2
        pts.append((math.cos(a) * r, 6 + math.sin(a) * r))
    for (ax, ay), (bx, by) in zip(pts, pts[1:] + pts[:1]):
        m.poly([(0, 6, 0), (ax, ay, 0), (bx, by, 0)], "l_amber", two_sided=True)
    m.bar((0, .4, 0), (0, 3.6, 0), .25, "l_amber", n=4, caps=False)
    m.torus((0, .4, 0), 4.0, .45, "l_red", nu=8, nv=3)
    m.ao_strength = 0.0


def tree(m):
    """Generic round tree, 9 m (MultiMesh: instance scale / yaw vary it; foliage code = shader tint + sway)."""
    m.cyl((0, 0, 0), .45, 3.4, "trunk", n=4, r2=.3, caps=False)
    m.ell((0, 5.0, 0), (2.6, 2.4, 2.6), "leaf", n=6, rings=3, cap=False)
    m.ell((.9, 7.2, .3), (1.7, 1.6, 1.7), "leafL", n=5, rings=2, cap=False)
    m.ao_strength = .45


def wind_blades(m):
    """Rotor: hub + three blades in the local XY plane facing +Z; the shader spins it about local Z (one MultiMesh)."""
    m.cone((0, 0, -.4), 1.1, 2.2, "turbine", n=6, rot=(90, 0, 0), cap=False)
    for k in range(3):
        a = 2 * math.pi * k / 3
        c, s = math.cos(a), math.sin(a)

        def P(r, w):
            return (c * r - s * w, s * r + c * w)
        p0, p1, p2, p3 = P(.8, -.9), P(.8, .9), P(24, .25), P(24, -.4)
        m.poly([(p0[0], p0[1], .15), (p3[0], p3[1], .15), (p2[0], p2[1], .15), (p1[0], p1[1], .15)], "turbine")
    m.ao_strength = 0.0


# ---------------------------------------------------------------------------------------------------- export
# id, builder, group, budget, material, pivot (None = city space)
MODELS = [("terrain_city", terrain_city, "world", 10500, "vc_world", None),
          ("sky_dome", sky_dome, "world", 400, "vc_world", None)]
for _d in L.DISTRICT_IDS:
    MODELS.append(("district_" + _d, (lambda d: lambda m: district(m, d))(_d), "world", 6500, "vc_world", None))
MODELS.append(("grid_lines", grid_lines, "world", 7000, "vc_world", None))
PLANT_BUDGET = {"steam": 1800, "peaker": 1200, "hydro": 2200, "wind": 4400, "solar": 1500, "battery": 900}
for _t in L.PLANT_TYPES:
    for _l in (1, 2, 3):
        MODELS.append((f"plant_{_t}_l{_l}", (lambda t, l: lambda m: PLANT_FN[t](m, l))(_t, _l), "plants",
                       PLANT_BUDGET[_t], "vc_world", None))
MODELS += [("substation", lambda m: substation(m, False), "grid", 300, "vc_world", "ground"),
           ("substation_smart", lambda m: substation(m, True), "grid", 420, "vc_world", "ground"),
           ("truck_crew", truck_crew, "props", 600, "vc_prop", "ground"),
           ("car", car, "props", 200, "vc_prop", "ground"),
           ("fault_marker", fault_marker, "props", 72, "vc_prop", "ground"),
           ("tree", tree, "props", 70, "vc_prop", "ground"),
           ("wind_blades", wind_blades, "props", 60, "vc_blade", "origin")]


def _deterministic_fbx():
    """Blender's FBX exporter stamps the wall-clock time and derives object UUIDs from Python's per-process randomised
    hash(): pin both so two builds write identical bytes."""
    import datetime
    import zlib

    import io_scene_fbx.export_fbx_bin as fbx_bin
    import io_scene_fbx.fbx_utils as fbx_utils

    if getattr(fbx_bin, "_vc_pinned", False):
        return
    orig = fbx_bin.fbx_header_elements

    def header(root, scene_data, time=None):
        return orig(root, scene_data, datetime.datetime(2026, 10, 27, 0, 0, 0))
    fbx_bin.fbx_header_elements = header

    def stable_hash(key):
        return zlib.crc32(repr(key).encode()) * 2654435761 % (2 ** 62)
    fbx_utils.hash = stable_hash
    fbx_bin._vc_pinned = True


def export(obj, path_fbx, path_glb):
    _deterministic_fbx()
    import io_scene_fbx.fbx_utils as fbx_utils
    fbx_utils._keys_to_uuids.clear()
    fbx_utils._uuids_to_keys.clear()
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=str(path_fbx), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             use_mesh_modifiers=False, mesh_smooth_type="FACE", use_triangles=True,
                             colors_type="LINEAR", prioritize_active_color=True, add_leaf_bones=False,
                             bake_anim=False, path_mode="STRIP", embed_textures=False, use_custom_props=False,
                             use_tspace=False)
    bpy.ops.export_scene.gltf(filepath=str(path_glb), export_format="GLB", use_selection=True,
                              export_yup=True, export_apply=False, export_animations=False,
                              export_cameras=False, export_lights=False, export_texcoords=True,
                              export_normals=True, export_materials="EXPORT", export_vertex_color="ACTIVE",
                              export_all_vertex_colors=False, export_morph=False, export_tangents=False,
                              export_extras=False)


def unity_vec(v):
    """Authoring (x, y, +z south) -> Unity (x, y, +z north)."""
    return [v[0], v[1], -v[2]] if len(v) == 3 else [v[0], -v[1]]


def bounds(obj):
    """Unity-space bounds of the exported mesh (Blender object space (x, y_b, z_b) = authoring (x, -z, y) ->
    Unity (x, y, -z) = (x_b, z_b, y_b))."""
    vs = [(v.co.x, v.co.z, v.co.y) for v in obj.data.vertices]
    lo = [round(min(v[i] for v in vs), 3) for i in range(3)]
    hi = [round(max(v[i] for v in vs), 3) for i in range(3)]
    return lo, hi


def kit_counts():
    """Triangles of every kit piece in a reference size (manifest 'kit' section; ART-IDS)."""
    out = {}
    r = rng("kit")
    samples = {
        "block": lambda m: K.block(m, 0, 0, 14, 12, 0, 14, "plaster", "roofL", "home", .5, units=1),
        "gable_house": lambda m: K.gable_house(m, 0, 0, 8, 10, 0, 7, "plaster", "roofR", "home", .5),
        "row_houses": lambda m: K.row_houses(m, 0, 0, 30, 10, 0, r, ["plaster"], ["roofR"]),
        "tower": lambda m: K.tower(m, 0, 0, 16, 16, 0, 60, "glass", "roofL", "curtain", .5, tiers=3),
        "warehouse": lambda m: K.warehouse(m, 0, 0, 30, 18, 0, 10, "brick", "roofK", .5),
        "harbour_crane": lambda m: K.harbour_crane(m, 0, 0, 0),
        "containers": lambda m: K.containers(m, 0, 0, 2, 3, 0, r),
        "data_hall": lambda m: K.data_hall(m, 0, 0, 40, 20, 0, .5),
        "stadium": lambda m: K.stadium(m, 0, 0, 0),
        "hospital": lambda m: K.hospital(m, 0, 0, 0, .5),
        "terminal": lambda m: K.terminal(m, 0, 0, 80, 20, 0, .5),
        "control_tower": lambda m: K.control_tower(m, 0, 0, 0, .5),
        "hangar": lambda m: K.hangar(m, 0, 0, 28, 24, 0),
        "market_hall": lambda m: K.market_hall(m, 0, 0, 0, .5),
        "villa": lambda m: K.villa(m, 0, 0, 0, r),
        "lamp": lambda m: K.lamp(m, 0, 0, 0),
        "apartment": lambda m: K.apartment(m, 0, 0, 16, 14, 0, r),
        "shop_row": lambda m: K.shop_row(m, 0, 0, 36, 12, 0, r),
    }
    for name, fn in samples.items():
        m = Model("kit_" + name)
        fn(m)
        out[name] = m.triangles()
        m.bm.free()
    return out


def layout_json():
    trees = []
    for did, d in DISTRICT_INFO.items():
        for (x, y, z) in d.trees:
            trees.append(unity_vec([r2(x), r2(y), r2(z)]) + [did])
    r = rng("trees")
    # scattered trees on open land: away from water, roads, districts, plant pads and the wind ridge rows
    road_pts = []
    for _n, pts in L.ROADS:
        for a, b in zip(pts, pts[1:]):
            road_pts.append((a, b))
    tries = 0
    wild = []
    while len(wild) < 420 and tries < 40000:
        tries += 1
        x = r.uniform(-300, 320)
        z = r.uniform(-420, 380)
        h = H(x, z)
        if h < 1.2 or _inside_district(x, z, 6) or L.river_dist(x, z) < 22:
            continue
        if any(L._seg_dist(x, z, a, b)[0] < 9 for a, b in road_pts):
            continue
        if any(abs(x - px) < 60 and abs(z - pz) < 50 for t, (px, pz, _py, _yw) in L.PLANTS.items()
               if t not in ("wind", "hydro")):
            continue
        if L.in_poly(x, z, L.RESERVOIR) or (-300 < z < -276 and 90 < x < 220):
            continue
        if any(math.hypot(x - wx, z - wz) < 14 for wx, _wy, wz in _wind_sites()):
            continue
        # clusters: keep a tree only where a low-frequency field says "wood"
        wood = math.sin(x * .031 + 1.1) * math.cos(z * .027 - .7) + .35 * math.sin(x * .09 + z * .07)
        if wood < (-.25 if z < -170 or L.river_dist(x, z) < 60 else .15):
            continue
        wild.append(unity_vec([r2(x), r2(h), r2(z)]) + [""])
    trees += wild
    districts = []
    for did, (cx, cz), (hx, hz), y, sub in L.DISTRICTS:
        d = DISTRICT_INFO[did]
        mdl = MANIFEST_MODELS.get("district_" + did, {})
        districts.append({"id": did, "box_min": mdl["bounds_min"], "box_max": mdl["bounds_max"],
                          "pad_y": y, "substation": unity_vec([sub[0], r2(y + PAD_LIFT), sub[1]]), "ripple_radius": r2(d.R),
                          "lamps": len(d.lamps), "trees": len(d.trees)})
    roads = []
    for name, _pts in L.ROADS:
        roads.append({"id": name, "half_width": L.ROAD_HALF, "centre": [unity_vec(list(p)) for p in ROAD_LINES[name]]})
    plants = {}
    for t, (x, z, y, yaw) in L.PLANTS.items():
        plants[t] = {"site": unity_vec([x, y, z]), "levels": [f"plant_{t}_l{k}" for k in (1, 2, 3)]}
    # rotor hubs: the rotor faces the camera (south = Unity -Z); level n uses the first 8 * n hubs
    plants["wind"]["hubs"] = [unity_vec([r2(px), r2(py + HUB_Y), r2(pz + 1.8)]) for px, py, pz in _wind_sites()]
    plants["hydro"]["reservoir_y"] = L.RESERVOIR_Y
    for c in GRID_INFO.values():
        c["points"] = [unity_vec(p) for p in c["points"]]
    return {"schema": 1, "space": "unity: x east, y up, z north, metres", "water_y": L.WATER_Y,
            "districts": districts,
            "hubs": [{"id": h[0], "pos": unity_vec([h[1][0], h[2], h[1][1]])} for h in L.HUBS],
            "corridors": [dict(id=k, a=c[1], b=c[2], **GRID_INFO[k]) for k, c in ((c[0], c) for c in L.CORRIDORS)],
            "plants": plants, "plant_types": L.PLANT_TYPES, "roads": roads, "trees": trees}


MANIFEST_MODELS = {}


FACE_NORTH = ("truck_crew", "car")
UV_MEANING = {
    "world": "UV0/UV1 per material code (vckit module doc)",
    "plants": "UV0.x battery strip 0..1, UV0.y plant type index + .5",
    "grid": "UV0.x corridor index (-1 on substations)",
    "props": "UV0.x / UV0.y morph flags (truck boom; car roof / tail); vehicles: nose = Unity local +Z",
}


def sidecar(name, group, mat, keys, tris, lo, hi):
    codes = {}
    for k in keys:
        a = vckit.code_of(k)
        codes[k] = {"code": a, "index": int(round(a * 20)), "srgb": "#" + vckit.PALETTE[k]}
    return {"id": name, "group": group, "material": mat, "shader": vckit.SHADER_OF[mat], "triangles": tris,
            "unity_bounds_min": lo, "unity_bounds_max": hi, "space": "unity: x east, y up, z north, metres",
            "vertex_colour": "sRGB-encoded palette x baked AO; alpha = material code (index = round(a * 20))",
            "uv": UV_MEANING[group], "palette": codes}


def main():
    for g in ("world", "plants", "grid", "props"):
        (OUT / g).mkdir(parents=True, exist_ok=True)
    manifest = {"schema": 2, "generator": "blender/build_art.py", "target": "Unity 6 LTS URP (Android, portrait)",
                "space": "unity: x east, y up, z north, metres",
                "shaders": vckit.SHADER_OF, "models": MANIFEST_MODELS}
    for name, fn, group, budget, mat, pivot in MODELS:
        for o in list(bpy.data.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        for me in list(bpy.data.meshes):
            bpy.data.meshes.remove(me)
        m = Model(name, material=mat)
        m.smooth = False
        fn(m)
        tris = m.triangles()
        if name in FACE_NORTH:
            # vehicles are authored nose +Z (south); turn them so the nose is Unity local +Z (= authoring -Z)
            bmesh.ops.rotate(m.bm, verts=m.bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi, 3, "Y"))
        if getattr(m, "smooth", False):
            for f in m.bm.faces:
                f.smooth = True
        if pivot in (None, "origin"):
            obj, keys = m.to_object(origin_shift=(0, 0, 0))
        else:
            obj, keys = m.to_object(ground_pivot=True)
        fbx = OUT / group / f"{name}.fbx"
        glb = OUT / group / f"{name}.glb"
        export(obj, fbx, glb)
        lo, hi = bounds(obj)
        assert 0 < tris <= budget, (name, tris, budget)
        side = OUT / group / f"{name}.json"
        side.write_text(json.dumps(sidecar(name, group, mat, keys, tris, lo, hi), indent=1, sort_keys=True) + "\n")
        entry = {"group": group, "material": mat, "triangles": tris, "budget": budget, "bounds_min": lo,
                 "bounds_max": hi, "keys": keys}
        for kind, path in (("fbx", fbx), ("glb", glb), ("sidecar", side)):
            blob = path.read_bytes()
            entry[kind] = {"path": str(path.relative_to(ROOT)), "bytes": len(blob), "sha256": hashlib.sha256(blob).hexdigest()}
        MANIFEST_MODELS[name] = entry
        print(f"MODEL {name} tris={tris} budget={budget} size={[round(h - l, 1) for l, h in zip(lo, hi)]}")
    manifest["kit"] = kit_counts()
    lay = json.dumps(layout_json(), indent=1, sort_keys=True) + "\n"
    (OUT / "layout.json").write_text(lay)
    manifest["layout_sha256"] = hashlib.sha256(lay.encode()).hexdigest()
    text = json.dumps(manifest, indent=1, sort_keys=True) + "\n"
    (OUT / "manifest.json").write_text(text)
    print(f"ART_OK: {len(MANIFEST_MODELS)} models, {sum(v['triangles'] for v in MANIFEST_MODELS.values())} triangles, "
          f"manifest sha256 {hashlib.sha256(text.encode()).hexdigest()}")


main()
