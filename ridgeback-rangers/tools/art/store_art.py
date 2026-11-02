"""Ridgeback Rangers — store art + diorama preview, rendered in Blender from the game's OWN assets: the Blender kit merge
bank (unity/Assets/Resources/kit/props.bytes, read back exactly as World/KitBank.cs reads it), the Meshy dinosaurs and
the Chief Warden (unity/Assets/Art/Meshy), the palette, the tram car FBX and the map (data/map.json). EEVEE, headless.
  blender -b --python ridgeback-rangers/tools/art/store_art.py -- <game dir> [preview_dir]
Outputs: store/feature-graphic.png (1024×500), store/icon-candidates/c1..c3.png (512×512, full-bleed, no text);
with preview_dir also diorama_low.png / diorama_medium.png (the island laid out like World/IslandWorld.cs, Low = "_low"
twins + box fences + no lamps; Medium = bevelled kit) — the art check for docs/ART.md, rendered with BACK-FACE CULLING
ON, so a wrongly wound prop shows up as a hole.
Tone (plan R4, game is 13+): a grown-up management game — warm late light, the island and the work of the rangers;
no cartoon close-ups of eyes, no childish styling, no text, no film-franchise imagery (no torch gate, no jeeps, no logo).
"""
import json
import math
import os
import random
import struct
import sys

import bpy
import mathutils

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
GAME = os.path.abspath(argv[0] if argv else os.path.join(os.path.dirname(__file__), "../.."))
PREVIEW = argv[1] if len(argv) > 1 else ""
A = os.path.join(GAME, "unity/Assets")
STORE = os.path.join(GAME, "store")
PALETTE = os.path.join(A, "Resources/materials/palette.png")
KIT = 2.2   # IslandWorld.KitScale


def read_bank(path):
    data = open(path, "rb").read()
    assert data[:4] == b"RRKB"
    ver, count = struct.unpack_from("<HH", data, 4)
    assert ver == 1
    off, bank = 8, {}
    for _ in range(count):
        n = data[off]; off += 1
        name = data[off:off + n].decode("ascii"); off += n
        nv, ni = struct.unpack_from("<HH", data, off); off += 4
        verts = []
        for _ in range(nv):
            x, y, z, nx, ny, nz, sw = struct.unpack_from("<hhhbbbB", data, off); off += 10
            verts.append(((x / 500.0, y / 500.0, z / 500.0), sw))
        idx = list(struct.unpack_from("<%dH" % ni, data, off)); off += 2 * ni
        bank[name] = (verts, idx)
    return bank


BANK = read_bank(os.path.join(A, "Resources/kit/props.bytes"))
_mesh_cache = {}


def palette_mat():
    m = bpy.data.materials.get("palette")
    if m:
        return m
    m = bpy.data.materials.new("palette")
    m.use_nodes = True
    m.use_backface_culling = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    tex = m.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(PALETTE)
    tex.interpolation = "Closest"
    m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    return m


