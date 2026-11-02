"""W66 model pass (10-29): Medium / High (HD) variants of the 10 weakest models players see in the first 10 minutes.

Picked by tools/w66_coverage.gd (bot play on the real Main scene, home / region / vehicle cameras, ID render) among
the models no earlier art pass touched (adobe homes, desert plants, mesa, rock, clouds, shop / apartment / office /
barn / cottage / warehouse were redone 10-22..10-27). Writes assets/models/hd/<name>.glb + hd/manifest.json; the
canonical files (what Low draws) and data/model_meta.json are NOT touched.

    /Volumes/BuildSSD/blender/blender -b --factory-startup -P tools/blender/build_w66.py
    python3 tools/blender/build_w66.py --compare <dirA> <dirB>     (two builds: same triangle sets per model)

Rules (asserted at build time):
  * same object names as the canonical file (Body + Wheel_<n><L|R> for vehicles, one object named after the model
    otherwise), the same wheel origins / radii (VehicleActor reads Wheel*.position.y), the same pivot;
  * the HD bounding box stays inside the canonical one (+2 cm): placement, footprints, traffic gaps, attachment
    mounts and the Low casters read data/model_meta.json, which keeps describing both files;
  * only the shared 8x8 palette (tools/palette.py) and the one Palette material: 0 extra draws, no new textures;
  * deterministic: seeded jitter only. Blender 5.2's glTF exporter may write the same triangles in another index
    order (LESSONS 10-29), so "byte-stable" is checked as a triangle SET (--compare), and a model is committed only
    when its geometry really changed.
"""
import json
import math
import os
import random
import struct
import sys

if "--compare" in sys.argv:   # plain python half: compare two output folders by triangle set
    def _tris(path):
        data = open(path, "rb").read()
        jl = struct.unpack_from("<I", data, 12)[0]
        js = json.loads(data[20:20 + jl])
        bin_ = data[20 + jl + 8:]
        out = set()
        for mesh in js["meshes"]:
            for p in mesh["primitives"]:
                def acc(i):
                    a = js["accessors"][i]
                    bv = js["bufferViews"][a["bufferView"]]
                    off = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
                    comp = {5126: "f", 5125: "I", 5123: "H", 5121: "B"}[a["componentType"]]
                    n = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[a["type"]]
                    stride = bv.get("byteStride", struct.calcsize(comp) * n)
                    return [struct.unpack_from("<%d%s" % (n, comp), bin_, off + k * stride) for k in range(a["count"])]
                pos = acc(p["attributes"]["POSITION"])
                uv = acc(p["attributes"]["TEXCOORD_0"])
                idx = [i[0] for i in acc(p["indices"])]
                for k in range(0, len(idx), 3):
                    tri = tuple(sorted(tuple(round(c, 5) for c in pos[i] + uv[i]) for i in idx[k:k + 3]))
                    out.add((mesh.get("name", ""), tri))
        return out
    a_dir, b_dir = sys.argv[sys.argv.index("--compare") + 1:][:2]
    bad = 0
    for f in sorted(os.listdir(a_dir)):
        if f.endswith(".glb"):
            same = _tris(os.path.join(a_dir, f)) == _tris(os.path.join(b_dir, f))
            bad += 0 if same else 1
            print(("same " if same else "DIFF ") + f)
    sys.exit(1 if bad else 0)

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_assets as a  # noqa: E402
import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
from palette import uv_for  # noqa: E402

HD_DIR = os.environ.get("W66_OUT") or os.path.join(a.ROOT, "assets", "models", "hd")
CANON = json.load(open(a.META_PATH))
TOL = 0.02
a.OUT_DIR = HD_DIR
a.META = {}
a.reset_scene()
a.MAT = a.palette_material()
MANIFEST = {}
ONLY = set(os.environ.get("W66_ONLY", "").split(",")) - {""}


# ------------------------------------------------------------------------------------------------ helpers
def paint_faces(mb, faces, color):
    u, v = uv_for(color)
    for f in faces:
        for loop in f.loops:
            loop[mb.uv].uv = (u, v)


def hull(mb, pts, color_of):
    """Convex hull of `pts`; each face painted color_of(face centre)."""
    verts = [mb.bm.verts.new(p) for p in pts]
    res = bmesh.ops.convex_hull(mb.bm, input=verts)
    loose = {g for g in res.get("geom_interior", []) + res.get("geom_unused", []) if isinstance(g, bmesh.types.BMVert)}
    if loose:
        bmesh.ops.delete(mb.bm, geom=list(loose), context="VERTS")
    for f in [g for g in res["geom"] if isinstance(g, bmesh.types.BMFace)]:
        paint_faces(mb, [f], color_of(f.calc_center_median()))


