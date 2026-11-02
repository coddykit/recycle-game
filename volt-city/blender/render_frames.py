"""Volt City P2 look-dev frames (Unity is not installed yet, so Blender Eevee renders the city):
    blender -b --factory-startup -P blender/render_frames.py -- <out dir> [moments] [tiers]   (blender/frames.sh)

Imports the built GLBs (unity/Assets/Art/Models), rebuilds the Unity shaders' look as Eevee node materials (same
material codes, same window / ripple / corridor / water-by-height / pool rules as Shaders/VcWorldCore.hlsl), places
the instanced props from layout.json and renders portrait 1080 x 2340 frames:
  tiers:   low    = no shadows, no specular, diffuse only, 85 % internal resolution (upscaled), 50 % trees, 20 cars
           medium = sun shadows, specular, full trees, 40 cars
           before = the plain import look (vertex colour only: no surfaces, windows, ripple, light pools) — the
                    "before" side of the compare sheets
  moments: dusk (Riverside lighting up mid-ripple), night (Airport Flats not hooked up = dark; Tech line overheating),
           storm (wet, a line down -> Riverside dark, crews with the bucket up at two faults)
Camera = Shaders' portrait diorama camera (Runtime/VcCameraFit.cs): 55 deg tilt, 35 deg vertical FOV, fitted to the
district boxes. Fog = exponential depth fog of the light curve (compositor, from the mist pass).
"""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[1]
MODELS = ROOT / "unity/Assets/Art/Models"
SURFACE = ROOT / "unity/Assets/Art/Textures/surface.png"
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = Path(ARGS[0]) if ARGS else Path("/Volumes/BuildSSD/game-builds/volt-city/p2")
MOMENTS = ARGS[1].split(",") if len(ARGS) > 1 else ["dusk", "night", "storm"]
TIERS = ARGS[2].split(",") if len(ARGS) > 2 else ["low", "medium", "before"]
W, H = 1080, 2340
LAY = json.loads((MODELS / "layout.json").read_text())
MAN = json.loads((MODELS / "manifest.json").read_text())

# Same values as Runtime/VcLightCurves.cs (sun pitch / yaw in Unity degrees: yaw 0 = towards +Z north)
CURVES = {
    "dusk": {"sun": (14.0, 62.0), "sun_color": (1.0, 0.56, 0.30), "sun_energy": 1.5, "ambient": (0.36, 0.36, 0.62),
             "ambient_energy": 0.66, "fog": (0.74, 0.48, 0.44), "fog_density": 0.00024, "sky_top": (0.17, 0.22, 0.45),
             "sky_horizon": (0.98, 0.58, 0.36), "night": 0.6, "wet": 0.0, "window_glow": 2.5},
    "night": {"sun": (40.0, -140.0), "sun_color": (0.56, 0.66, 0.95), "sun_energy": 0.42, "ambient": (0.26, 0.33, 0.52),
              "ambient_energy": 0.78, "fog": (0.07, 0.12, 0.21), "fog_density": 0.0002, "sky_top": (0.03, 0.06, 0.14),
              "sky_horizon": (0.12, 0.20, 0.33), "night": 1.0, "wet": 0.0, "window_glow": 2.7},
    "storm": {"sun": (32.0, -20.0), "sun_color": (0.70, 0.76, 0.86), "sun_energy": 0.5, "ambient": (0.32, 0.38, 0.50),
              "ambient_energy": 0.66, "fog": (0.30, 0.35, 0.43), "fog_density": 0.00046, "sky_top": (0.18, 0.22, 0.29),
              "sky_horizon": (0.40, 0.44, 0.51), "night": 0.75, "wet": 1.0, "window_glow": 2.6},
    "day": {"sun": (48.0, -28.0), "sun_color": (1.0, 0.95, 0.88), "sun_energy": 1.25, "ambient": (0.62, 0.70, 0.84),
            "ambient_energy": 0.62, "fog": (0.70, 0.80, 0.92), "fog_density": 0.00016, "sky_top": (0.32, 0.54, 0.86),
            "sky_horizon": (0.80, 0.87, 0.95), "night": 0.0, "wet": 0.0, "window_glow": 2.0},
}
# per moment: district ripple (front, target), corridor states (index -> (state, flow)), plants, faults, trucks
# camera fit per moment = the game's "fit the lit districts" view at that point of a run
VIEWS = {"dusk": ["old_harbor", "riverside", "market_square"],
         "night": ["riverside", "market_square", "tech_park"],
         "ripple": ["riverside"],
         "storm": ["old_harbor", "riverside", "stadium_row", "market_square"],
         "city": None}
