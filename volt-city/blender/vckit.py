"""Volt City (games/volt-city) art kit: shared palette + bmesh builder authored directly in GODOT space.
Copied from the Pit Lane Boss kit (games/pit-wall/blender/pwkit.py, itself from Kaiten's artkit.py).

Every builder works in Godot coordinates: X right (east), Y up, Z front (+Z = south = towards the portrait camera),
1 unit = 1 m. `Model.to_object()` rotates the finished mesh into Blender space (x, y, z) -> (x, -z, y) so the glTF
exporter (export_yup) writes it back unchanged. World models (terrain, districts, plants, grid) are authored in CITY
coordinates (origin_shift = 0): the game appends them with an identity transform.

Colour model (weak Android phones first): every face carries a palette key, written as a per-corner VERTEX COLOUR
= palette x baked ambient occlusion, stored sRGB-ENCODED (FBX colors_type LINEAR writes the values raw; the Unity
shaders decode sRGB -> linear) and ALPHA = a material code (CODE below, index = round(alpha * 20), read by
unity/Assets/Art/Shaders/VcWorld*.hlsl). One material per model. UV channels are DATA, not texture mappings, written
raw (FBX keeps them; glTF loaders undo the exporter's v flip):
  UV0 (uv)  districts: x = light-ripple distance from the district substation (0..1), y = window seed (0..1) on window
            walls / lamp-pool light on streets;  grid: x = corridor index (0..15, -1 = substation);  plants: x = battery
            strip position, y = plant type + .5;  props: x / y = morph flags (car roof / tail, truck boom);  terrain:
            (lateral m, metres along) on road strips, sky: y = height factor
  UV1 (uv2) window walls: x = window style (kit_buildings.STYLE), y = building base height (m). Small values only:
            Unity half-precision vertex compression must stay OFF for TexCoord0/1 (the importer script sets Mesh
            Compression Off; Player Settings > Vertex Compression: untick TexCoord0 + TexCoord1).
Unity space = authoring space with Z mirrored: Unity (x, y, z) = (east, up, NORTH) = (gx, gy, -gz); the sidecar JSON
carries the expected Unity bounds so the importer check (Assets/Art/Editor/VcArtImport.cs) can verify the axes.
"""
import math
import random
from contextlib import contextmanager

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector
from mathutils.bvhtree import BVHTree