def rock(mb, c, r, rnd, color_of, n=9, squash=0.8):
    """Faceted boulder: a jittered point cloud around `c` with radii r = (rx, ry, rz)."""
    pts = []
    for i in range(n):
        th = math.acos(1 - 2 * (i + 0.5) / n)
        ph = math.pi * (1 + 5 ** 0.5) * i + rnd.uniform(-0.3, 0.3)
        k = rnd.uniform(0.82, 1.08)
        pts.append((c[0] + math.sin(th) * math.cos(ph) * r[0] * k, c[1] + math.sin(th) * math.sin(ph) * r[1] * k,
                    c[2] + math.cos(th) * r[2] * k * squash))
    hull(mb, pts, color_of)


def lump(mb, c, r, color, subdiv=1, rnd=None, jitter=0.0):
    """Soft foliage lump: icosphere, optional seeded radial jitter (smooth-shaded by the caller)."""
    res = bmesh.ops.create_icosphere(mb.bm, subdivisions=subdiv, radius=1.0)
    verts = res["verts"]
    for v in verts:
        k = 1.0 + (rnd.uniform(-jitter, jitter) if rnd else 0.0)
        v.co = Vector((v.co.x * r[0] * k + c[0], v.co.y * r[1] * k + c[1], v.co.z * r[2] * k + c[2]))
    faces = {f for v in verts for f in v.link_faces}
    paint_faces(mb, faces, color)
    return faces


def tube(mb, pts, radius, color, segs=8, cap=True, radii=None):
    """Swept tube through `pts` (rings perpendicular to each segment)."""
    rings = []
    for i, p in enumerate(pts):
        p = Vector(p)
        d = (Vector(pts[min(i + 1, len(pts) - 1)]) - Vector(pts[max(i - 1, 0)])).normalized()
        side = d.cross(Vector((0, 1, 0)))
        if side.length < 1e-4:
            side = d.cross(Vector((1, 0, 0)))
        side.normalize()
        up = side.cross(d).normalized()
        r = radii[i] if radii else radius
        rings.append([mb.bm.verts.new(p + (side * math.cos(t) + up * math.sin(t)) * r)
                      for t in [2 * math.pi * k / segs for k in range(segs)]])
    faces = []
    for i in range(len(rings) - 1):
        for k in range(segs):
            j = (k + 1) % segs
            faces.append(mb.bm.faces.new([rings[i][k], rings[i][j], rings[i + 1][j], rings[i + 1][k]]))
    if cap:
        faces.append(mb.bm.faces.new(list(reversed(rings[0]))))
        faces.append(mb.bm.faces.new(rings[-1]))
    paint_faces(mb, faces, color)
    return faces


def ribbed(mb, r, z0, z1, c, color, ribs=8, depth=0.18, rings_z=None, top_round=True):
    """Ribbed column (cactus): star-section rings lofted from z0 to z1 with a rounded top."""
    zs = rings_z or [z0, (z0 + z1) / 2, z1]
    rings = []
    n = ribs * 2
    for z in zs:
        rings.append([mb.bm.verts.new((c[0] + math.cos(2 * math.pi * k / n) * r * (1 if k % 2 == 0 else 1 - depth),
                                       c[1] + math.sin(2 * math.pi * k / n) * r * (1 if k % 2 == 0 else 1 - depth), z))
                      for k in range(n)])
    if top_round:
        for z_off, s in ((r * 0.55, 0.78), (r * 0.92, 0.42)):
            rings.append([mb.bm.verts.new((c[0] + (v.co.x - c[0]) * s, c[1] + (v.co.y - c[1]) * s, zs[-1] + z_off))
                          for v in rings[-1 if z_off == r * 0.55 else -1]])
    faces = []
    for i in range(len(rings) - 1):
        for k in range(n):
            j = (k + 1) % n
            faces.append(mb.bm.faces.new([rings[i][k], rings[i][j], rings[i + 1][j], rings[i + 1][k]]))
    top = mb.bm.verts.new((c[0], c[1], rings[-1][0].co.z + r * 0.12))
    for k in range(n):
        faces.append(mb.bm.faces.new([rings[-1][k], rings[-1][(k + 1) % n], top]))
    faces.append(mb.bm.faces.new(list(reversed(rings[0]))))
    paint_faces(mb, faces, color)
    return faces


def tris_of(obj):
    me = obj.data
    me.calc_loop_triangles()
    return len(me.loop_triangles)


def smooth(obj):
    for p in obj.data.polygons:
        p.use_smooth = True


def finish(name, col, objs_smooth=()):
    for o in col.objects:
        if o.name in objs_smooth:
            smooth(o)
    tris = sum(tris_of(o) for o in col.objects)
    names = sorted(o.name for o in col.objects)
    a.export(name, col)
    _check(name, names, tris)


def _check(name, names, tris):
    hd, cn = a.META[name], CANON[name]
    for i in range(3):
        if hd["min"][i] < cn["min"][i] - TOL or hd["max"][i] > cn["max"][i] + TOL:
            raise SystemExit("W66 %s leaves the canonical box on axis %d: hd %s..%s canonical %s..%s"
                             % (name, i, hd["min"][i], hd["max"][i], cn["min"][i], cn["max"][i]))
    MANIFEST[name] = {"tris": tris, "objects": names, "min": hd["min"], "max": hd["max"]}
    print("W66_MODEL %s tris=%d objects=%s" % (name, tris, ",".join(names)))