def uv_of(sw):
    return ((sw % 8 + 0.5) / 8.0, 1.0 - (sw // 8 + 0.5) / 8.0)


def bank_mesh(name):
    """Unity space (x, y up, z) -> Blender (x, z, y), winding reversed back — the inverse of kit.py's bank_entry()."""
    if name in _mesh_cache:
        return _mesh_cache[name]
    verts, idx = BANK[name]
    me = bpy.data.meshes.new("bank_" + name)
    me.from_pydata([(p[0], p[2], p[1]) for p, _ in verts], [], [(idx[i], idx[i + 2], idx[i + 1]) for i in range(0, len(idx), 3)])
    uvl = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        for li in poly.loop_indices:
            uvl.data[li].uv = uv_of(verts[me.loops[li].vertex_index][1])
    me.materials.append(palette_mat())
    _mesh_cache[name] = me
    return me


def prop(name, loc, yaw=0.0, scale=KIT, low=False):
    """loc = Blender (x, y, z); yaw in degrees as the game passes it (Unity yaw = clockwise from above)."""
    if low and (name + "_low") in BANK:
        name = name + "_low"
    ob = bpy.data.objects.new(name, bank_mesh(name))
    bpy.context.scene.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = (0, 0, -math.radians(yaw))
    ob.scale = scale if isinstance(scale, tuple) else (scale, scale, scale)
    return ob


def flat_mat(name, rgb, rough=0.9):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.use_backface_culling = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = rough
    return m


def srgb(h):
    c = [int(h[i:i + 2], 16) / 255.0 for i in (1, 3, 5)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def disc(c, r, z, mat, segs=32, r2=None):
    r2 = r2 or r
    me = bpy.data.meshes.new("disc")
    vs = [(c[0], c[1], z)] + [(c[0] + math.cos(a) * r, c[1] + math.sin(a) * r2, z) for a in (i * 2 * math.pi / segs for i in range(segs))]
    fs = [(0, 1 + i, 1 + (i + 1) % segs) for i in range(segs)]
    me.from_pydata(vs, [], fs)
    me.materials.append(mat)
    ob = bpy.data.objects.new("disc", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def strip(a, b, w, z, mat):
    dx, dy = b[0] - a[0], b[1] - a[1]
    L = math.hypot(dx, dy) or 1
    sx, sy = -dy / L * w / 2, dx / L * w / 2
    me = bpy.data.meshes.new("strip")
    me.from_pydata([(a[0] - sx, a[1] - sy, z), (b[0] - sx, b[1] - sy, z), (b[0] + sx, b[1] + sy, z), (a[0] + sx, a[1] + sy, z)], [], [(0, 1, 2, 3)])
    me.materials.append(mat)
    ob = bpy.data.objects.new("strip", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def box(c, size, mat, yaw=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(c[0], c[1], c[2] + size[2] / 2))
    ob = bpy.context.active_object
    ob.scale = size
    ob.rotation_euler = (0, 0, -math.radians(yaw))
    ob.data.materials.append(mat)
    return ob


def textured(obj, png):
    m = bpy.data.materials.new(os.path.basename(png))
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    tex = m.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(png)
    m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.8
    for o in [obj] + list(obj.children_recursive):
        if o.type == "MESH":
            o.data.materials.clear()
            o.data.materials.append(m)


def fbx(path, png, loc, yaw=0.0, scale=1.0):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    root = [o for o in new if o.parent is None][0]
    if png:
        for o in new:
            if o.type == "MESH":
                textured(o, png)
    root.location = loc
    root.rotation_euler = (root.rotation_euler.x, root.rotation_euler.y, math.radians(yaw))
    root.scale = (root.scale.x * scale, root.scale.y * scale, root.scale.z * scale)
    return root


def dino(family, loc, yaw=0.0, scale=1.0):
    return fbx(os.path.join(A, "Art/Meshy/dino_%s_lod0.fbx" % family), os.path.join(A, "Resources/kit/dino_%s_base.png" % family), loc, yaw, scale)


def warden(loc, yaw=0.0, scale=1.0):
    return fbx(os.path.join(A, "Art/Meshy/warden_lod0.fbx"), os.path.join(A, "Art/Meshy/warden_base.png"), loc, yaw, scale)


def tram(loc, yaw=0.0, scale=KIT):
    ob = fbx(os.path.join(A, "Resources/kit/tram_car.fbx"), None, loc, yaw, scale)
    for o in [ob] + list(ob.children_recursive):
        if o.type == "MESH":
            o.data.materials.clear()
            o.data.materials.append(palette_mat())
    return ob


def reset(sky_top="#F2B880", sky_bottom="#9FC9C2", strength=1.0, filmic=True):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mesh_cache.clear()
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.eevee.use_soft_shadows = True
    sc.eevee.shadow_cube_size = "1024"
    sc.eevee.shadow_cascade_size = "2048"
    sc.eevee.use_gtao = True
    sc.eevee.gtao_distance = 2.0
    sc.view_settings.view_transform = "Filmic" if filmic else "Standard"
    sc.view_settings.look = "Medium High Contrast" if filmic else "None"
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    nt = w.node_tree
    bg = nt.nodes["Background"]
    coord = nt.nodes.new("ShaderNodeTexCoord")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    # a vertical sky gradient: the window vector's z drives the ramp
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    rng_ = nt.nodes.new("ShaderNodeMapRange")
    nt.links.new(coord.outputs["Generated"], sep.inputs[0])
    nt.links.new(sep.outputs["Z"], rng_.inputs["Value"])
    rng_.inputs["From Min"].default_value = -0.05
    rng_.inputs["From Max"].default_value = 0.45
    nt.links.new(rng_.outputs["Result"], ramp.inputs["Fac"])
    ramp.color_ramp.elements[0].color = (*srgb(sky_bottom), 1)
    ramp.color_ramp.elements[1].color = (*srgb(sky_top), 1)
    nt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = strength
    sc.world = w
    return sc


def sun(rot=(52, 0, -38), energy=3.4, color="#FFE2BC", angle=3.0):
    s = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    s.data.energy = energy
    s.data.color = srgb(color)
    s.data.angle = math.radians(angle)
    s.rotation_euler = mathutils.Euler([math.radians(a) for a in rot])
    bpy.context.scene.collection.objects.link(s)
    return s


def fill(rot=(70, 0, 150), energy=0.8, color="#BFD9FF"):
    return sun(rot, energy, color, 10)


def camera(loc, look, lens=50):
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    bpy.context.scene.collection.objects.link(cam)
    cam.location = loc
    d = mathutils.Vector(look) - mathutils.Vector(loc)
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = lens
    cam.data.clip_start = 2.0 if loc[2] > 50 else 0.1
    cam.data.clip_end = 5000
    bpy.context.scene.camera = cam
    return cam


QUICK = os.environ.get("QUICK") == "1"
SHOTS = [x for x in os.environ.get("SHOTS", "").split(",") if x]


def bbox(root):
    """World-space bounds of an imported model (all mesh children)."""
    bpy.context.view_layer.update()
    lo, hi = [1e9] * 3, [-1e9] * 3
    for o in [root] + list(root.children_recursive):
        if o.type != "MESH":
            continue
        for c in o.bound_box:
            w = o.matrix_world @ mathutils.Vector(c)
            for k in range(3):
                lo[k] = min(lo[k], w[k]); hi[k] = max(hi[k], w[k])
    return lo, hi


def render(path, w, h, samples=64):
    sc = bpy.context.scene
    if QUICK:
        samples, w, h = 8, w // 2, h // 2
        path = os.path.join(os.environ.get("TMPDIR", "/tmp"), "quick_" + os.path.basename(path))
    sc.eevee.taa_render_samples = samples
    sc.render.resolution_x, sc.render.resolution_y = w, h
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGB"
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("RENDER", path)


# ------------------------------------------------------------------------------------------------ the island (preview)

def island(low):
    """World/IslandWorld.cs, Blender side: 1 map km = 100 units, Unity (x, z) = Blender (x, y). All three zones open."""
    m = json.load(open(os.path.join(A, "Resources/data/map.json")))
    nodes = {n["id"]: n for n in m["nodes"]}
    P = lambda n: (n["x"] * 100.0, n["y"] * 100.0)
    mats = {k: flat_mat(k, srgb(v)) for k, v in {"sea": "#4FA3C7", "sand": "#E8D3A0", "grass": "#8CC970", "grassd": "#5E9E4A",
                                                  "path": "#C9A878", "stone": "#B9B4AA", "mud": "#7E5F45", "wood": "#9A6B43",
                                                  "steel": "#9AA6AE", "rail": "#9A6B43"}.items()}
    disc((150, 125), 700, -0.6, mats["sea"], 8)
    disc((150, 125), 205, -0.1, mats["sand"], 28, 170)
    disc((150, 125), 192, 0.0, mats["grass"], 28, 158)
    prop("volcano", (150, 300, 0), 0, KIT, low)
    prop("hill", (40, 285, 0), 30, (2.2, 2.2, 2.13), low)
    prop("hill", (265, 285, 0), -20, (2.4, 2.4, 2.53), low)
    for a, b in m["edges"]:
        strip(P(nodes[a]), P(nodes[b]), 6, 0.1, mats["path"])
        if not low:
            pa, pb = P(nodes[a]), P(nodes[b])
            L = math.hypot(pb[0] - pa[0], pb[1] - pa[1])
            if L >= 24:
                sx, sy = -(pb[1] - pa[1]) / L * 4.6, (pb[0] - pa[0]) / L * 4.6
                d = 12.0
                while d < L - 10:
                    t = d / L
                    prop("lamp", (pa[0] + (pb[0] - pa[0]) * t - sx, pa[1] + (pb[1] - pa[1]) * t - sy, 0), 0, KIT)
                    d += 30
    for i, n in enumerate([n for n in m["nodes"] if n["kind"] == "dig"]):
        disc(P(n), 9, 0.11, mats["mud"], 10)
        prop("dig_site", (*P(n), 0), 35 * i, KIT, low)
    gate = P(nodes["gate"])
    disc((gate[0], gate[1] + 8), 16, 0.12, mats["stone"], 12)
    for name, nid, dz in (("gate", "gate", 0), ("hq", "hq", 0), ("barn", "barn", 0), ("vet", "vet", 0), ("hatchery", "hatchery", 0), ("station", "station", 4)):
        p = P(nodes[nid])
        prop(name, (p[0], p[1] + dz, 0), 0)
    prop("kiosk_cafe", (gate[0] - 26, gate[1] + 16, 0), 0, KIT, low)
    prop("kiosk_gift", (gate[0] + 26, gate[1] + 16, 0), 0, KIT, low)
    # zones: ground patch + foliage (same rules as BuildZone, own rng), viewpoints
    for z in m["zones"]:
        pts = [P(n) for n in m["nodes"] if n["zone"] == z["id"] and n["kind"] in ("paddock", "view")]
        zc = (sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts))
        if z["id"] != "cove":
            disc(zc, 70, 0.05, mats["sand"] if z["biome"] == "dune" else mats["grassd"], 16, 55)
        rng = random.Random(z["id"])
        for _ in range(14 if low else 28):
            p = (zc[0] + rng.uniform(-70, 70), zc[1] + rng.uniform(-55, 55))
            if any(math.hypot(p[0] - q[0], p[1] - q[1]) < 26 for q in (P(n) for n in m["nodes"])):
                continue
            roll, yaw, s = rng.random(), rng.randrange(360), KIT * (0.85 + rng.random() * 0.3)
            if z["biome"] == "dune":
                prop("rock", (*p, 0), yaw, s, low) if roll < 0.5 else prop("tree_fern", (*p, 0), yaw, s * 1.3, low)
            elif roll < 0.5:
                prop("tree_tall" if roll < 0.2 else "tree", (*p, 0), yaw, s * 1.35, low)
            else:
                prop("tree_fern", (*p, 0), yaw, s * 1.3, low)
        v = [n for n in m["nodes"] if n["zone"] == z["id"] and n["kind"] == "view"][0]
        prop("view", (P(v)[0], P(v)[1] + 4, 0), 0)
    # every paddock built, tier by zone, water + shade on the dunes
    for n in [n for n in m["nodes"] if n["kind"] == "paddock"]:
        c = P(n)
        dune = nodes[n["id"]]["zone"] == "dune"
        disc(c, 20, 0.08, mats["sand"] if dune else mats["grass"], 12, 14)
        tier = 2 if n["zone"] == "fern" else 1
        fence(c, 40, 28, tier, low, mats)
        if dune:
            prop("water_point", (c[0] + 12, c[1] + 7, 0), 0, KIT, low)
            prop("shade_sail", (c[0] - 12, c[1] + 7, 0), 15, KIT, low)
    # the tram rail (station -> views loop) and two cars
    stops = [P(nodes["station"])] + [P(nodes[v]) for v in ("c_view", "f_view", "d_view")]
    for i in range(len(stops)):
        a, b = stops[i], stops[(i + 1) % len(stops)]
        strip((a[0] + 6, a[1]), (b[0] + 6, b[1]), 3.2, 0.14, mats["rail"])
    return nodes


def fence(c, w, d, tier, low, mats):
    h = 1.8 if tier == 1 else 2.4
    corners = [(c[0] - w / 2, c[1] - d / 2), (c[0] + w / 2, c[1] - d / 2), (c[0] + w / 2, c[1] + d / 2), (c[0] - w / 2, c[1] + d / 2)]
    for i in range(4):
        a, b = corners[i], corners[(i + 1) % 4]
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        posts = max(2, round(L / 5))
        yaw = math.degrees(math.atan2(b[0] - a[0], b[1] - a[1]))
        mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        if low:   # MeshKit.Fence boxes
            m = mats["wood"] if tier == 1 else mats["steel"]
            for p in range(posts + 1):
                t = p / posts
                box((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, 0), (0.45, 0.45, h), m, yaw)
            for r in range(2):
                box((mid[0], mid[1], h * (0.4 + 0.45 * r)), (0.2, L, 0.25), m, yaw)
        else:
            for p in range(posts):
                t = p / posts
                prop("fence%d_post" % tier, (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, 0), yaw, h)
            prop("fence%d_rail" % tier, (mid[0], mid[1], 0), yaw, (h, L, h))


def preview(low):
    reset(sky_top="#CFE6F2", sky_bottom="#E9F2F2", filmic=False)
    island(low)
    sun((50, 0, -30), 3.2, "#FFFFFF", 2)
    fill()
    # the game's camera: pitch 52°, fov 32° (vertical), from the south
    pitch = math.radians(52)
    look = (150, 120, 0)
    dist = 720
    camera((look[0], look[1] - math.cos(pitch) * dist, math.sin(pitch) * dist), look)
    bpy.context.scene.camera.data.sensor_fit = "VERTICAL"
    bpy.context.scene.camera.data.sensor_height = 24
    bpy.context.scene.camera.data.lens = 12 / math.tan(math.radians(32) / 2)
    render(os.path.join(PREVIEW, "diorama_%s.png" % ("low" if low else "medium")), 720, 1280, 16)
    # close-up of a paddock row (fences / foliage detail)
    look = (100, 160, 0)
    camera((look[0] + 40, look[1] - 120, 95), look, lens=45)
    render(os.path.join(PREVIEW, "closeup_%s.png" % ("low" if low else "medium")), 1000, 700, 16)


# ------------------------------------------------------------------------------------------------ store art

def scene_set(sky_top, sky_bottom):
    sc = reset(sky_top=sky_top, sky_bottom=sky_bottom)
    mats = {"grass": flat_mat("g", srgb("#7DBA60")), "grassd": flat_mat("gd", srgb("#5E9E4A")), "path": flat_mat("p", srgb("#C9A878")),
            "sea": flat_mat("s", srgb("#4FA3C7"), 0.3), "sand": flat_mat("sa", srgb("#E8D3A0"))}
    return sc, mats


def feature_graphic():
    """1024×500: late afternoon on Ridgeback Isle — a mixed herd in a timber paddock, the safari tram on its rail, the
    Chief Warden on the path, tree ferns, and the volcano behind. No text (the composer adds the tagline)."""
    sc, mats = scene_set("#D9774F", "#F8D7A4")
    disc((0, 40), 600, 0.0, mats["grass"], 48)
    disc((0, 700), 1400, -0.5, mats["sea"], 16)
    prop("volcano", (30, 190, 0), 10, 1.7)
    prop("hill", (-110, 150, 0), 0, (2.0, 2.0, 1.6))
    prop("hill", (150, 150, 0), 40, (2.2, 2.2, 1.8))
    # the paddock: wood fence, a mixed herd, a drinking pool
    disc((6, 16), 19, 0.03, mats["grassd"], 24, 13)
    fence((6, 16), 36, 22, 1, False, None)
    dino("horned", (-2, 10, 0), yaw=-120, scale=6.0)
    dino("horned", (-9, 18, 0), yaw=-70, scale=4.5)
    dino("duckbill", (12, 12, 0), yaw=-160, scale=6.5)
    dino("plated", (4, 20, 0), yaw=-100, scale=5.5)
    dino("longneck", (16, 24, 0), yaw=-140, scale=13.0)
    prop("water_point", (18, 7, 0), 0, 1.6)
    # the tram line and the service road in front, a car at the stop, the Chief Warden on the path
    strip((-200, -6), (200, -6), 3.2, 0.14, flat_mat("rail", srgb("#9A6B43")))
    strip((-200, -14), (200, -14), 6, 0.1, mats["path"])
    tram((22, -6, 0.2), yaw=90, scale=2.0)
    warden((-10, -11, 0), yaw=-40, scale=1.9)
    prop("lamp", (-2, -18, 0), 0, 2.0)
    prop("lamp", (40, -18, 0), 0, 2.0)
    for (x, y, nm, yaw, s) in ((-26, -6, "tree_fern", 20, 3.6), (-22, 14, "tree", 80, 3.6), (-34, 30, "tree_tall", 0, 3.6), (36, 10, "tree_fern", 140, 3.4),
                               (44, 30, "tree", 200, 3.4), (-6, 44, "tree_tall", 10, 3.4), (28, 46, "tree_fern", 60, 3.0), (54, 4, "rock", 30, 2.4),
                               (-46, 6, "tree_fern", 300, 3.4), (62, 26, "tree_tall", 0, 3.6), (-24, 52, "tree", 50, 3.2), (12, 58, "tree", 90, 3.2),
                               (-60, 40, "tree", 120, 3.4), (80, 50, "tree_fern", 10, 3.2), (-14, -2, "rock", 70, 1.6)):
        prop(nm, (x, y, 0), yaw, s)
    prop("view", (-22, 34, 0), 0, 2.0)
    sun((64, 0, -62), 4.0, "#FFCB94", 2.5)
    fill((65, 0, 130), 0.8, "#AFC8FF")
    camera((-6, -46, 11), (8, 18, 7), lens=32)
    render(os.path.join(STORE, "feature-graphic.png"), 1024, 500, 96)


def icon_longneck():
    """c1: a long-neck's head and neck rising against a sunset volcano — one big silhouette that reads at 48 px."""
    sc, mats = scene_set("#B9473A", "#F5B97A")
    disc((0, 0), 400, 0.0, mats["grassd"], 32)
    prop("volcano", (-30, 150, 0), 0, 1.5)
    dino("longneck", (1.8, 0.5, 0), yaw=-40, scale=11.0)
    for (x, y, nm, yaw, s) in ((-3.2, -3.6, "tree_fern", 10, 2.0), (4.2, -3.8, "tree_fern", 70, 2.0), (9, 8, "tree_tall", 0, 3.0)):
        prop(nm, (x, y, 0), yaw, s)
    sun((78, 0, 200), 3.6, "#FFB070", 2)
    fill((60, 0, -20), 0.9, "#FFC9A0")
    camera((0.8, -9.0, 1.0), (-0.4, 0, 6.0), lens=24)
    render(os.path.join(STORE, "icon-candidates/c1.png"), 512, 512, 96)


def icon_horned():
    """c2: a plated herbivore in profile, back-lit by the evening sun — calm, grown-up, readable as a shape."""
    sc, mats = scene_set("#7A3E48", "#F2A56B")
    disc((0, 0), 400, 0.0, mats["grassd"], 32)
    prop("volcano", (40, 220, 0), 0, 1.6)
    dino("plated", (0, 0, 0), yaw=90, scale=8.0)
    for (x, y, nm, yaw, s) in ((-7.5, 3, "tree_fern", 10, 2.6), (7, 4, "tree_fern", 120, 2.8), (-12, 9, "tree_tall", 0, 3.2), (12, 10, "tree", 60, 3.2)):
        prop(nm, (x, y, 0), yaw, s)
    sun((76, 0, 165), 4.2, "#FFB67A", 2)
    fill((55, 0, -10), 0.7, "#9FB8FF")
    camera((0.5, -7.6, 1.3), (0.15, 0, 2.15), lens=34)
    render(os.path.join(STORE, "icon-candidates/c2.png"), 512, 512, 96)


def icon_warden():
    """c3: the Chief Warden in front, a long-neck behind her over the trees — the 'you run the sanctuary' icon."""
    sc, mats = scene_set("#2E8C9A", "#D8EFE2")
    disc((0, 0), 200, 0.0, mats["grass"], 32)
    warden((0, 0, 0), yaw=-15, scale=1.0)
    dino("longneck", (1.35, 4.0, 0), yaw=-30, scale=4.2)
    for (x, y, nm, yaw, s) in ((-2.6, 4, "tree_fern", 10, 1.4), (-4.5, 9, "tree_tall", 60, 1.6)):
        prop(nm, (x, y, 0), yaw, s)
    sun((50, 0, -40), 3.6, "#FFE6C4", 2)
    fill((70, 0, 150), 1.0, "#BFE0FF")
    camera((0.25, -1.5, 1.4), (0.3, 0, 1.62), lens=26)
    render(os.path.join(STORE, "icon-candidates/c3.png"), 512, 512, 96)


def main():
    if PREVIEW:
        preview(True)
        preview(False)
        return
    for name, fn in (("fg", feature_graphic), ("c1", icon_longneck), ("c2", icon_horned), ("c3", icon_warden)):
        if not SHOTS or name in SHOTS:
            fn()
    print("STORE_ART_DONE")


main()