# One shared palette (sRGB hex). Original look: navy-teal dusk, warm amber windows, cyan power, red/amber trouble only.
PALETTE = {
    # neutrals / structure
    "white": "F2F1EC", "offwhite": "E4E0D6", "lgrey": "C2C6CA", "grey": "8C9198", "dgrey": "5A5F66",
    "charcoal": "3A3E45", "black": "202227", "steel": "B4BCC4", "steelD": "7A828A", "galv": "9EA6AD",
    "concrete": "B5B1A8", "concreteD": "8C887F", "concreteL": "CFCBC2", "asphalt": "4C5057", "asphaltD": "3E4248",
    "glass": "6F9FBA", "glassD": "3B6680", "brick": "A4553D", "brickD": "84442F", "brickL": "BD6D52",
    "plaster": "E9DFCB", "plasterY": "EED69A", "plasterP": "EDC4B0", "plasterB": "B5CEDC", "plasterG": "C3D8B3",
    "plasterW": "F3EFE6", "stone": "C6B9A2", "stoneD": "998C78", "sandstone": "D7BF94",
    "roofR": "B0503A", "roofT": "C46A47", "roofG": "5D6A5E", "roofK": "3C424B", "roofB": "3E5A7A", "roofL": "8F949A",
    "copper": "5FA391", "gold": "D9AE45", "wood": "8A6440", "woodL": "B08A5C",
    "red": "D23B36", "redD": "9E2A26", "orange": "EE8A2E", "yellow": "F2C230", "green": "3E9A55", "blue": "2F6CC8",
    "teal": "2A8F8C", "tealD": "1F6466", "navy": "26364F", "purple": "6E58A0", "cream": "F1E3C2",
    "container1": "C0573E", "container2": "3D7A9A", "container3": "D9A440", "container4": "5E8C5A",
    # ground (terrain base colours; the shader picks water / sand / grass / rock by world height)
    "ground": "6F9A4E", "grass": "5E8E45", "grassD": "4A7A3A", "field": "86A456", "wheat": "B3A562", "meadow": "73994C", "plough": "8C7356", "sand": "D8C38F", "rock": "8E8A80",
    "water": "2D6A86", "waterD": "1F4E68", "pool": "3FB6C8", "pier": "7C6A55", "rail": "6C6660",
    # vegetation
    "leaf": "4F9446", "leafD": "356B35", "leafL": "79B85A", "pine": "2E5A40", "trunk": "6E4E34",
    # power plant / grid
    "pylon": "A9B2BA", "wire": "3A4048", "insul": "DCE6EE", "solar": "24406A", "solarF": "B9C2CA",
    "turbine": "EEF1F3", "battery": "D9DEE3", "batt_strip": "7DF2A0", "smoke": "D6D8DA", "steam": "E9ECEE",
    # vehicles (generic utility yellow; liveries are shader uniforms)
    "truck": "F2B71E", "truckD": "C48E12", "boom": "E8E9EA", "tyre": "1E1F22", "carbody": "FFFFFF",
    "carglass": "2A3A4C", "l_head": "FFF2D6",
    # lit (w_* = district windows with the ripple; e_* = district lamps with the ripple; l_* = always lit)
    "e_lamp": "FFD9A0", "e_pool": "4C5057", "l_white": "F4F7FF", "l_red": "FF3B30", "l_amber": "FFB020",
    "l_cyan": "5FE6FF", "l_green": "7DF2A0", "l_warm": "FFE2B0", "cyan": "39C6E0",
    # sky dome (r = height factor, the shader paints the gradient)
    "sky": "FFFFFF",
}
# window walls: "w_<wall colour>" = that wall colour with the window code (the shader paints the windows by world position)
WALLS = ("brick", "brickD", "brickL", "plaster", "plasterY", "plasterP", "plasterB", "plasterG", "plasterW", "concrete",
         "concreteL", "concreteD", "stone", "sandstone", "glass", "glassD", "steel", "offwhite", "white", "lgrey", "grey",
         "galv", "cream", "navy", "teal")
for _k in WALLS:
    PALETTE["w_" + _k] = PALETTE[_k]
GLOSS = {"steel", "steelD", "glass", "glassD", "galv", "solar", "copper", "gold", "carglass", "battery", "turbine"}
# Material codes = alpha (index round(a*20)); see assets/art/vc_world.gdshaderinc
C_MATTE, C_GLOSS, C_ASPHALT, C_GROUND, C_ROOF, C_WATER, C_RESERVOIR = 1.0, .9, .8, .7, .6, .5, .45
C_WINDOW, C_LAMP, C_LIT, C_LINE, C_SKY, C_CYAN, C_PLANTLIT, C_FOLIAGE, C_SMOKE, C_BATT = \
    .4, .35, .3, .25, .2, .15, .1, .05, .55, .65
C_STREET, C_LIVERY, C_PAINT, C_PYLON = .75, .85, .95, .85   # livery / paint: prop shader; pylon: world shader
CODE = {"truckD": .8, "asphalt": C_ASPHALT, "asphaltD": C_ASPHALT, "e_pool": C_STREET, "truck": C_LIVERY, "carbody": C_PAINT, "ground": C_GROUND, "grass": C_GROUND,
        "grassD": C_GROUND, "field": C_GROUND, "wheat": C_GROUND, "meadow": C_GROUND, "plough": C_GROUND, "sand": C_GROUND, "rock": C_GROUND,
        "roofL": C_ROOF, "roofK": C_ROOF, "water": C_WATER, "waterD": C_WATER, "pool": C_WATER, "wire": C_LINE, "sky": C_SKY,
        "cyan": C_CYAN, "leaf": C_FOLIAGE, "leafD": C_FOLIAGE, "leafL": C_FOLIAGE, "pine": C_FOLIAGE,
        "smoke": C_SMOKE, "steam": C_SMOKE, "batt_strip": C_BATT, "pylon": C_PYLON}