def model(name, fn, organic=False):
    if ONLY and name not in ONLY:
        return
    col = a.new_collection(name)
    mb = a.MB()
    fn(mb)
    obj = mb.build(name, col)
    finish(name, col, (obj.name,) if organic else ())


# ------------------------------------------------------------------------------------------------ vehicles
def hd_wheel(col, name, r, w, loc, rim="chrome"):
    """Same origin / radius as the canonical wheel: tyre with rounded shoulders, recessed rim, hub."""
    mb = a.MB()
    mb.cyl(r, w * 0.72, (0, 0, 0), "tire", axis="X", segs=14)
    for s in (-1, 1):   # shoulders
        mb.cyl(r * 0.94, w * 0.14, (s * w * 0.43, 0, 0), "tire", axis="X", segs=14)
    mb.cyl(r * 0.6, w + 0.01, (0, 0, 0), rim, axis="X", segs=10)
    mb.cyl(r * 0.24, w + 0.02, (0, 0, 0), "darkgray", axis="X", segs=6)
    return mb.build(name, col, loc)


def hd_wheels(col, xs, ys, r, w, rim="chrome"):
    i = 0
    for y in ys:
        for x in xs:
            hd_wheel(col, "Wheel_%d%s" % (i, "L" if x < 0 else "R"), r, w, (x, y, r), rim)
        i += 1


def vehicle(name, body_fn, wheel_fn):
    if ONLY and name not in ONLY:
        return
    col = a.new_collection(name)
    mb = a.MB()
    body_fn(mb)
    mb.build("Body", col)
    wheel_fn(col)
    finish(name, col)


def mirrors(mb, x, y, z, color="charcoal"):
    for s in (-1, 1):
        mb.box((0.03, 0.03, 0.03), (s * (x - 0.02), y, z - 0.02), color)
        mb.box((0.05, 0.04, 0.09), (s * x, y, z + 0.03), color, bevel=0.01)


def hd_box_truck(mb):
    # chassis + fuel tank + side skirts
    mb.box((0.86, 2.6, 0.2), (0, -0.1, 0.36), "charcoal")
    for s in (-1, 1):
        mb.cyl(0.1, 0.36, (s * 0.38, 0.22, 0.3), "chrome", axis="Y", segs=8)
        mb.box((0.03, 0.56, 0.16), (s * 0.45, -0.25, 0.3), "darkgray")
    # cab: lower body, raked windscreen, side windows, roof cap, grille, bumper, lamps, mirrors, step
    mb.box((1.0, 0.8, 0.5), (0, 0.82, 0.6), "paint", bevel=0.06)
    mb.box((0.98, 0.7, 0.36), (0, 0.78, 1.02), "paint", bevel=0.05, taper=(1.0, 0.86))
    mb.box((0.84, 0.05, 0.3), (0, 1.13, 1.03), "glass", rot=(-0.2, 0, 0))
    for s in (-1, 1):
        mb.box((0.02, 0.42, 0.26), (s * 0.49, 0.85, 1.03), "glass")
        mb.box((0.02, 0.3, 0.04), (s * 0.505, 0.78, 0.72), "charcoal")   # door handle line
        mb.box((0.12, 0.14, 0.03), (s * 0.4, 1.18, 0.28), "steel")       # step
    mb.box((0.94, 0.66, 0.06), (0, 0.76, 1.22), "paint", bevel=0.02)
    mb.box((0.62, 0.04, 0.22), (0, 1.22, 0.62), "chrome", bevel=0.01)
    for z in (0.56, 0.62, 0.68):
        mb.box((0.56, 0.05, 0.02), (0, 1.235, z), "charcoal")
    mb.box((1.02, 0.1, 0.14), (0, 1.25, 0.38), "darkgray", bevel=0.02)
    for s in (-1, 1):
        mb.box((0.16, 0.04, 0.09), (s * 0.38, 1.235, 0.66), "lamp")
        mb.box((0.06, 0.03, 0.04), (s * 0.44, 1.24, 0.47), "neonorange")
    mirrors(mb, 0.53, 1.06, 1.0)
    # cargo box: body, corner posts, top / bottom rails, ribs, roll-up rear door, stripe, marker lamps
    mb.box((1.04, 1.68, 1.22), (0, -0.48, 1.12), "white", bevel=0.03)
    for sx in (-1, 1):
        for sy in (-1, 1):
            mb.box((0.06, 0.06, 1.24), (sx * 0.52, -0.48 + sy * 0.84, 1.12), "lightgray")
    for z in (0.5, 1.74):   # top / bottom frame rails round the box (not a lid: the roof stays white)
        for sx in (-1, 1):
            mb.box((0.05, 1.74, 0.05), (sx * 0.515, -0.48, z), "lightgray")
        for sy in (-1, 1):
            mb.box((1.08, 0.05, 0.05), (0, -0.48 + sy * 0.845, z), "lightgray")
    mb.box((1.08, 1.5, 0.22), (0, -0.48, 1.16), "paint")
    mb.box((1.08, 1.5, 0.03), (0, -0.48, 1.035), "darkgray")
    mb.box((0.9, 0.03, 1.08), (0, -1.335, 1.1), "lightgray")
    for z in [0.66 + 0.12 * k for k in range(8)]:
        mb.box((0.9, 0.035, 0.02), (0, -1.345, z), "gray")
    mb.box((0.04, 0.03, 0.22), (0.3, -1.35, 0.7), "charcoal")
    for s in (-1, 1):
        mb.box((0.12, 0.03, 0.08), (s * 0.38, -1.345, 0.5), "taillight")
        mb.box((0.04, 0.04, 0.04), (s * 0.48, 0.32, 1.74), "neonorange")
        mb.box((0.04, 0.06, 0.18), (s * 0.4, -1.25, 0.2), "charcoal")   # mud flaps