BASE = {"city": "night", "ripple": "night"}
EMISSION_GAIN = 0.75      # Eevee (Standard view) vs URP's neutral tonemap; Unity values stay as in VcLightCurves
STATES = {
    "dusk": {"ripple": {"riverside": (0.55, 1.0)}, "corridors": {}, "faults": [], "boom": [],
             "plants": {"steam": 2, "peaker": 1, "hydro": 2, "wind": 2, "solar": 2, "battery": 1}},
    "night": {"ripple": {"airport_flats": (2.0, 0.0)}, "corridors": {5: (1, 1.0), 7: (3, 0.0), 12: (3, 0.0)},
              "faults": [], "boom": [],
              "plants": {"steam": 3, "peaker": 2, "hydro": 2, "wind": 2, "solar": 0, "battery": 2}},
    "ripple": {"ripple": {"riverside": (0.5, 1.0)}, "corridors": {}, "faults": [], "boom": [],
               "plants": {"steam": 2, "peaker": 1, "hydro": 1, "wind": 1, "solar": 0, "battery": 1}},
    "storm": {"ripple": {"riverside": (2.0, 0.0)}, "corridors": {1: (2, 0.0), 6: (1, 1.0)},
              "faults": [("river_line", .45), ("stadium_line", .5)], "boom": [0],
              "plants": {"steam": 3, "peaker": 2, "hydro": 3, "wind": 3, "solar": 2, "battery": 2}},
}


def U2B(p):
    """Unity (x, y, z north) -> Blender (x, y north, z up)."""
    return Vector((p[0], p[2], p[1]))