MATERIALS = ("vc_world", "vc_prop", "vc_blade")
SHADER_OF = {"vc_world": "Assets/Art/Shaders/VcWorld.shader (+ VcWorldLow.shader)",
             "vc_prop": "Assets/Art/Shaders/VcProp.shader (+ VcPropLow.shader)",
             "vc_blade": "Assets/Art/Shaders/VcBlades.shader"}
ROUGH = {"vc_world": .8, "vc_prop": .6, "vc_blade": .5}


def code_of(key):
    if key in CODE:
        return CODE[key]
    if key.startswith("w_"):
        return C_WINDOW
    if key.startswith("e_"):
        return C_LAMP
    if key.startswith("l_"):
        return C_LIT
    return C_GLOSS if key in GLOSS else C_MATTE


def _linear(v):
    return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4


def _srgb(v):
    v = max(0.0, min(1.0, v))
    return v * 12.92 if v <= .0031308 else 1.055 * v ** (1 / 2.4) - .055


def rgb(key):
    h = PALETTE[key]
    return tuple(_linear(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4))


def material(name):
    """The one shared vertex-colour material per model (Color Attribute 'Col' -> base colour). The look lives in the
    game's shaders (alpha = material code); Blender only needs a readable preview."""
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    mat.use_backface_culling = True
    try:
        mat.use_nodes = True
    except Exception:
        pass
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    attr = nt.nodes.new("ShaderNodeVertexColor")
    attr.layer_name = "Col"
    gamma = nt.nodes.new("ShaderNodeGamma")       # COLOR_0 stores sRGB (Godot shaders convert when needed)
    gamma.inputs["Gamma"].default_value = 2.2
    nt.links.new(attr.outputs["Color"], gamma.inputs["Color"])
    nt.links.new(gamma.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = ROUGH[name]
    bsdf.inputs["Metallic"].default_value = 0.0
    return mat


def rng(*seed):
    """Deterministic RNG per model / district (string hashing is randomised per process: never hash(str))."""
    s = 0
    for part in seed:
        for ch in str(part):
            s = (s * 131 + ord(ch)) & 0xFFFFFFFF
    return random.Random(s)


def rot_matrix(rot):
    if not rot:
        return Matrix.Identity(4)
    return Euler(tuple(math.radians(a) for a in rot), "XYZ").to_matrix().to_4x4()


def mtx(c=(0, 0, 0), s=(1, 1, 1), rot=None):
    return Matrix.Translation(Vector(c)) @ rot_matrix(rot) @ Matrix.Diagonal((*s, 1))


# Godot axes for skip letters: t=+Y b=-Y f=+Z (front) k=-Z (back) r=+X l=-X
_SKIP = {"t": (1, 1), "b": (1, -1), "f": (2, 1), "k": (2, -1), "r": (0, 1), "l": (0, -1)}
G2B = Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))  # Godot -> Blender

# Fixed cosine-weighted hemisphere directions (deterministic AO): golden-angle spiral, 14 rays.
_AO_DIRS = []
for _i in range(14):
    _u = (_i + .5) / 14
    _r = math.sqrt(_u)
    _a = _i * 2.399963
    _AO_DIRS.append((_r * math.cos(_a), math.sqrt(max(0.0, 1 - _u)), _r * math.sin(_a)))


def _tangent_frame(n):
    ref = Vector((0, 1, 0)) if abs(n.y) < .9 else Vector((1, 0, 0))
    t = n.cross(ref).normalized()
    return t, n.cross(t).normalized()


def _hash01(*vals):
    h = 0
    for v in vals:
        h = (h * 1000003) ^ int(round(v * 1000.0))
        h &= 0xFFFFFFFF
    h ^= h >> 13
    h = (h * 0x5BD1E995) & 0xFFFFFFFF
    h ^= h >> 15
    return (h & 0xFFFF) / 65535.0