def hd_minibus(mb):
    # lower body + rounded nose + window band with pillars + roof with vents
    mb.box((1.04, 2.48, 0.6), (0, -0.04, 0.56), "paint", bevel=0.08)
    mb.box((1.0, 0.24, 0.5), (0, 1.17, 0.58), "paint", bevel=0.09, taper=(0.96, 0.8))
    mb.box((1.04, 2.3, 0.36), (0, -0.05, 1.0), "glass", bevel=0.03)
    for y in (-1.05, -0.62, -0.18, 0.26, 0.7):
        mb.box((1.06, 0.07, 0.36), (0, y, 1.0), "paint")
    mb.box((1.02, 2.46, 0.12), (0, -0.04, 1.23), "white", bevel=0.04)
    mb.box((0.88, 0.1, 0.42), (0, 1.22, 0.98), "glass", rot=(-0.28, 0, 0))
    mb.box((0.92, 0.06, 0.06), (0, 1.27, 1.17), "white")
    mb.box((0.8, 0.04, 0.3), (0, -1.29, 1.0), "glass")
    for x in (-0.25, 0.25):   # roof vents
        mb.box((0.26, 0.3, 0.06), (x, -0.5, 1.32), "lightgray", bevel=0.02)
    mb.box((0.5, 0.36, 0.05), (0, 0.4, 1.31), "lightgray", bevel=0.02)
    # stripe, sliding door, bumpers, grille, lamps, mirrors
    mb.box((1.06, 2.3, 0.07), (0, -0.04, 0.68), "white")
    mb.box((0.02, 0.6, 0.6), (0.525, 0.45, 0.82), "darkgray")
    mb.box((0.02, 0.56, 0.56), (0.53, 0.45, 0.82), "paint")
    mb.box((0.02, 0.4, 0.03), (0.54, 0.45, 0.62), "chrome")
    mb.box((1.08, 0.1, 0.16), (0, 1.29, 0.3), "darkgray", bevel=0.02)
    mb.box((1.08, 0.1, 0.16), (0, -1.29, 0.3), "darkgray", bevel=0.02)
    mb.box((0.5, 0.04, 0.12), (0, 1.3, 0.48), "charcoal")
    for s in (-1, 1):
        mb.box((0.16, 0.04, 0.08), (s * 0.36, 1.29, 0.5), "lamp")
        mb.box((0.14, 0.04, 0.12), (s * 0.4, -1.3, 0.5), "taillight")
    mirrors(mb, 0.55, 1.12, 0.92)
    for s in (-1, 1):   # wheel arches
        for y in (0.82, -0.82):
            mb.box((0.03, 0.56, 0.12), (s * 0.52, y, 0.5), "charcoal")


def hd_sedan(mb):
    # lower body with sculpted hood / boot, cabin with raked pillars, window frames, arches, bumpers, lamps
    mb.box((0.84, 1.5, 0.24), (0, 0, 0.32), "paint", bevel=0.06)
    mb.box((0.82, 0.42, 0.12), (0, 0.5, 0.48), "paint", bevel=0.04, taper=(0.98, 0.8))
    mb.box((0.82, 0.32, 0.12), (0, -0.56, 0.48), "paint", bevel=0.04, taper=(0.98, 0.8))
    mb.box((0.8, 0.9, 0.04), (0, -0.08, 0.5), "charcoal", bevel=0.01)
    mb.box((0.76, 0.82, 0.27), (0, -0.08, 0.63), "glass", taper=(0.9, 0.7), bevel=0.03)
    mb.box((0.66, 0.5, 0.05), (0, -0.1, 0.77), "paint", bevel=0.02)
    for s in (-1, 1):
        mb.box((0.03, 0.05, 0.22), (s * 0.38, -0.08, 0.62), "paint")       # B pillar
        for y in (0.46, -0.46):
            mb.box((0.03, 0.4, 0.1), (s * 0.425, y, 0.3), "charcoal")    # arches
        mb.box((0.02, 0.16, 0.02), (s * 0.43, 0.02, 0.44), "chrome")     # handles
    mb.box((0.86, 0.06, 0.1), (0, 0.73, 0.26), "darkgray", bevel=0.02)
    mb.box((0.86, 0.06, 0.1), (0, -0.73, 0.26), "darkgray", bevel=0.02)
    mb.box((0.34, 0.03, 0.07), (0, 0.755, 0.38), "charcoal")
    for s in (-1, 1):
        mb.box((0.14, 0.04, 0.06), (s * 0.28, 0.74, 0.4), "lamp")
        mb.box((0.14, 0.04, 0.06), (s * 0.28, -0.74, 0.4), "taillight")
    mirrors(mb, 0.44, 0.26, 0.55)