# ------------------------------------------------------------------------------------------------ node expression kit
class NB:
    def __init__(self, nt):
        self.nt = nt
        self.x = 0

    def node(self, kind, **props):
        n = self.nt.nodes.new(kind)
        for k, v in props.items():
            setattr(n, k, v)
        self.x += 1
        n.location = (self.x % 40 * 200, -(self.x // 40) * 300)
        return n


class E:
    """Scalar / vector math expression: E(nb, socket); + - * / via Math / VectorMath nodes."""

    def __init__(self, nb, sock, vec=False):
        self.nb, self.s, self.vec = nb, sock, vec

    @staticmethod
    def wrap(nb, v, vec=False):
        if isinstance(v, E):
            return v
        if isinstance(v, (tuple, list)):
            n = nb.node("ShaderNodeCombineXYZ")
            for i in range(3):
                n.inputs[i].default_value = v[i]
            return E(nb, n.outputs[0], True)
        n = nb.node("ShaderNodeValue")
        n.outputs[0].default_value = float(v)
        return E(nb, n.outputs[0], False)

    def _op(self, other, op, rev=False):
        o = E.wrap(self.nb, other)
        a, b = (o, self) if rev else (self, o)
        if a.vec or b.vec:
            n = self.nb.node("ShaderNodeVectorMath", operation=op)
            self.nb.nt.links.new(a.v3().s, n.inputs[0])
            self.nb.nt.links.new(b.v3().s, n.inputs[1])
            return E(self.nb, n.outputs[0], True)
        n = self.nb.node("ShaderNodeMath", operation=op)
        self.nb.nt.links.new(a.s, n.inputs[0])
        self.nb.nt.links.new(b.s, n.inputs[1])
        return E(self.nb, n.outputs[0], False)

    def v3(self):
        if self.vec:
            return self
        n = self.nb.node("ShaderNodeCombineXYZ")
        for i in range(3):
            self.nb.nt.links.new(self.s, n.inputs[i])
        return E(self.nb, n.outputs[0], True)

    def __add__(self, o): return self._op(o, "ADD")
    def __radd__(self, o): return self._op(o, "ADD", True)
    def __sub__(self, o): return self._op(o, "SUBTRACT")
    def __rsub__(self, o): return self._op(o, "SUBTRACT", True)
    def __mul__(self, o): return self._op(o, "MULTIPLY")
    def __rmul__(self, o): return self._op(o, "MULTIPLY", True)
    def __truediv__(self, o): return self._op(o, "DIVIDE")

    def f1(self, op, *more):
        n = self.nb.node("ShaderNodeMath", operation=op)
        self.nb.nt.links.new(self.s, n.inputs[0])
        for i, m in enumerate(more):
            self.nb.nt.links.new(E.wrap(self.nb, m).s, n.inputs[i + 1])
        return E(self.nb, n.outputs[0], False)

    def comp(self, i):
        n = self.nb.node("ShaderNodeSeparateXYZ")
        self.nb.nt.links.new(self.s, n.inputs[0])
        return E(self.nb, n.outputs[i], False)


def mix(nb, a, b, t):
    a, b, t = E.wrap(nb, a), E.wrap(nb, b), E.wrap(nb, t)
    return a + (b - a) * t


def clamp01(e):
    return e.f1("MAXIMUM", 0.0).f1("MINIMUM", 1.0)


def smoothstep(nb, e0, e1, x):
    t = clamp01((E.wrap(nb, x) - e0) / (e1 - e0))
    return t * t * (3.0 - 2.0 * t)


def step(nb, edge, x):          # x >= edge -> 1
    return E.wrap(nb, x).f1("GREATER_THAN", E.wrap(nb, edge) - 1e-6)


def eq(e, k):
    return e.f1("COMPARE", float(k), 0.5)


def hash21(nb, p2x, p2y):
    """Same as VcHash21 (frac-based) built from math nodes."""
    x = (p2x * 123.34).f1("FRACT")
    y = (p2y * 456.21).f1("FRACT")
    d = x * (x + 45.32) + y * (y + 45.32)
    x = x + d
    y = y + d
    return (x * y).f1("FRACT")


# ------------------------------------------------------------------------------------------------ the material
STYLES = {1: (3.2, 3.2, .45, .45, .32, .62, 1.0), 2: (3.0, 3.6, .82, .42, .3, .7, .55),
          3: (1.6, 3.8, .86, .78, .1, .58, .3), 4: (4.0, 6.0, .7, .16, .74, .38, .7),
          5: (3.2, 3.4, .5, .45, .32, .6, 1.0), 6: (2.4, 2.0, .92, .12, .5, .96, 0.0),
          7: (3.0, 3.4, .62, .5, .3, .88, .2), 8: (4.2, 3.2, .5, .56, .24, .46, 1.0),
          9: (2.2, 11.0, .9, .78, .08, .95, .6), 10: (6.0, 4.0, .7, .3, .55, .92, .6)}


def world_material(name, kind, tier, g):
    """Eevee version of Shaders/VcWorldCore.hlsl for one mesh kind (0 terrain, 1 district, 2 grid, 3 plants).
    g = light-curve globals; tier 'before' = vertex colour only."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    nb = NB(nt)
    out = nb.node("ShaderNodeOutputMaterial")
    bsdf = nb.node("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    attr = nb.node("ShaderNodeAttribute", attribute_name="Color", attribute_type="GEOMETRY")
    gamma = nb.node("ShaderNodeGamma")
    gamma.inputs[1].default_value = 2.2
    nt.links.new(attr.outputs["Color"], gamma.inputs[0])
    c0 = E(nb, gamma.outputs[0], True)
    alpha = E(nb, attr.outputs["Alpha"])
    if tier == "before":
        nt.links.new(c0.s, bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = .8
        return mat
    code = (alpha * 20.0 + 0.5).f1("FLOOR")
    uv0 = nb.node("ShaderNodeUVMap", uv_map="UVMap")
    uv1 = nb.node("ShaderNodeUVMap", uv_map="UVMap.001")
    u0 = E(nb, uv0.outputs[0], True)
    u1 = E(nb, uv1.outputs[0], True)
    ux, uy = u0.comp(0), u0.comp(1)
    style, base = (u1.comp(0) + 0.5).f1("FLOOR"), u1.comp(1)
    geo = nb.node("ShaderNodeNewGeometry")
    pos = E(nb, geo.outputs["Position"], True)
    nrm = E(nb, geo.outputs["Normal"], True)
    # Blender (x, y north, z up) -> the shader's world (x, y up, z north)
    wx, wy, wz = pos.comp(0), pos.comp(2), pos.comp(1)
    nx, ny, nz = nrm.comp(0), nrm.comp(2), nrm.comp(1)
    img = bpy.data.images.load(str(SURFACE), check_existing=True)
    img.colorspace_settings.name = "Non-Color"

    def tex(scale):
        t = nb.node("ShaderNodeTexImage", image=img, interpolation="Cubic", extension="REPEAT")
        v = nb.node("ShaderNodeCombineXYZ")
        nt.links.new((wx * scale).s, v.inputs[0])
        nt.links.new((wz * scale).s, v.inputs[1])
        nt.links.new(v.outputs[0], t.inputs[0])
        return E(nb, t.outputs[0], True)
    tx = tex(0.013)
    tf = tex(0.21)
    night = E.wrap(nb, g["night"])
    wet = E.wrap(nb, g["wet"])
    sky_top = E.wrap(nb, g["sky_top"])
    sky_hor = E.wrap(nb, g["sky_horizon"])
    warm = E.wrap(nb, (1.0, 0.62, 0.26))
    cool = E.wrap(nb, (0.86, 0.92, 1.0))
    cyan = E.wrap(nb, (0.30, 0.88, 1.0))
    obj_r = nb.node("ShaderNodeAttribute", attribute_name="ripple", attribute_type="OBJECT")
    rip = E(nb, obj_r.outputs["Vector"], True)
    front, target = rip.comp(0), rip.comp(1)

    def on_at(u):
        lit = (front - u).f1("GREATER_THAN", 0.0)
        return mix(nb, 1.0 - target, target, lit)

    branches = []           # (mask, albedo vec, emission vec, rough, spec)
    # --- window wall (8)
    if kind == 1:
        cellw = E.wrap(nb, 3.2)
        floorh = E.wrap(nb, 3.2)
        fw = E.wrap(nb, .45)
        fh = E.wrap(nb, .45)
        sill = E.wrap(nb, .32)
        occ = E.wrap(nb, .62)
        wrm = E.wrap(nb, 1.0)
        for s, (a, b, c, d, e, f, w) in STYLES.items():
            if s == 1:
                continue
            m = eq(style, s)
            cellw = mix(nb, cellw, a, m)
            floorh = mix(nb, floorh, b, m)
            fw = mix(nb, fw, c, m)
            fh = mix(nb, fh, d, m)
            sill = mix(nb, sill, e, m)
            occ = mix(nb, occ, f, m)
            wrm = mix(nb, wrm, w, m)
        # facade tangent from the world normal (any yaw)
        tl = (nz * nz + nx * nx).f1("SQRT").f1("MAXIMUM", 1e-4)
        tsx, tsz = (E.wrap(nb, 0.0) - nz) / tl, nx / tl
        s_ = (wx * tsx + wz * tsz) / cellw + uy * 7.0
        h_ = (wy - base) / floorh
        mx = (s_.f1("FRACT") - 0.5).f1("ABSOLUTE").f1("LESS_THAN", fw * 0.5)
        my = ((h_ - sill - fh * 0.5 + 0.5).f1("FRACT") - 0.5).f1("ABSOLUTE").f1("LESS_THAN", fh * 0.5)
        plinth = smoothstep(nb, .55, .75, wy - base)
        vert = ny.f1("ABSOLUTE").f1("LESS_THAN", 0.5) * (style.f1("GREATER_THAN", 0.5))
        mask = mx * my * plinth * vert
        cx, cy = s_.f1("FLOOR"), h_.f1("FLOOR")
        hsh = hash21(nb, cx + uy * 31.0, cy + uy * 31.0)
        litw = step(nb, 1.0 - occ, hsh)
        on = on_at(ux + hsh * 0.12)
        tint = mix(nb, cool, warm, clamp01(wrm + (hash21(nb, cx + 7.0, cy + 7.0) - 0.5) * 0.3))
        tint = mix(nb, tint, mix(nb, cyan, cool, .45), eq(style, 6))
        tone = hash21(nb, cx + 11.0, cy + 11.0) * 0.5 + 0.75
        glass_day = mix(nb, E.wrap(nb, (.20, .27, .34)), sky_top * 1.3, .35)
        glass = mix(nb, glass_day, E.wrap(nb, (.035, .05, .08)), night)
        wall = c0 * (tf.comp(0) * .14 + .9)
        alb = mix(nb, wall, glass, mask) * mix(nb, 1.0, mix(nb, .55, 1.0, on), night)
        em = tint * (mask * litw * on * night * tone * g["window_glow"] * EMISSION_GAIN)
        branches.append((eq(code, 8), alb, em, .6, .3))
    # --- ground (14): water / sand / grass / rock by height
    if kind == 0:
        water = wy.f1("LESS_THAN", -0.08)
        depth = clamp01((E.wrap(nb, 0.0) - wy) / 6.0)
        wc = mix(nb, E.wrap(nb, (.18, .46, .52)), E.wrap(nb, (.07, .22, .34)), depth) * (tx.comp(1) * .16 + .9)
        wc = wc * mix(nb, 1.0, E.wrap(nb, (.30, .36, .55)), night)
        grass = c0 * (tx.comp(1) * .3 + .82) * (tf.comp(0) * .14 + .93)
        sand = E.wrap(nb, (.80, .71, .52)) * (tf.comp(0) * .15 + .9)
        rock = E.wrap(nb, (.50, .48, .44)) * (tx.comp(0) * .25 + .85)
        gnd = mix(nb, grass, sand, 1.0 - smoothstep(nb, .35, .95, wy))
        gnd = mix(nb, gnd, rock, smoothstep(nb, .78, .6, ny))
        gnd = gnd * mix(nb, 1.0, E.wrap(nb, (.40, .46, .70)), night) * mix(nb, 1.0, .85, wet)
        alb = mix(nb, gnd, wc, water)
        em = sky_hor * ((tx.comp(2) * .05 + .05) * (1.0 - night * .6)) * water
        branches.append((eq(code, 14), alb, em, mix(nb, .95, .1, water), mix(nb, .06, .5, water)))
        # road asphalt (16) with lane markings
        asp = c0 * (tf.comp(0) * .24 + .84) * mix(nb, 1.0, .68, wet) * mix(nb, 1.0, .82, night)
        dash = step(nb, .5, (uy / 9.0).f1("FRACT")) * (1.0 - smoothstep(nb, .12, .2, ux.f1("ABSOLUTE")))
        edge = smoothstep(nb, 3.55, 3.7, ux.f1("ABSOLUTE")) * (1.0 - smoothstep(nb, 3.85, 4.0, ux.f1("ABSOLUTE")))
        alb = mix(nb, asp, E.wrap(nb, (.86, .84, .76)) * mix(nb, 1.0, .6, night), clamp01(dash * .8 + edge * .7))
        branches.append((eq(code, 16), alb, sky_hor * (wet * .04), mix(nb, .9, .3, wet), mix(nb, .1, .55, wet)))
        # sky (4)
        t = clamp01(uy)
        skyc = mix(nb, sky_hor, sky_top, smoothstep(nb, 0.0, .55, t))
        branches.append((eq(code, 4), E.wrap(nb, (0, 0, 0)), skyc, 1.0, 0.0))
    if kind == 1:
        # district street (15) with lamp pools, lamps (7), roofs (12)
        on = on_at(ux)
        asp = c0 * (tf.comp(0) * .24 + .84) * mix(nb, 1.0, .68, wet) * mix(nb, 1.0, .82, night)
        pool = clamp01(uy).f1("POWER", 1.4)
        branches.append((eq(code, 15), asp * mix(nb, 1.0, mix(nb, .6, 1.0, on), night),
                         warm * (pool * on * night * (wet * .6 + 1.0) * .85), mix(nb, .9, .3, wet), mix(nb, .1, .55, wet)))
        branches.append((eq(code, 7), c0 * mix(nb, 1.0, .3, night), c0 * (on * night * 2.2), .5, .1))
        roof = c0 * (tf.comp(0) * .3 + .82) * (hash21(nb, (wx / 9.0).f1("FLOOR"), (wz / 9.0).f1("FLOOR")) * .14 + .92)
        branches.append((eq(code, 12), roof * mix(nb, 1.0, mix(nb, .6, 1.0, on), night), E.wrap(nb, (0, 0, 0)), .9, .1))
        branches.append((eq(code, 6), c0 * .35, c0 * (on * mix(nb, .35, 1.8, night)), .5, .1))
    else:
        branches.append((eq(code, 12), c0 * (tf.comp(0) * .3 + .82), E.wrap(nb, (0, 0, 0)), .9, .1))
        gate = E.wrap(nb, 1.0)
        if kind == 3:
            pon = nb.node("ShaderNodeAttribute", attribute_name="plant_on", attribute_type="OBJECT")
            gate = mix(nb, 1.0, E(nb, pon.outputs["Fac"]), c0.comp(1).f1("GREATER_THAN", .3))
        branches.append((eq(code, 6), c0 * .35, c0 * (gate * mix(nb, .35, 1.8, night)), .5, .1))
    # water surfaces (10, 9)
    wcol = E.wrap(nb, (.11, .33, .43)) * (tx.comp(1) * .16 + .9)
    for k in (10, 9):
        branches.append((eq(code, k), wcol, sky_hor * ((1.0 - night * .6) * .06), .12, .5))
    if kind == 2:
        # conductors (5): corridor state from the object's corridor table (an image row would be overkill here)
        st = nb.node("ShaderNodeAttribute", attribute_name="corr_state", attribute_type="GEOMETRY")
        stv = E(nb, st.outputs["Vector"], True)
        state, flow = stv.comp(0), stv.comp(1)
        glow = cyan * ((flow * .75 + .25) * .6)
        glow = mix(nb, glow, E.wrap(nb, (1.0, .55, .12)), eq(state, 1))
        glow = mix(nb, glow, E.wrap(nb, (1.0, .18, .12)), eq(state, 2))
        branches.append((eq(code, 5), mix(nb, c0, E.wrap(nb, (.35, .08, .06)), eq(state, 2)),
                         glow * mix(nb, .25, 1.4, night), .5, .2))
        branches.append((eq(code, 17), c0 * mix(nb, 1.0, .7, night), E.wrap(nb, (0, 0, 0)), .5, .3))
    branches.append((eq(code, 3), c0 * .4, cyan * (1.22 * mix(nb, .5, 1.5, night)), .5, .2))
    if kind == 3:
        chg = nb.node("ShaderNodeValue")
        chg.outputs[0].default_value = 0.6
        lit = E(nb, chg.outputs[0]).f1("GREATER_THAN", ux)
        branches.append((eq(code, 13), mix(nb, E.wrap(nb, (.12, .14, .16)), c0 * .4, lit),
                         E.wrap(nb, (.45, 1.0, .62)) * (lit * mix(nb, .6, 1.6, night)), .5, .2))
        branches.append((eq(code, 11), c0 * mix(nb, 1.0, .55, night), warm * (night * .06), 1.0, 0.0))
    branches.append((eq(code, 1), c0 * (tf.comp(1) * .3 + .85), E.wrap(nb, (0, 0, 0)), .9, .05))
    branches.append((eq(code, 18), c0 * mix(nb, 1.0, .72, night), E.wrap(nb, (0, 0, 0)), .35, .5))
    # default (matte etc.)
    dflt = c0 * (tf.comp(0) * .1 + .94) * mix(nb, 1.0, .82, wet)
    if kind == 1:
        dflt = dflt * mix(nb, 1.0, mix(nb, .62, 1.0, on_at(ux)), night)
    alb, em, rough, spec = dflt, E.wrap(nb, (0, 0, 0)), E.wrap(nb, .85), E.wrap(nb, .12)
    for m, a, e, r, s in branches:
        alb = mix(nb, alb, a, m)
        em = mix(nb, em, e, m)
        rough = mix(nb, rough, r, m)
        spec = mix(nb, spec, s, m)
    nt.links.new(alb.s, bsdf.inputs["Base Color"])
    nt.links.new(em.s, bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 1.0
    if tier == "low":
        bsdf.inputs["Roughness"].default_value = 1.0
        bsdf.inputs["Specular IOR Level"].default_value = 0.0
    else:
        nt.links.new(rough.s, bsdf.inputs["Roughness"])
        nt.links.new((spec * 1.6).s, bsdf.inputs["Specular IOR Level"])
    return mat


def prop_material(name, kind, tier, g):
    """Shaders/VcPropCore.hlsl: livery (17/16), car paint per object (19), lights (6), foliage (1)."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    nb = NB(nt)
    out = nb.node("ShaderNodeOutputMaterial")
    bsdf = nb.node("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    attr = nb.node("ShaderNodeAttribute", attribute_name="Color", attribute_type="GEOMETRY")
    gamma = nb.node("ShaderNodeGamma")
    gamma.inputs[1].default_value = 2.2
    nt.links.new(attr.outputs["Color"], gamma.inputs[0])
    c0 = E(nb, gamma.outputs[0], True)
    if tier == "before":
        nt.links.new(c0.s, bsdf.inputs["Base Color"])
        return mat
    code = (E(nb, attr.outputs["Alpha"]) * 20.0 + 0.5).f1("FLOOR")
    tint_a = nb.node("ShaderNodeAttribute", attribute_name="tint", attribute_type="OBJECT")
    tint = E(nb, tint_a.outputs["Color"], True)
    night = E.wrap(nb, g["night"])
    alb, em = c0, E.wrap(nb, (0, 0, 0))
    alb = mix(nb, alb, c0 * E.wrap(nb, (.95, .72, .12)), eq(code, 17))
    alb = mix(nb, alb, E.wrap(nb, (.77, .56, .07)) * 1.0, eq(code, 16))
    alb = mix(nb, alb, c0 * tint, eq(code, 19))
    alb = mix(nb, alb, c0 * tint * mix(nb, 1.0, .7, night), eq(code, 1))
    em = mix(nb, em, c0 * mix(nb, .6, 2.4, night), eq(code, 6))
    alb = mix(nb, alb, c0 * .3, eq(code, 6))
    alb = alb * mix(nb, 1.0, .8, E.wrap(nb, g["wet"]))
    nt.links.new(alb.s, bsdf.inputs["Base Color"])
    nt.links.new(em.s, bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 1.0
    bsdf.inputs["Roughness"].default_value = 1.0 if tier == "low" else .55
    bsdf.inputs["Specular IOR Level"].default_value = 0.0 if tier == "low" else .3
    return mat


# ------------------------------------------------------------------------------------------------ scene
def load_glb(group, name):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(MODELS / group / f"{name}.glb"))
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    o = objs[0]
    o.name = name
    return o


def corridor_attr(obj, states):
    """Bake the corridor state per vertex into a 'corr_state' attribute (the Unity shader reads _VcCorridorState[uv.x])."""
    me = obj.data
    uv = me.uv_layers["UVMap"].data
    a = me.attributes.new("corr_state", "FLOAT_VECTOR", "CORNER")
    hide_loops = set()
    for li, d in enumerate(uv):
        ci = int(round(d.uv[0]))
        s, f = states.get(ci, (0, .6)) if d.uv[0] > -.5 else (0, .6)
        a.data[li].vector = (s, f, 0)
        if s == 3:
            hide_loops.add(li)
    if hide_loops:
        # not-built corridors collapse (the shader moves them to the origin): delete those faces here
        import bmesh
        bm = bmesh.new()
        bm.from_mesh(me)
        uvl = bm.loops.layers.uv["UVMap"]
        dead = [f for f in bm.faces if f.loops[0][uvl].uv[0] > -.5 and states.get(int(round(f.loops[0][uvl].uv[0])), (0,))[0] == 3]
        bmesh.ops.delete(bm, geom=dead, context="FACES")
        bm.to_mesh(me)
        bm.free()


def place(src, pos, yaw_deg=0.0, scale=1.0, props=None):
    o = bpy.data.objects.new(src.name + "_i", src.data)
    bpy.context.scene.collection.objects.link(o)
    o.location = pos
    o.rotation_euler = (0, 0, math.radians(-yaw_deg))
    o.scale = (scale, scale, scale)
    for k, v in (props or {}).items():
        o[k] = v
    return o


def road_samples(n, seed=7):
    pts = []
    for r in LAY["roads"]:
        c = r["centre"]
        for a, b in zip(c, c[1:]):
            pts.append((a, b))
    out = []
    k = seed
    for i in range(n):
        k = (k * 1103515245 + 12345) % 2 ** 31
        a, b = pts[k % len(pts)]
        t = ((k >> 8) % 1000) / 1000
        p = [a[j] + (b[j] - a[j]) * t for j in range(3)]
        yaw = math.degrees(math.atan2(b[0] - a[0], b[2] - a[2]))
        side = 1.8 if (k >> 4) % 2 else -1.8
        if side < 0:
            yaw += 180
        rad = math.radians(yaw)
        p[0] += math.cos(rad) * side
        p[2] -= math.sin(rad) * side
        out.append((p, yaw, (k >> 12) % 8, (k >> 16) % 3))
    return out


PAINTS = [(0.93, .93, .91), (.73, .75, .77), (.29, .31, .34), (.78, .26, .23), (.23, .40, .71), (.18, .56, .54),
          (.36, .56, .31), (.85, .75, .54)]


def build_scene(tier, moment):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    st = STATES[moment] if moment in STATES else STATES[BASE[moment]]
    g = CURVES[BASE.get(moment, moment)]
    sc = bpy.context.scene
    objs = {}
    for name in ["terrain_city", "sky_dome", "grid_lines"] + ["district_" + d["id"] for d in LAY["districts"]]:
        objs[name] = load_glb("world", name)
    corridor_attr(objs["grid_lines"], st["corridors"])
    for t, lvl in st["plants"].items():
        if lvl:
            o = load_glb("plants", f"plant_{t}_l{lvl}")
            o["plant_on"] = 1.0 if t != "solar" or moment != "night" else 0.0
            objs["plant_" + t] = o
    subs = {"substation": load_glb("grid", "substation"), "substation_smart": load_glb("grid", "substation_smart")}
    props = {n: load_glb("props", n) for n in ("truck_crew", "car", "fault_marker", "tree", "wind_blades")}
    for o in list(subs.values()) + list(props.values()):
        o.hide_render = True
    # materials
    mats = {k: world_material(f"world_{k}", k, tier, g) for k in range(4)}
    for name, o in objs.items():
        kind = 0 if name in ("terrain_city", "sky_dome") else 1 if name.startswith("district") else 2 if name == "grid_lines" else 3
        o.data.materials.clear()
        o.data.materials.append(mats[kind])
        if kind == 1:
            did = name.removeprefix("district_")
            f, t = st["ripple"].get(did, (2.0, 1.0))
            o["ripple"] = (f, t, 0.0)
    for s in subs.values():
        s.data.materials.clear()
        s.data.materials.append(mats[2])
    pm = {k: prop_material(f"prop_{k}", k, tier, g) for k in range(4)}
    for i, n in enumerate(("truck_crew", "car", "fault_marker", "tree")):
        props[n].data.materials.clear()
        props[n].data.materials.append(pm[i])
    props["wind_blades"].data.materials.clear()
    props["wind_blades"].data.materials.append(pm[0])
    # substations (smart in the storm frame: three)
    smart = {"riverside", "market_square", "tech_park"} if moment == "storm" else set()
    for d in LAY["districts"]:
        src = subs["substation_smart" if d["id"] in smart else "substation"]
        place(src, U2B(d["substation"]))
    for h in LAY["hubs"]:
        place(subs["substation"], U2B(h["pos"]))
    # trees / cars / blades / trucks / faults
    trees = LAY["trees"][::2] if tier == "low" else LAY["trees"]
    for i, t in enumerate(trees):
        s = .8 + .5 * ((i * 37) % 11) / 10
        h = ((i * 53) % 17) / 16
        place(props["tree"], U2B(t[:3]), (i * 47) % 360, s, {"tint": (.82 + .26 * h, .95 + .07 * h, .72 + .14 * h, 1.0)})
    for p, yaw, paint, shape in road_samples(20 if tier == "low" else 40):
        place(props["car"], U2B(p), yaw, 1.0, {"tint": PAINTS[paint] + (1.0,)})
    wl = st["plants"].get("wind", 0)
    for i, hub in enumerate(LAY["plants"]["wind"]["hubs"][:8 * wl]):
        o = place(props["wind_blades"], U2B(hub))
        o.rotation_euler = (0, math.radians((i * 41) % 120), 0)
    corr = {c["id"]: c for c in LAY["corridors"]}
    trucks = []
    for k, (cid, t) in enumerate(st["faults"]):
        pts = corr[cid]["points"]
        a, b = pts[len(pts) // 2 - 1], pts[len(pts) // 2]
        x = a[0] + (b[0] - a[0]) * t
        z = a[1] + (b[1] - a[1]) * t
        y = 0.0
        best = min((r for r in LAY["roads"]), key=lambda r: min(math.hypot(q[0] - x, q[2] - z) for q in r["centre"]))
        q = min(best["centre"], key=lambda q: math.hypot(q[0] - x, q[2] - z))
        place(props["fault_marker"], U2B((x, q[1] + 22, z)), 0, 3.6)
        trucks.append((q, k))
    for (q, k) in trucks:
        place(props["truck_crew"], U2B((q[0] + 3, q[1], q[2])), 30 + 60 * k, 1.4)
    for p, yaw, _paint, _s in road_samples(2, seed=99):
        place(props["truck_crew"], U2B(p), yaw, 1.4)
    return objs


def fit_camera(sc, boxes):
    """Same math as Runtime/VcCameraFit.cs (55 deg tilt, 35 deg vertical FOV, fit inside the frame with a margin)."""
    tilt, fov, margin = math.radians(55), math.radians(35), .03
    aspect = W / H
    fwd = Vector((0, math.cos(tilt), -math.sin(tilt)))
    up = Vector((0, math.sin(tilt), math.cos(tilt)))
    right = Vector((1, 0, 0))
    ty = math.tan(fov / 2)
    tx = ty * aspect
    pts = []
    for lo, hi in boxes:
        for i in range(8):
            pts.append(Vector(((hi if i & 1 else lo)[0], (hi if i & 4 else lo)[2], (hi if i & 2 else lo)[1])))
    target = sum(pts, Vector()) / len(pts)
    lim = 1 - 2 * margin
    dist = 1000.0
    for _ in range(6):
        lo_d, hi_d = 50.0, 20000.0
        for _k in range(40):
            mid = (lo_d + hi_d) / 2
            eye = target - fwd * mid
            ok = True
            for p in pts:
                q = p - eye
                z = q.dot(fwd)
                if z <= 20 or abs(q.dot(right) / (z * tx)) > lim or abs(q.dot(up) / (z * ty)) > lim:
                    ok = False
                    break
            if ok:
                hi_d = mid
            else:
                lo_d = mid
        dist = hi_d
        eye = target - fwd * dist
        xs = [(p - eye).dot(right) / ((p - eye).dot(fwd) * tx) for p in pts]
        ys = [(p - eye).dot(up) / ((p - eye).dot(fwd) * ty) for p in pts]
        depth = sum((p - eye).dot(fwd) for p in pts) / len(pts)
        target += right * ((max(xs) + min(xs)) / 2 * depth * tx) + up * ((max(ys) + min(ys)) / 2 * depth * ty)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle_y = fov
    cam.data.clip_start = 20
    cam.data.clip_end = 9000
    cam.location = target - fwd * dist
    cam.rotation_euler = (math.radians(90 - 55), 0, 0)
    sc.camera = cam
    return dist


def setup_render(sc, tier, moment):
    g = CURVES[BASE.get(moment, moment)]
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = W, H
    sc.render.resolution_percentage = 85 if tier == "low" else 100
    sc.eevee.taa_render_samples = 16
    # Standard view (no filmic highlight desaturation): closest to URP's Neutral tonemap with saturated emissives
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.exposure = -0.1
    # sun (Unity pitch / yaw -> Blender: pointing down by pitch, towards yaw from north)
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sc.collection.objects.link(sun)
    pitch, yaw = g["sun"]
    d = Vector((math.sin(math.radians(yaw)) * math.cos(math.radians(pitch)),
                math.cos(math.radians(yaw)) * math.cos(math.radians(pitch)), -math.sin(math.radians(pitch))))
    sun.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    sun.data.color = g["sun_color"]
    sun.data.energy = g["sun_energy"] * 3.2
    sun.data.use_shadow = tier == "medium"
    sun.data.angle = math.radians(2)
    w = bpy.data.worlds.new("w")
    sc.world = w
    w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    bg.inputs[0].default_value = tuple(c * 1.0 for c in g["ambient"]) + (1.0,)
    bg.inputs[1].default_value = max(g["ambient_energy"], 0.62) * 0.9
    # depth fog from the mist pass (exponential fog approximated: mist depth 1 / density, quadratic falloff)
    sc.view_layers[0].use_pass_mist = True
    w.mist_settings.start = 300
    w.mist_settings.depth = 2.6 / g["fog_density"] * .55
    w.mist_settings.falloff = "QUADRATIC"
    # Blender 5 compositor: a node group with ShaderNodeMix / ShaderNodeMath and a group output
    tree = bpy.data.node_groups.new("comp", "CompositorNodeTree")
    sc.compositing_node_group = tree
    rl = tree.nodes.new("CompositorNodeRLayers")
    mul = tree.nodes.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    mul.inputs[1].default_value = 0.62
    tree.links.new(rl.outputs["Mist"], mul.inputs[0])
    mixn = tree.nodes.new("ShaderNodeMix")
    mixn.data_type = "RGBA"
    tree.links.new(mul.outputs[0], mixn.inputs[0])
    tree.links.new(rl.outputs["Image"], mixn.inputs[6])
    mixn.inputs[7].default_value = tuple(g["fog"]) + (1.0,)
    tree.interface.new_socket("Image", in_out="OUTPUT", socket_type="NodeSocketColor")
    outn = tree.nodes.new("NodeGroupOutput")
    tree.links.new(mixn.outputs[2], outn.inputs[0])


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for moment in MOMENTS:
        view = VIEWS.get(moment)
        boxes = [(d["box_min"], d["box_max"]) for d in LAY["districts"] if view is None or d["id"] in view]
        for tier in TIERS:
            build_scene(tier, moment)
            sc = bpy.context.scene
            setup_render(sc, tier, moment)
            dist = fit_camera(sc, boxes)
            path = OUT / tier / f"{moment}.png"
            path.parent.mkdir(parents=True, exist_ok=True)
            sc.render.filepath = str(path)
            bpy.ops.render.render(write_still=True)
            print(f"FRAME {tier} {moment} {path} cam_dist={dist:.0f}")


main()