class Model:
    """Accumulates primitives into one bmesh (Godot space); every face stores a palette key."""

    def __init__(self, name, material="vc_world"):
        self.name = name
        self.material = material
        self.bm = bmesh.new()
        self.fu = self.bm.faces.layers.float.new("fu")
        self.fv = self.bm.faces.layers.float.new("fv")
        self.fw = self.bm.faces.layers.float.new("fw")
        self.fz = self.bm.faces.layers.float.new("fz")
        self.cur_uv = (0.0, 0.0)
        self.cur_uv2 = (0.0, 0.0)
        self.uvfn = None          # (key, (u, v), godot position) -> (u, v) per corner (districts: ripple distance)
        self.keys = []
        self.xf = Matrix.Identity(4)
        self.ymin = 0.0
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.belt_faces = set()
        self.jitter = {}          # key -> brightness jitter (+/-) per face (wood planks, stone, rice)
        self.ao_strength = .55
        self.ao_distance = None   # None = from model size
        self.ground_occludes = True
        self.meta = {}

    # ------------------------------------------------------------------ transform stack
    @contextmanager
    def at(self, pos=(0, 0, 0), ry=0.0, rot=None, scale=1.0):
        old = self.xf
        s = (scale, scale, scale) if isinstance(scale, (int, float)) else scale
        r = rot if rot else (0, ry, 0)
        self.xf = old @ mtx(pos, s, r)
        try:
            yield self
        finally:
            self.xf = old

    def P(self, p):
        return self.xf @ Vector(p)

    @contextmanager
    def data(self, u=None, v=None, w=None, z=None):
        """Faces made inside get UV0 = (u, v) and UV1 = (w, z) (see the module doc); None keeps the outer value."""
        old, old2 = self.cur_uv, self.cur_uv2
        self.cur_uv = (old[0] if u is None else float(u), old[1] if v is None else float(v))
        self.cur_uv2 = (old2[0] if w is None else float(w), old2[1] if z is None else float(z))
        try:
            yield self
        finally:
            self.cur_uv, self.cur_uv2 = old, old2

    # ------------------------------------------------------------------ internals
    def _slot(self, key):
        assert key in PALETTE, key
        if key not in self.keys:
            self.keys.append(key)
        return self.keys.index(key)

    def _finish(self, faces, key, skip=""):
        slot = self._slot(key) if isinstance(key, str) else None
        drop = []
        for i, f in enumerate(faces):
            f.material_index = slot if slot is not None else self._slot(key(i, f))
            f.smooth = False
            f[self.fu], f[self.fv] = self.cur_uv
            f[self.fw], f[self.fz] = self.cur_uv2
            if skip:
                f.normal_update()
                for letter in skip:
                    axis, sign = _SKIP[letter]
                    if f.normal[axis] * sign > .9:
                        drop.append(f)
                        break
        if drop:
            bmesh.ops.delete(self.bm, geom=drop, context="FACES")
        return self

    def _faces_of(self, verts):
        faces = {}
        for v in verts:
            for f in v.link_faces:
                faces[id(f)] = f
        return sorted(faces.values(), key=lambda f: tuple(round(c, 4) for c in f.calc_center_median()))

    # ------------------------------------------------------------------ raw faces
    def poly(self, pts, key, two_sided=False, uv=None):
        """One planar polygon (points in local space, CCW seen from the visible side).
        uv: list of (u, v) per point (the car's emblem decals)."""
        vs = [self.bm.verts.new(self.P(p)) for p in pts]
        f = self.bm.faces.new(vs)
        faces = [f]
        if uv:
            for loop, t in zip(f.loops, uv):
                loop[self.uv].uv = t
            self.belt_faces.add(f)
        if two_sided:
            vs2 = [self.bm.verts.new(v.co) for v in reversed(vs)]
            faces.append(self.bm.faces.new(vs2))
        return self._finish(faces, key)

    def tri_fan(self, pts, key):
        return self.poly(pts, key)

    # ------------------------------------------------------------------ primitives
    def box(self, c, s, key, rot=None, skip="", bevel=0.0, keys=None):
        """Centre c, size s (Godot axes). skip: faces to omit (t b f k r l, local after rot).
        keys: dict letter -> palette key for individual sides (e.g. {"t": "kinako"})."""
        res = bmesh.ops.create_cube(self.bm, size=1.0, matrix=self.xf @ mtx(c, s, rot))
        verts = res["verts"]
        if bevel:
            edges = list({e for v in verts for e in v.link_edges})
            out = bmesh.ops.bevel(self.bm, geom=verts + edges, offset=min(bevel, min(s) / 3),
                                  segments=1, affect="EDGES", profile=.5, clamp_overlap=True)
            verts = out["verts"] + [v for v in verts if v.is_valid]
        faces = self._faces_of(verts)
        if keys:
            inv = (self.xf @ rot_matrix(rot)).to_3x3().inverted()

            def pick(i, f):
                f.normal_update()
                n = (inv @ f.normal).normalized()
                for letter, k in keys.items():
                    axis, sign = _SKIP[letter]
                    if n[axis] * sign > .7:
                        return k
                return key
            return self._finish(faces, pick, skip)
        return self._finish(faces, key, skip)

    def boxr(self, x0, y0, z0, x1, y1, z1, key, skip="", bevel=0.0, keys=None):
        return self.box(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2),
                        (abs(x1 - x0), abs(y1 - y0), abs(z1 - z0)), key, None, skip, bevel, keys)

    def lathe(self, profile, key, n=8, c=(0, 0, 0), rot=None, sx=1.0, sz=1.0, phase=0.0, keys=None,
              cap_bottom=True, cap_top=True, flip=False):
        """Surface of revolution around local +Y. profile = [(radius, y), ...] bottom -> top.
        radius 0 at an end makes a pole. keys(i_segment, j_band) -> palette key for stripes."""
        bm = self.bm
        base = self.xf @ mtx(c, (1, 1, 1), rot)
        rings = []
        for r, y in profile:
            if r <= 1e-6:
                rings.append([bm.verts.new(base @ Vector((0, y, 0)))])
            else:
                ring = []
                for i in range(n):
                    a = phase + 2 * math.pi * i / n
                    ring.append(bm.verts.new(base @ Vector((r * math.sin(a) * sx, y, r * math.cos(a) * sz))))
                rings.append(ring)
        faces, fkeys = [], []
        for j in range(len(rings) - 1):
            lo, hi = rings[j], rings[j + 1]
            for i in range(n):
                i2 = (i + 1) % n
                if len(lo) == 1 and len(hi) == 1:
                    continue
                if len(lo) == 1:
                    f = bm.faces.new((lo[0], hi[i2], hi[i]))
                elif len(hi) == 1:
                    f = bm.faces.new((lo[i], lo[i2], hi[0]))
                else:
                    f = bm.faces.new((lo[i], lo[i2], hi[i2], hi[i]))
                faces.append(f)
                fkeys.append(keys(i, j) if keys else key)
        if cap_bottom and len(rings[0]) > 2:
            faces.append(bm.faces.new(list(reversed(rings[0]))))
            fkeys.append(keys(-1, -1) if keys else key)
        if cap_top and len(rings[-1]) > 2:
            faces.append(bm.faces.new(rings[-1]))
            fkeys.append(keys(-1, len(rings)) if keys else key)
        if (profile[-1][1] < profile[0][1]) != flip:
            bmesh.ops.reverse_faces(bm, faces=faces)
        return self._finish(faces, lambda i, f: fkeys[i])

    def cyl(self, base, r, h, key, n=8, r2=None, rot=None, caps=True, cap_bottom=None, phase=0.0, sx=1, sz=1,
            keys=None):
        prof = [(r, 0.0), (r if r2 is None else r2, h)]
        cb = caps if cap_bottom is None else cap_bottom
        return self.lathe(prof, key, n, base, rot, sx, sz, phase, cap_bottom=cb, cap_top=caps, keys=keys)

    def cone(self, base, r, h, key, n=8, rot=None, cap=True, phase=0.0):
        return self.lathe([(r, 0.0), (0, h)], key, n, base, rot, phase=phase, cap_bottom=cap)

    def ell(self, c, r, key, n=8, rings=4, rot=None, top=1.0, bottom=-1.0, phase=0.0, keys=None, cap=True):
        """Ellipsoid (rx, ry, rz) centred at c; top/bottom clip in [-1, 1] of ry (domes, caps)."""
        r = (r, r, r) if isinstance(r, (int, float)) else r
        prof = []
        a0, a1 = math.asin(max(-1, bottom)), math.asin(min(1, top))
        for k in range(rings + 1):
            a = a0 + (a1 - a0) * k / rings
            rad = math.cos(a)
            prof.append((0 if rad < 1e-4 else rad * r[0], math.sin(a) * r[1]))
        return self.lathe(prof, key, n, c, rot, 1.0, r[2] / r[0], phase, keys=keys, cap_bottom=cap)

    def bar(self, a, b, r, key, n=6, r2=None, caps=True, phase=0.0):
        a, b = Vector(a), Vector(b)
        d = b - a
        q = d.to_track_quat("Y", "Z")
        m = Matrix.Translation(a) @ q.to_matrix().to_4x4()
        old = self.xf
        self.xf = old @ m
        try:
            self.lathe([(r, 0.0), (r if r2 is None else r2, d.length)], key, n, phase=phase,
                       cap_bottom=caps, cap_top=caps)
        finally:
            self.xf = old
        return self

    def tube(self, pts, radii, key, n=6, cap_start=True, cap_end=True, phase=0.0, keys=None, flat=None):
        """Joined polyline tube (limbs, ropes, lantern strings). flat=(sx, sy) squashes the ring."""
        bm = self.bm
        pts = [Vector(p) for p in pts]
        rings = []
        prev_side = None
        for k, p in enumerate(pts):
            if k == 0:
                d = pts[1] - pts[0]
            elif k == len(pts) - 1:
                d = pts[-1] - pts[-2]
            else:
                d = (pts[k + 1] - pts[k]).normalized() + (pts[k] - pts[k - 1]).normalized()
            d.normalize()
            ref = Vector((0, 0, 1)) if abs(d.z) < .9 else Vector((1, 0, 0))
            side = d.cross(ref).normalized() if prev_side is None else (prev_side - d * prev_side.dot(d)).normalized()
            prev_side = side
            up = d.cross(side).normalized()
            fx, fy = flat or (1, 1)
            ring = []
            for i in range(n):
                a = phase + 2 * math.pi * i / n
                off = side * math.cos(a) * radii[k] * fx + up * math.sin(a) * radii[k] * fy
                ring.append(bm.verts.new(self.P(p + off)))
            rings.append(ring)
        faces, fk = [], []
        for j in range(len(rings) - 1):
            for i in range(n):
                i2 = (i + 1) % n
                faces.append(bm.faces.new((rings[j][i], rings[j][i2], rings[j + 1][i2], rings[j + 1][i])))
                fk.append(keys(i, j) if keys else key)
        if cap_start:
            faces.append(bm.faces.new(list(reversed(rings[0]))))
            fk.append(keys(-1, -1) if keys else key)
        if cap_end:
            faces.append(bm.faces.new(rings[-1]))
            fk.append(keys(-1, len(rings)) if keys else key)
        return self._finish(faces, lambda i, f: fk[i])

    def prism(self, pts, a0, a1, key, plane="xy", skip="", side_key=None):
        """Extrude a 2D polygon. plane xy: pts=(x, y) along Z a0..a1 (front-facing signs);
        xz: pts=(x, z) along Y (floor shapes); zy: pts=(z, y) along X (side profiles)."""
        def to3(u, v, w):
            if plane == "xy":
                return (u, v, w)
            if plane == "xz":
                return (u, w, v)
            return (w, v, u)
        bm = self.bm
        lo = [bm.verts.new(self.P(to3(u, v, a0))) for u, v in pts]
        hi = [bm.verts.new(self.P(to3(u, v, a1))) for u, v in pts]
        caps = [bm.faces.new(lo), bm.faces.new(list(reversed(hi)))]
        sides = []
        n = len(pts)
        for i in range(n):
            j = (i + 1) % n
            sides.append(bm.faces.new((lo[i], lo[j], hi[j], hi[i])))
        bmesh.ops.recalc_face_normals(bm, faces=caps + sides)
        self._finish(caps, key, skip)
        if sides:
            self._finish([f for f in sides if f.is_valid], side_key or key, skip)
        return self

    def loft(self, rings, key, cap_start=True, cap_end=True, keys=None, recalc=None):
        """Skin a list of equal-length point rings (torsos, fish slices, bus bodies)."""
        bm = self.bm
        vr = [[bm.verts.new(self.P(p)) for p in ring] for ring in rings]
        n = len(rings[0])
        faces, fk = [], []
        for j in range(len(vr) - 1):
            for i in range(n):
                i2 = (i + 1) % n
                faces.append(bm.faces.new((vr[j][i], vr[j][i2], vr[j + 1][i2], vr[j + 1][i])))
                fk.append(keys(i, j) if keys else key)
        if cap_start:
            faces.append(bm.faces.new(list(reversed(vr[0]))))
            fk.append(keys(-1, -1) if keys else key)
        if cap_end:
            faces.append(bm.faces.new(vr[-1]))
            fk.append(keys(-1, len(vr)) if keys else key)
        if recalc if recalc is not None else (cap_start and cap_end):
            bmesh.ops.recalc_face_normals(bm, faces=faces)
        return self._finish(faces, lambda i, f: fk[i])

    def torus(self, c, R, r, key, nu=10, nv=4, rot=None, keys=None):
        bm = self.bm
        base = self.xf @ mtx(c, (1, 1, 1), rot)
        grid = []
        for i in range(nu):
            a = 2 * math.pi * i / nu
            ring = []
            for j in range(nv):
                b = 2 * math.pi * j / nv + math.pi / nv
                rr = R + r * math.cos(b)
                ring.append(bm.verts.new(base @ Vector((rr * math.cos(a), r * math.sin(b), rr * math.sin(a)))))
            grid.append(ring)
        faces, fk = [], []
        for i in range(nu):
            i2 = (i + 1) % nu
            for j in range(nv):
                j2 = (j + 1) % nv
                faces.append(bm.faces.new((grid[i][j], grid[i2][j], grid[i2][j2], grid[i][j2])))
                fk.append(keys(i) if keys else key)
        bmesh.ops.recalc_face_normals(bm, faces=faces)
        return self._finish(faces, lambda i, f: fk[i])

    def grid(self, x0, x1, z0, z1, nx, nz, height, key, y=0.0, xs=None, zs=None):
        bm = self.bm
        xs = xs or [x0 + (x1 - x0) * i / nx for i in range(nx + 1)]
        zs = zs or [z0 + (z1 - z0) * j / nz for j in range(nz + 1)]
        vs = [[bm.verts.new(self.P((x, y + height(x, z), z))) for x in xs] for z in zs]
        faces, fk = [], []
        for j in range(len(zs) - 1):
            for i in range(len(xs) - 1):
                f = bm.faces.new((vs[j][i], vs[j + 1][i], vs[j + 1][i + 1], vs[j][i + 1]))
                f.normal_update()
                if f.normal.y < 0:
                    f.normal_flip()
                faces.append(f)
                cx, cz = (xs[i] + xs[i + 1]) / 2, (zs[j] + zs[j + 1]) / 2
                fk.append(key(cx, cz) if callable(key) else key)
        return self._finish(faces, lambda i, f: fk[i])

    # ------------------------------------------------------------------ finish
    def triangles(self):
        return sum(len(f.verts) - 2 for f in self.bm.faces)

    def vcount(self):
        return len(self.bm.verts)

    def _clean(self, bm):
        loose = [v for v in bm.verts if not v.link_faces]
        if loose:
            bmesh.ops.delete(bm, geom=loose, context="VERTS")

    def _bake_colours(self):
        """Per-corner linear colour = palette colour x face jitter x ambient occlusion."""
        bm = self.bm
        bm.normal_update()
        bm.faces.ensure_lookup_table()
        tree = BVHTree.FromBMesh(bm)
        lo = Vector((min(v.co.x for v in bm.verts), min(v.co.y for v in bm.verts), min(v.co.z for v in bm.verts)))
        hi = Vector((max(v.co.x for v in bm.verts), max(v.co.y for v in bm.verts), max(v.co.z for v in bm.verts)))
        size = (hi - lo).length
        dist = self.ao_distance or max(.02, min(.7, size * .12))
        cols = {}
        for f in bm.faces:
            key = self.keys[f.material_index]
            base = Vector(rgb(key))
            j = self.jitter.get(key, 0.0)
            if j:
                c = f.calc_center_median()
                base = base * (1.0 + j * (2 * _hash01(c.x, c.y, c.z) - 1))
            glow = key.startswith("e_") or key.startswith("l_")
            alpha = code_of(key)
            n = f.normal.normalized() if f.normal.length > 1e-8 else Vector((0, 1, 0))
            t, b = _tangent_frame(n)
            centre = f.calc_center_median()
            per = []
            for loop in f.loops:
                if glow or self.ao_strength <= 0:
                    per.append((*base, alpha))
                    continue
                o = loop.vert.co.lerp(centre, .12) + n * min(.002, dist * .02)
                hit = 0
                for dx, dy, dz in _AO_DIRS:
                    d = (t * dx + n * dy + b * dz).normalized()
                    loc, _, _, _ = tree.ray_cast(o, d, dist)
                    if loc is not None:
                        hit += 1
                    elif self.ground_occludes and d.y < -1e-4 and (o.y - self.ymin_pre) / -d.y < dist:
                        hit += 1
                occ = hit / len(_AO_DIRS)
                k = 1.0 - self.ao_strength * occ ** 1.2
                per.append((base.x * k, base.y * k, base.z * k, alpha))
            cols[f.index] = per
        return cols

    def to_object(self, ground_pivot=True, origin_shift=None):
        """Bake vertex colours + AO, write the data UV, pivot (ground pivot = min Y -> 0, or an explicit origin_shift,
        (0, 0, 0) for city-space models), Godot -> Blender rotation. Returns (object, used palette keys)."""
        bm = self.bm
        self._clean(bm)
        self.ymin_pre = min(v.co.y for v in bm.verts)
        if origin_shift is not None:
            shift = Vector(origin_shift)
        else:
            shift = Vector((0, self.ymin_pre if ground_pivot else 0.0, 0))
        self.ymin = shift.y
        bm.faces.index_update()
        cols = self._bake_colours()
        uvs = {}
        uv2s = {f.index: (f[self.fw], f[self.fz]) for f in bm.faces}
        for f in bm.faces:
            key = self.keys[f.material_index]
            const = (f[self.fu], f[self.fv])
            per = []
            for loop in f.loops:
                if f in self.belt_faces:
                    t = loop[self.uv].uv
                    per.append((t[0], t[1]))
                elif self.uvfn is not None:
                    per.append(self.uvfn(key, const, loop.vert.co))
                else:
                    per.append(const)
            uvs[f.index] = per
        for v in bm.verts:
            v.co -= shift
        used_keys = sorted({self.keys[f.material_index] for f in bm.faces})
        for f in bm.faces:
            f.material_index = 0
        bmesh.ops.transform(bm, matrix=G2B, verts=bm.verts)
        bm.normal_update()
        mesh = bpy.data.meshes.new(self.name)
        bm.to_mesh(mesh)
        bm.free()
        attr = mesh.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
        uvl = mesh.uv_layers["UVMap"] if "UVMap" in mesh.uv_layers else mesh.uv_layers.new(name="UVMap")
        uvd = mesh.uv_layers.new(name="UVData")
        for poly in mesh.polygons:
            per = cols[poly.index]
            pu = uvs[poly.index]
            p2 = uv2s[poly.index]
            for k, li in enumerate(poly.loop_indices):
                c = per[k]
                attr.data[li].color = (_srgb(c[0]), _srgb(c[1]), _srgb(c[2]), c[3])
                uvl.data[li].uv = (pu[k][0], pu[k][1])
                uvd.data[li].uv = p2
        mesh.uv_layers.active = uvl
        mesh.color_attributes.active_color_name = "Col"
        mesh.materials.append(material(self.material))
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        return obj, used_keys