def hd_taxi(mb):
    hd_sedan(mb)
    mb.box((0.3, 0.14, 0.04), (0, -0.1, 0.81), "charcoal")
    mb.box((0.28, 0.12, 0.1), (0, -0.1, 0.86), "neonyellow", bevel=0.02)
    for s in (-1, 1):   # checker band
        for k in range(6):
            mb.box((0.012, 0.09, 0.04), (s * 0.425, -0.42 + k * 0.15, 0.4), "black" if k % 2 == 0 else "white")


# ------------------------------------------------------------------------------------------------ garage
def hd_garage_l2(mb):
    mb.box((5.0, 3.6, 0.12), (0, 0, 0.06), "concrete")
    for x in (-1.9, -1.0, -0.1, 0.8):   # parking bay lines in the yard
        mb.box((0.04, 0.9, 0.01), (x, 1.32, 0.125), "marking")
    # shed: plinth, walls with corrugation ribs, gable roof with eaves, ridge cap and skylights
    mb.box((3.64, 2.64, 0.3), (-0.5, -0.4, 0.27), "concrete")
    mb.box((3.6, 2.6, 1.56), (-0.5, -0.4, 1.08), "lightsand", bevel=0.04)
    for x in [-2.2 + 0.34 * k for k in range(11)]:
        mb.box((0.04, 2.64, 1.5), (x, -0.4, 1.08), "khaki")
    for y in (-1.7, -1.32, -0.94, -0.56, -0.18, 0.2, 0.58):
        mb.box((3.64, 0.04, 1.5), (-0.5, y, 1.08), "khaki")
    mb.gable(3.6, 2.6, 0.7, (-0.5, -0.4, 1.86), "roofred", rot=(0, 0, math.pi / 2))
    mb.box((3.8, 0.12, 0.06), (-0.5, -0.4, 2.52), "darkred")
    for x in (-1.6, -0.5, 0.6):
        mb.box((0.5, 0.36, 0.04), (x, -0.95, 2.22), "glass", rot=(0.49, 0, 0))
    # two roller doors with frames + slats + hazard kerbs
    for x in (-1.35, 0.35):
        mb.box((1.42, 0.08, 1.3), (x, 0.9, 0.77), "darkgray")
        mb.box((1.3, 0.06, 1.2), (x, 0.92, 0.72), "steel")
        for z in [0.22 + 0.12 * k for k in range(10)]:
            mb.box((1.3, 0.08, 0.025), (x, 0.935, z), "gray")
        for s in (-1, 1):
            mb.box((0.08, 0.1, 0.2), (x + s * 0.68, 1.0, 0.22), "yellow")
        mb.box((0.18, 0.1, 0.08), (x, 0.98, 1.48), "lamp")
    # office annex: windows with frames, door + canopy, AC unit, flat roof parapet
    mb.box((1.2, 1.6, 1.4), (1.9, 0.2, 0.82), "white", bevel=0.05)
    mb.box((1.3, 1.7, 0.1), (1.9, 0.2, 1.56), "darkgray")
    mb.box((0.66, 0.05, 0.46), (1.72, 1.01, 0.92), "darkgray")
    mb.box((0.6, 0.06, 0.4), (1.72, 1.02, 0.92), "glass")
    mb.box((0.02, 0.07, 0.4), (1.72, 1.03, 0.92), "darkgray")
    mb.box((0.3, 0.05, 0.62), (2.26, 1.01, 0.43), "darkwood")
    mb.box((0.44, 0.3, 0.04), (2.26, 1.12, 0.8), "darkgray")
    mb.box((0.05, 0.6, 0.36), (2.52, 0.2, 1.0), "glass")
    mb.box((0.5, 0.4, 0.26), (1.75, -0.2, 1.74), "lightgray", bevel=0.02)
    mb.cyl(0.13, 0.03, (1.75, -0.2, 1.88), "darkgray", segs=8)
    # company sign board with frame
    mb.box((1.7, 0.1, 0.5), (-0.5, 0.95, 2.05), "darkgray", bevel=0.02)
    mb.box((1.6, 0.12, 0.42), (-0.5, 0.95, 2.05), "paint", bevel=0.02)
    mb.box((1.0, 0.13, 0.08), (-0.5, 0.95, 2.05), "white")
    # fence: posts + rails, gate posts; yard clutter: pallets, crates, drums
    for x in (-2.4, -1.6, -0.8, 0.0, 0.8, 1.6, 2.4):
        mb.box((0.06, 0.06, 0.6), (x, -1.75, 0.4), "gray")
    for z in (0.62, 0.36):
        mb.box((4.9, 0.04, 0.04), (0, -1.75, z), "gray")
    mb.box((0.7, 0.6, 0.12), (2.1, 1.35, 0.18), "wood")
    mb.box((0.36, 0.36, 0.36), (1.95, 1.35, 0.42), "wood", bevel=0.03)
    mb.box((0.3, 0.32, 0.32), (2.3, 1.32, 0.4), "khaki", bevel=0.03)
    mb.box((0.28, 0.28, 0.26), (2.1, 1.35, 0.73), "wood", bevel=0.03)
    for x, y, c in ((-2.3, 1.45, "blue"), (-2.05, 1.5, "red"), (-2.2, 1.22, "blue")):
        mb.cyl(0.13, 0.38, (x, y, 0.31), c, segs=10)
        mb.cyl(0.135, 0.03, (x, y, 0.3), "darkgray", segs=10)


# ------------------------------------------------------------------------------------------------ crossing
def hd_border_gantry(mb):
    for x in (-2.3, 2.3):
        mb.box((0.5, 0.48, 0.1), (x, 0, 0.05), "concrete", bevel=0.02)
        mb.box((0.3, 0.3, 0.06), (x, 0, 0.13), "steel")
        for dx in (-0.1, 0.1):
            for dy in (-0.1, 0.1):
                mb.box((0.03, 0.03, 0.05), (x + dx, dy, 0.18), "darkgray")
        # I-beam post: web + two flanges, hazard stripes at the foot
        mb.box((0.06, 0.18, 2.7), (x, 0, 1.5), "steel")
        for s in (-1, 1):
            mb.box((0.2, 0.03, 2.7), (x, s * 0.09, 1.5), "steel")
        for k in range(3):
            mb.box((0.22, 0.22, 0.1), (x, 0, 0.3 + k * 0.2), "yellow" if k % 2 == 0 else "black")
    # truss beam: chords, verticals, diagonals
    for z in (2.7, 3.0):
        for s in (-1, 1):
            mb.box((4.82, 0.05, 0.05), (0, s * 0.1, z), "darkgray")
    xs = [-2.3 + 0.46 * k for k in range(11)]
    for i, x in enumerate(xs):
        for s in (-1, 1):
            mb.box((0.035, 0.035, 0.3), (x, s * 0.1, 2.85), "darkgray")
            if i < len(xs) - 1:
                ang = math.atan2(0.3, 0.46) * (1 if i % 2 == 0 else -1)
                mb.box((0.55, 0.03, 0.03), (x + 0.23, s * 0.1, 2.85), "darkgray", rot=(0, ang, 0))
    # sign board: frame, paint face, white border + legend bars, hood lamps
    mb.box((3.06, 0.12, 0.62), (0, 0.06, 3.22), "darkgray", bevel=0.02)
    mb.box((2.96, 0.14, 0.54), (0, 0.06, 3.22), "paint")
    for z in (3.45, 2.99):
        mb.box((2.84, 0.15, 0.03), (0, 0.06, z), "white")
    mb.box((2.2, 0.15, 0.1), (0, 0.06, 3.3), "white")
    mb.box((1.4, 0.15, 0.06), (0, 0.06, 3.13), "white")
    for x in (-1.2, 0, 1.2):
        mb.box((0.2, 0.14, 0.05), (x, 0.13, 2.66), "darkgray")
        mb.box((0.16, 0.1, 0.05), (x, 0.13, 2.62), "lamp")


def hd_bridge(mb):  # deck along Y, 5 long, top surface at z = 0
    mb.box((2.36, 5.0, 0.22), (0, 0, -0.11), "concrete")
    for s in (-1, 1):
        mb.box((0.14, 5.0, 0.36), (s * 1.23, 0, -0.1), "concrete")          # edge beams
        mb.box((0.18, 5.0, 0.08), (s * 1.22, 0, 0.04), "lightgray")         # kerbs
        mb.box((0.06, 5.0, 0.14), (s * 1.28, 0, -0.28), "gray")             # drip edge
        mb.box((0.1, 5.0, 0.08), (s * 1.25, 0, 0.33), "red")                # handrail
        mb.box((0.05, 5.0, 0.04), (s * 1.25, 0, 0.18), "red")               # mid rail
        for y in [-2.4 + 0.4 * k for k in range(13)]:
            mb.box((0.06, 0.06, 0.3), (s * 1.25, y, 0.18), "white")
        for y in (-0.6, 0.6):   # girders under the deck
            pass
    for x in (-0.7, 0.0, 0.7):
        mb.box((0.18, 5.0, 0.24), (x, 0, -0.34), "gray")
    # two piers: cap beam, tapered column pair, cutwater footing
    for y in (-1.2, 1.2):
        mb.box((2.4, 0.5, 0.2), (0, y, -0.56), "concrete")
        for x in (-0.55, 0.55):
            mb.box((0.36, 0.36, 1.0), (x, y, -1.15), "gray", taper=(0.85, 0.85))
        mb.box((1.6, 0.5, 0.2), (0, y, -1.7), "darkgray", taper=(0.9, 0.9))
        for s in (-1, 1):
            mb.cyl(0.25, 0.2, (s * 0.8, y, -1.7), "darkgray", segs=6)


def hd_tunnel(mb):
    rnd = random.Random(6602)
    # concrete portal on both faces: jambs + segmented arch ring + keystone + hazard band; dark vault inside
    zc, r_in, r_out = 1.0, 1.1, 1.36
    for yf in (1.15, -1.15):
        for s in (-1, 1):
            mb.box((0.26, 0.2, 1.0), (s * (r_in + 0.13), yf, 0.5), "concrete")
        n = 9
        for k in range(n):
            t0 = math.pi * k / n
            t = math.pi * (k + 0.5) / n
            rr = (r_in + r_out) / 2
            mb.box((r_out - r_in, 0.2, math.pi * rr / n + 0.03), (math.cos(t) * rr, yf, zc + math.sin(t) * rr),
                   "concrete", rot=(0, -t + math.pi / 2 - math.pi / 2, 0))
        mb.box((0.3, 0.24, 0.3), (0, yf, zc + r_out - 0.08), "lightgray", taper=(1.3, 1.0))
        for k in range(6):
            mb.box((0.3, 0.06, 0.16), (-0.75 + k * 0.3, yf + (0.12 if yf > 0 else -0.12), zc + r_out + 0.12),
                   "yellow" if k % 2 == 0 else "black")
    # vault (inside of the bore, dark) and the side walls of the bore
    n = 9
    for k in range(n):
        t = math.pi * (k + 0.5) / n
        mb.box((0.06, 2.1, math.pi * r_in / n + 0.04), (math.cos(t) * (r_in + 0.03), 0, zc + math.sin(t) * (r_in + 0.03)),
               "charcoal", rot=(0, -t, 0))
    for s in (-1, 1):
        mb.box((0.06, 2.1, 1.0), (s * (r_in + 0.03), 0, 0.5), "charcoal")
    # the hill: faceted boulders round and over the portal, darker at the foot, grass tufts on top
    def rock_col(c):
        return "darkgray" if c.z < 0.9 else ("gray" if c.z < 2.6 else "steel")
    for s in (-1, 1):
        rock(mb, (s * 1.8, 0.0, 0.9), (0.5, 1.2, 1.0), rnd, rock_col, n=10)
        rock(mb, (s * 1.9, 0.25, 0.4), (0.42, 1.1, 0.5), rnd, rock_col, n=8)
        rock(mb, (s * 1.5, -0.2, 1.9), (0.6, 1.15, 0.65), rnd, rock_col, n=10)
    rock(mb, (0.0, 0.0, 2.75), (1.55, 1.15, 0.6), rnd, rock_col, n=14)
    rock(mb, (0.55, -0.05, 3.35), (0.95, 0.95, 0.75), rnd, rock_col, n=12)
    rock(mb, (-0.7, 0.15, 3.2), (0.6, 0.75, 0.45), rnd, rock_col, n=9)
    for x, y, z, r, c in ((-0.25, 0.55, 3.55, 0.28, "olive"), (0.95, -0.4, 3.75, 0.24, "forest"),
                          (-1.2, -0.4, 3.0, 0.22, "olive"), (1.65, 0.5, 2.5, 0.2, "forest"),
                          (0.3, 0.2, 3.92, 0.18, "darkgreen")):
        lump(mb, (x, y, z), (r, r, r * 0.6), c)


# ------------------------------------------------------------------------------------------------ props
def hd_windmill(mb):
    # stone foot ring, tapered octagonal tower with bands, door, windows, gallery, cap, hub, four lattice sails
    mb.cyl(0.5, 0.3, (0, 0, 0.15), "concrete", segs=12, r2=0.48)
    mb.cyl(0.46, 2.3, (0, 0, 1.45), "white", segs=12, r2=0.33)
    for z, r in ((0.95, 0.425), (1.85, 0.375)):
        mb.cyl(r + 0.015, 0.05, (0, 0, z), "lightgray", segs=12)
    mb.box((0.26, 0.06, 0.48), (0, 0.44, 0.48), "darkwood")
    mb.box((0.32, 0.05, 0.06), (0, 0.45, 0.74), "roofred")
    for z, ang in ((1.35, 0.0), (2.05, math.pi)):
        mb.box((0.14, 0.05, 0.2), (math.sin(ang) * 0.4, math.cos(ang) * 0.4, z), "darkblue",
               rot=(0, 0, -ang))
    mb.cyl(0.4, 0.06, (0, 0, 2.62), "wood", segs=12)
    mb.cone(0.42, 0.6, (0, 0, 2.95), "roofred", segs=12)
    mb.ball(0.05, (0, 0, 3.27), "gold")
    mb.cyl(0.07, 0.3, (0, 0.33, 2.3), "darkwood", axis="Y", segs=8)
    mb.cyl(0.1, 0.06, (0, 0.43, 2.3), "darkwood", axis="Y", segs=8)
    for i in range(4):
        t = i * math.pi / 2 + 0.3
        ca, sa = math.cos(t), math.sin(t)
        # stock (spar), the sail frame offset to one side, cross bars, cloth
        mb.box((0.05, 0.04, 1.22), (ca * 0.62, 0.46, 2.3 + sa * 0.62), "darkwood", rot=(0, -t + math.pi / 2, 0))
        px, pz = -sa * 0.11, ca * 0.11
        mb.box((0.24, 0.02, 0.92), (ca * 0.74 + px, 0.45, 2.3 + sa * 0.74 + pz), "white",
               rot=(0, -t + math.pi / 2, 0))
        for f in (0.32, 0.56, 0.8, 1.04):
            mb.box((0.22, 0.03, 0.02), (ca * f + px, 0.47, 2.3 + sa * f + pz), "wood", rot=(0, -t + math.pi / 2, 0))
        mb.box((0.02, 0.03, 0.92), (ca * 0.74 + px * 1.9, 0.47, 2.3 + sa * 0.74 + pz * 1.9), "wood",
               rot=(0, -t + math.pi / 2, 0))


def hd_cactus(mb):
    ribbed(mb, 0.13, 0.0, 1.02, (0, 0, 0), "green", ribs=8, depth=0.16, rings_z=[0.0, 0.08, 0.5, 1.02])
    # right arm: out, elbow, up; left arm lower and shorter
    tube(mb, [(0.08, 0, 0.5), (0.2, 0, 0.53), (0.29, 0, 0.62), (0.31, 0, 0.78), (0.31, 0, 0.9)], 0.075, "green",
         segs=8, radii=[0.075, 0.075, 0.075, 0.072, 0.07])
    mb.ball(0.07, (0.31, 0, 0.9), "green", subdiv=1)
    tube(mb, [(-0.08, 0, 0.66), (-0.18, 0, 0.68), (-0.25, 0, 0.75), (-0.27, 0, 0.88), (-0.27, 0, 0.96)], 0.07,
         "green", segs=8)
    mb.ball(0.065, (-0.27, 0, 0.96), "green", subdiv=1)
    # flowers on the crown and an arm tip; a ring of pebbles at the foot
    for x, y, z, c in ((0.0, 0.03, 1.2, "pink"), (0.05, -0.04, 1.17, "rose"), (0.31, 0.02, 0.97, "yellow")):
        mb.ball(0.04, (x, y, z), c, subdiv=1)
    for k in range(5):
        t = k * 2 * math.pi / 5 + 0.4
        mb.ball(0.04, (math.cos(t) * 0.2, math.sin(t) * 0.09, 0.02), "khaki", subdiv=0, scale=(1.2, 1, 0.6))


def hd_bush(mb):
    rnd = random.Random(6604)
    # five soft lumps: shadowed base, lit crowns, a couple of berries / blossoms
    for c, r, col in (((0.03, 0.0, 0.17), (0.27, 0.24, 0.19), "forest"),
                      ((0.25, 0.08, 0.16), (0.2, 0.18, 0.16), "darkgreen"),
                      ((0.0, 0.0, 0.29), (0.22, 0.2, 0.15), "darkgreen"),
                      ((0.2, 0.06, 0.3), (0.17, 0.16, 0.13), "green"),
                      ((0.05, -0.12, 0.34), (0.15, 0.13, 0.1), "lime")):
        lump(mb, c, r, col, subdiv=1, rnd=rnd, jitter=0.06)
    for x, y, z in ((0.12, -0.2, 0.3), (-0.2, 0.12, 0.32), (0.34, 0.15, 0.3)):
        mb.ball(0.025, (x, y, z), "yellow", subdiv=0)


def main():
    vehicle("box_truck", hd_box_truck, lambda c: hd_wheels(c, (-0.46, 0.46), (0.8, -0.55, -0.98), 0.25, 0.2))
    vehicle("minibus", hd_minibus, lambda c: hd_wheels(c, (-0.48, 0.48), (0.82, -0.82), 0.23, 0.18))
    vehicle("sedan", hd_sedan, lambda c: hd_wheels(c, (-0.38, 0.38), (0.46, -0.46), 0.17, 0.14))
    vehicle("taxi", hd_taxi, lambda c: hd_wheels(c, (-0.38, 0.38), (0.46, -0.46), 0.17, 0.14))
    model("garage_l2", hd_garage_l2)
    model("border_gantry", hd_border_gantry)
    model("bridge", hd_bridge)
    model("tunnel", hd_tunnel)
    model("windmill", hd_windmill)
    model("cactus", hd_cactus, organic=True)
    model("bush", hd_bush, organic=True)
    path = os.path.join(HD_DIR, "manifest.json")
    old = json.load(open(path)) if os.path.exists(path) and ONLY else {}
    old.update(MANIFEST)
    with open(path, "w") as f:
        json.dump({k: old[k] for k in sorted(old)}, f, indent=1, sort_keys=True)
        f.write("\n")
    print("W66_DONE %d models -> %s" % (len(MANIFEST), HD_DIR))


main()
