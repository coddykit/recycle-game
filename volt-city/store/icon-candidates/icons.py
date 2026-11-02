"""Grid Keeper: Energy Tycoon (volt-city) - app-icon CANDIDATES (2026-10-29). Not installed anywhere; owner picks one.

    blender -b --factory-startup -P icons.py -- <out_raw_dir> <blend_dir> [c1 c2 ...]

Renders each candidate at 1024 x 1024 (Cycles) to <out_raw_dir>/cN_raw.png and saves <blend_dir>/cN.blend.
post.py then adds the glow, downsizes to 512 / 96 / 48 and builds the contact sheet.
All geometry is modelled here (clean, chunky shapes read better at 48 px than the game's detailed kit).
Palette = the game UI: navy #0A1721, teal #2EC4B6, amber #FFC24D / #F5A623, gauge green #5FD38D, red #FF6B5E.
"""
import math
import random
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = Path(argv[0] if argv else "/tmp/icons")
BLEND = Path(argv[1] if len(argv) > 1 else OUT)
ONLY = argv[2:]
RES = 1024
SAMPLES = 64


def hexc(h, a=1.0):
    h = h.lstrip("#")
    srgb = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    lin = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb]
    return (*lin, a)


NAVY, NAVY2, PANEL = "#0A1721", "#12263A", "#1B3550"
TEAL, AMBER, ORANGE, GREEN, RED = "#2EC4B6", "#FFC24D", "#F5A623", "#5FD38D", "#FF6B5E"
WARM = "#FFE08A"


# ---------------------------------------------------------------- scene / render setup
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.compute_device_type = "METAL"
        prefs.get_devices()
        for d in prefs.devices:
            d.use = True
        sc.cycles.device = "GPU"
    except Exception as e:  # CPU fallback
        print("GPU setup failed:", e)
    sc.cycles.samples = SAMPLES
    sc.cycles.use_denoising = True
    sc.render.resolution_x = sc.render.resolution_y = RES
    sc.render.film_transparent = False
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGB"
    w = bpy.data.worlds.new("W")
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = hexc(NAVY)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.25
    return sc


def mat(name, color, rough=0.5, metal=0.0, emit=None, emit_strength=0.0, coat=0.0):
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    p = m.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = hexc(color)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    if emit:
        p.inputs["Emission Color"].default_value = hexc(emit)
        p.inputs["Emission Strength"].default_value = emit_strength
    if coat:
        p.inputs["Coat Weight"].default_value = coat
    return m


def emission_mat(name, color, strength=1.0):
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    nt = m.node_tree
    nt.nodes.clear()
    e = nt.nodes.new("ShaderNodeEmission")
    e.inputs[0].default_value = hexc(color)
    e.inputs[1].default_value = strength
    o = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(e.outputs[0], o.inputs[0])
    return m


def gradient_mat(name, stops, radial=0.0, center=(0.5, 0.5)):
    """Shadeless background: vertical ramp over Generated Y (0 bottom .. 1 top), darkened toward the corners by `radial`."""
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    nt = m.node_tree
    nt.nodes.clear()
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tc.outputs["Generated"], sep.inputs[0])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    cr = ramp.color_ramp
    cr.interpolation = "EASE"
    cr.elements[0].position, cr.elements[0].color = stops[0][0], hexc(stops[0][1])
    cr.elements[1].position, cr.elements[1].color = stops[-1][0], hexc(stops[-1][1])
    for pos, col in stops[1:-1]:
        el = cr.elements.new(pos)
        el.color = hexc(col)
    nt.links.new(sep.outputs["Y"], ramp.inputs[0])
    col_out = ramp.outputs[0]
    if radial:
        # d = distance from center in generated space -> factor 1 - radial * d^2
        vsub = nt.nodes.new("ShaderNodeVectorMath")
        vsub.operation = "SUBTRACT"
        vsub.inputs[1].default_value = (center[0], center[1], 0.5)
        nt.links.new(tc.outputs["Generated"], vsub.inputs[0])
        mul = nt.nodes.new("ShaderNodeVectorMath")
        mul.operation = "MULTIPLY"
        mul.inputs[1].default_value = (1, 1, 0)
        nt.links.new(vsub.outputs[0], mul.inputs[0])
        ln = nt.nodes.new("ShaderNodeVectorMath")
        ln.operation = "LENGTH"
        nt.links.new(mul.outputs[0], ln.inputs[0])
        sq = nt.nodes.new("ShaderNodeMath")
        sq.operation = "POWER"
        sq.inputs[1].default_value = 2.0
        nt.links.new(ln.outputs["Value"], sq.inputs[0])
        f = nt.nodes.new("ShaderNodeMath")
        f.operation = "MULTIPLY_ADD"
        f.inputs[1].default_value = -radial
        f.inputs[2].default_value = 1.0
        nt.links.new(sq.outputs[0], f.inputs[0])
        f.use_clamp = True
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        nt.links.new(col_out, mix.inputs["A"])
        nt.links.new(f.outputs[0], mix.inputs["B"])
        col_out = mix.outputs["Result"]
    e = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(col_out, e.inputs[0])
    o = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(e.outputs[0], o.inputs[0])
    return m


def link(obj):
    bpy.context.scene.collection.objects.link(obj)
    return obj


def mesh_obj(name, bm, material=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = link(bpy.data.objects.new(name, me))
    if material:
        ob.data.materials.append(material)
    return ob


def shade_smooth(ob, angle=40):
    for p in ob.data.polygons:
        p.use_smooth = True
    try:
        mod = ob.modifiers.new("smooth", "SMOOTH_BY_ANGLE")
    except Exception:
        try:
            bpy.context.view_layer.objects.active = ob
            bpy.ops.object.shade_auto_smooth(angle=math.radians(angle))
        except Exception:
            pass


def no_light_contrib(ob):
    ob.visible_diffuse = False
    ob.visible_glossy = False
    ob.visible_transmission = False
    ob.visible_shadow = False
    ob.visible_volume_scatter = False


def camera(loc, look_at, ortho=None, lens=50):
    cd = bpy.data.cameras.new("Cam")
    if ortho:
        cd.type = "ORTHO"
        cd.ortho_scale = ortho
    else:
        cd.lens = lens
    cd.clip_end = 500
    cam = link(bpy.data.objects.new("Cam", cd))
    cam.location = Vector(loc)
    d = Vector(look_at) - Vector(loc)
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam
    return cam


def backdrop(cam, material, dist=200):
    """Plane parented to the camera that exactly fills the frame."""
    cd = cam.data
    size = cd.ortho_scale if cd.type == "ORTHO" else 2 * dist * math.tan(cd.angle / 2)
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=size * 0.51)
    ob = mesh_obj("Backdrop", bm, material)
    ob.parent = cam
    ob.location = (0, 0, -dist)
    no_light_contrib(ob)
    return ob


def light(kind, loc, energy, color="#FFFFFF", size=1.0, rot=None, look_at=None):
    ld = bpy.data.lights.new(kind, kind)
    ld.energy = energy
    ld.color = hexc(color)[:3]
    if kind == "AREA":
        ld.size = size
    elif kind in ("POINT", "SPOT"):
        ld.shadow_soft_size = size
    elif kind == "SUN":
        ld.angle = math.radians(size)
    ob = link(bpy.data.objects.new(kind, ld))
    ob.location = loc
    if look_at is not None:
        ob.rotation_euler = (Vector(look_at) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    elif rot:
        ob.rotation_euler = Euler([math.radians(r) for r in rot])
    return ob


# ---------------------------------------------------------------- geometry helpers
def box_beam(bm, a, b, w):
    a, b = Vector(a), Vector(b)
    z = (b - a).normalized()
    up = Vector((0, 0, 1)) if abs(z.z) < 0.95 else Vector((1, 0, 0))
    x = z.cross(up).normalized()
    y = z.cross(x).normalized()
    h = w / 2
    vs = [bm.verts.new(p + x * sx * h + y * sy * h) for p in (a, b) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    f = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    for idx in f:
        bm.faces.new([vs[i] for i in idx])


def box(bm, center, size):
    m = Matrix.Translation(Vector(center)) @ Matrix.Diagonal((*size, 1.0))
    bmesh.ops.create_cube(bm, size=1.0, matrix=m)


def pylon(name, material, h=12.0, base=2.0, w=0.32):
    """Classic lattice transmission tower; origin at the foot centre. Returns (object, insulator tip points)."""
    bm = bmesh.new()
    top_half = 0.38
    waist_z = h * 0.62

    def half(z):
        if z <= waist_z:
            return base + (top_half - base) * (z / waist_z) ** 0.9
        return top_half

    levels = [0, h * 0.18, h * 0.34, h * 0.48, waist_z, h * 0.78, h * 0.9]
    corners = ((-1, -1), (1, -1), (1, 1), (-1, 1))
    for i in range(len(levels) - 1):
        z0, z1 = levels[i], levels[i + 1]
        for cx, cy in corners:
            box_beam(bm, (cx * half(z0), cy * half(z0), z0), (cx * half(z1), cy * half(z1), z1), w)
        for j in range(4):
            ax, ay = corners[j]
            bx, by = corners[(j + 1) % 4]
            # X bracing on every face
            box_beam(bm, (ax * half(z0), ay * half(z0), z0), (bx * half(z1), by * half(z1), z1), w * 0.55)
            box_beam(bm, (bx * half(z0), by * half(z0), z0), (ax * half(z1), ay * half(z1), z1), w * 0.55)
            box_beam(bm, (ax * half(z1), ay * half(z1), z1), (bx * half(z1), by * half(z1), z1), w * 0.6)
    # peak
    zt = levels[-1]
    for cx, cy in corners:
        box_beam(bm, (cx * half(zt), cy * half(zt), zt), (0, 0, h), w * 0.8)
    tips = []
    # cross-arms (triangular trusses): lower wide, upper narrower
    for za, span in ((h * 0.62, h * 0.36), (h * 0.78, h * 0.27)):
        hz = half(za)
        for s in (-1, 1):
            tip = (s * span, 0, za)
            for cy in (-1, 1):
                box_beam(bm, (s * hz, cy * hz, za), tip, w * 0.75)
                box_beam(bm, (s * hz, cy * hz, za + h * 0.07), tip, w * 0.55)
            box_beam(bm, (s * hz, 0, za + h * 0.07), (s * (hz + span) / 2, 0, za + h * 0.035), w * 0.4)
            tips.append(Vector(tip))
    ob = mesh_obj(name, bm, material)
    return ob, tips


def bolt_points(scale=1.0):
    pts = [(0.30, 1.0), (-0.44, -0.10), (-0.04, -0.10), (-0.30, -1.0), (0.46, 0.16), (0.06, 0.16)]
    return [(x * scale, y * scale) for x, y in pts]


def prism(name, pts2d, depth, material, plane="XZ", bevel=0.0):
    """Extrude a 2D polygon (in the XZ plane, facing -Y by default) by depth along +Y."""
    bm = bmesh.new()
    if plane == "XZ":
        vs = [bm.verts.new((x, -depth / 2, y)) for x, y in pts2d]
        off = Vector((0, depth, 0))
    else:  # XY, extrude along +Z
        vs = [bm.verts.new((x, y, -depth / 2)) for x, y in pts2d]
        off = Vector((0, 0, depth))
    f = bm.faces.new(vs)
    bm.normal_update()
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    nv = [g for g in ext["geom"] if isinstance(g, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, verts=nv, vec=off)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    ob = mesh_obj(name, bm, material)
    if bevel:
        bv = ob.modifiers.new("bevel", "BEVEL")
        bv.width = bevel
        bv.segments = 3
        bv.limit_method = "ANGLE"
        bv.angle_limit = math.radians(30)
        bv.harden_normals = False
    return ob


def tube_curve(name, points, radius, material, resolution=4):
    cd = bpy.data.curves.new(name, "CURVE")
    cd.dimensions = "3D"
    cd.bevel_depth = radius
    cd.bevel_resolution = resolution
    cd.use_fill_caps = True
    sp = cd.splines.new("POLY")
    sp.points.add(len(points) - 1)
    for p, c in zip(sp.points, points):
        p.co = (*c, 1)
    ob = link(bpy.data.objects.new(name, cd))
    ob.data.materials.append(material)
    return ob


def catenary(a, b, sag, n=40):
    a, b = Vector(a), Vector(b)
    return [a.lerp(b, t) - Vector((0, 0, sag * 4 * t * (1 - t))) for t in (i / n for i in range(n + 1))]


def skyline_profile(x0, x1, z0, rng, hmin, hmax, wmin, wmax):
    """Step outline of rooftops from x0 to x1 above baseline z0 -> polygon points (XZ) + building rects."""
    pts = [(x0, z0)]
    rects = []
    x = x0
    while x < x1:
        w = min(rng.uniform(wmin, wmax), x1 - x)
        hgt = rng.uniform(hmin, hmax)
        pts += [(x, z0 + hgt), (x + w, z0 + hgt)]
        rects.append((x, x + w, z0, z0 + hgt))
        x += w
    pts.append((x1, z0))
    # drop duplicate consecutive xs on equal heights
    clean = [pts[0]]
    for p in pts[1:]:
        if (abs(p[0] - clean[-1][0]) > 1e-6) or (abs(p[1] - clean[-1][1]) > 1e-6):
            clean.append(p)
    return clean, rects


def windows(bm, rects, y, rng, cell=0.34, win=0.17, density=0.55, inside=None, margin=0.18):
    for (xa, xb, za, zb) in rects:
        nx = int((xb - xa - 2 * margin) / cell)
        nz = int((zb - za - 2 * margin) / cell)
        for i in range(max(nx, 0)):
            for j in range(max(nz, 0)):
                if rng.random() > density:
                    continue
                cx = xa + margin + (i + 0.5) * cell + (xb - xa - 2 * margin - nx * cell) / 2
                cz = za + margin + (j + 0.5) * cell
                if inside and not inside(cx, cz):
                    continue
                box(bm, (cx, y, cz), (win, 0.04, win * 1.25))


def save(name):
    BLEND.mkdir(parents=True, exist_ok=True)
    OUT.mkdir(parents=True, exist_ok=True)
    sc = bpy.context.scene
    sc.render.filepath = str(OUT / f"{name}_raw.png")
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND / f"{name}.blend"), compress=True)
    bpy.ops.render.render(write_still=True)
    print("ICON_RENDERED", name, sc.render.filepath)


# ---------------------------------------------------------------- candidates
def c1():
    """Dusk pylon: black lattice tower against a sunset sky, teal live lines, warm city lights below."""
    reset()
    cam = camera((0, -60, 0.4), (0, 0, 0.6), lens=100)
    backdrop(cam, gradient_mat("Sky", [(0.0, "#FFB066"), (0.22, "#F07A45"), (0.42, "#8A3F5C"),
                                      (0.66, "#22304F"), (1.0, NAVY)], radial=0.5, center=(0.5, 0.25)))
    steel = mat("Steel", "#07111A", rough=0.6, metal=0.3)
    py, tips = pylon("Pylon", steel, h=15.0, base=2.6, w=0.42)
    py.location = (0, 0, -6.6)
    line_m = emission_mat("Line", TEAL, 3.0)
    ins_m = emission_mat("Insulator", "#7FF5EA", 4.0)
    for t in tips:
        t = t + py.location
        bot = t - Vector((0, 0, 1.0))
        tube_curve("Ins", [t, bot], 0.17, ins_m)
        s = 1 if t.x > 0 else -1
        tube_curve("Line", catenary(bot, (s * 22, -2, bot.z + 1.5), 2.2), 0.15, line_m)
    # city below, in front of the pylon foot
    rng = random.Random(7)
    city_m = mat("City", "#0B1622", rough=0.9)
    win_m = emission_mat("Win", "#FFC867", 3.0)
    for row, (y, zb, hmin, hmax, dens) in enumerate(((-4, -12.5, 3.6, 5.4, 0.45), (-8, -13.5, 3.0, 4.2, 0.55))):
        pts, rects = skyline_profile(-14, 14, zb, rng, hmin, hmax, 1.4, 2.6)
        prism(f"Sky{row}", pts, 1.0, city_m)
        bpy.data.objects[f"Sky{row}"].location.y = y
        bm = bmesh.new()
        windows(bm, rects, y - 0.55, rng, cell=0.55, win=0.28, density=dens)
        mesh_obj(f"Win{row}", bm, win_m)
    light("AREA", (0, 14, 6), 2500, "#FF9A5C", size=18, look_at=(0, 0, 2))  # warm back/rim light from the sunset
    light("AREA", (-10, -20, 14), 400, "#6FA8DC", size=10, look_at=(0, 0, 2))
    save("c1")


def c2():
    """Bolt needle on the 'Balanced' gauge: red|amber|green|amber|red arc (as the in-game dial), bolt = needle."""
    reset()
    cam = camera((0, -40, 0.45), (0, 0, 0.45), ortho=7.7)
    backdrop(cam, gradient_mat("BG", [(0.0, "#0A1721"), (0.55, "#17324D"), (1.0, "#0E2234")], radial=1.2,
                               center=(0.5, 0.55)))
    cz = -1.2
    r_in, r_out = 2.55, 3.55
    bands = [(-72, -40, RED), (-38, -22, AMBER), (-20, 20, GREEN), (22, 38, AMBER), (40, 72, RED)]
    for k, (a0, a1, col) in enumerate(bands):
        m = mat(f"Band{k}", col, rough=0.35, emit=col, emit_strength=0.35)
        bm = bmesh.new()
        n = max(4, int((a1 - a0) / 3))
        ring = []
        for i in range(n + 1):
            a = math.radians(a0 + (a1 - a0) * i / n)
            sx, cxz = math.sin(a), math.cos(a)
            ring.append([(sx * r, cz + cxz * r) for r in (r_in, r_out)])
        verts = []
        for (pi, po) in ring:
            verts.append([bm.verts.new((pi[0], y, pi[1])) for y in (-0.35, 0.35)] +
                         [bm.verts.new((po[0], y, po[1])) for y in (-0.35, 0.35)])
        for i in range(n):
            a, b = verts[i], verts[i + 1]
            bm.faces.new((a[0], b[0], b[2], a[2]))  # front
            bm.faces.new((a[3], b[3], b[1], a[1]))  # back
            bm.faces.new((a[2], b[2], b[3], a[3]))  # outer
            bm.faces.new((a[1], b[1], b[0], a[0]))  # inner
        bm.faces.new((verts[0][0], verts[0][2], verts[0][3], verts[0][1]))
        bm.faces.new((verts[-1][1], verts[-1][3], verts[-1][2], verts[-1][0]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        ob = mesh_obj(f"Band{k}", bm, m)
        bv = ob.modifiers.new("bevel", "BEVEL")
        bv.width = 0.12
        bv.segments = 3
        bv.limit_method = "ANGLE"
    # bolt needle: bottom tip on the hub, top reaching the green band
    bolt_m = mat("Bolt", AMBER, rough=0.3, emit="#FFB020", emit_strength=0.7)
    pts = [(x * 2.0 + 0.05, y * 2.05 + cz + 2.1) for x, y in bolt_points()]
    b = prism("Bolt", pts, 0.6, bolt_m, bevel=0.07)
    b.location.y = -0.5
    hub_m = mat("Hub", "#0E1E2E", rough=0.4, metal=0.5)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.62, depth=0.7, location=(0, -0.3, cz), rotation=(math.pi / 2, 0, 0))
    hub = bpy.context.object
    hub.data.materials.append(hub_m)
    bv = hub.modifiers.new("bevel", "BEVEL")
    bv.width = 0.12
    bv.segments = 3
    ring_m = emission_mat("HubRing", TEAL, 1.6)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.6, minor_radius=0.07, location=(0, -0.66, cz),
                                     rotation=(math.pi / 2, 0, 0))
    bpy.context.object.data.materials.append(ring_m)
    light("AREA", (-4, -12, 8), 900, "#FFFFFF", size=8, look_at=(0, 0, 0))
    light("AREA", (6, -6, -6), 200, TEAL, size=6, look_at=(0, 0, 0))
    save("c2")


def c3(plant="tower"):
    """Isometric mini island at dusk: plant -> teal line on a pylon -> glowing district.
    plant="tower": hyperbolic cooling tower (C3, rejected: reads as nuclear, IP_RULES 4);
    plant="steam": the game's own steam plant look, brick hall + banded chimney + white steam (C3b, chosen)."""
    reset()
    cam = (camera((22, -22, 20), (0, 0, 0.9), ortho=10.4) if plant == "tower"
           else camera((22, -22, 20), (0, 0, 1.55), ortho=11.0))
    sc = bpy.context.scene
    backdrop(cam, gradient_mat("Sea", [(0.0, "#0A1E2B"), (0.5, "#103447"), (1.0, "#0A1721")], radial=1.1),
             dist=150)
    sand = mat("Sand", "#D8B477", rough=0.9)
    grass = mat("Grass", "#4FA060", rough=0.9)
    for nm, (sx, sy, sz, z, m, bev) in {"Sand": (6.6, 6.6, 0.7, -0.45, sand, 0.9),
                                        "Grass": (6.0, 6.0, 0.8, -0.15, grass, 0.75)}.items():
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, z))
        o = bpy.context.object
        o.name = nm
        o.scale = (sx, sy, sz)
        bpy.ops.object.transform_apply(scale=True)
        bv = o.modifiers.new("bevel", "BEVEL")
        bv.width = bev
        bv.segments = 6
        bv.limit_method = "ANGLE"
        o.data.materials.append(m)
        shade_smooth(o)
    top = 0.25
    if plant == "steam":
        steam_plant(top)
    else:
        cooling_tower(top)
    district_and_line(top, plant)
    light("SUN", (-12, -16, 14), 3.4, "#FFC9A0", size=3, look_at=(0, 0, 0))
    light("SUN", (12, 10, 8), 0.9, "#7FA8D8", size=10, look_at=(0, 0, 0))
    sc.world.node_tree.nodes["Background"].inputs[0].default_value = hexc("#1B3550")
    sc.world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    save("c3" if plant == "tower" else "c3b")


def steam_plant(top):
    """Old-Smoky style: brick turbine hall with a dark roof, a tall white chimney with a red band, white-grey steam."""
    brick = mat("Brick", "#B4562F", rough=0.7)
    roof = mat("Roof", "#2A3442", rough=0.8)
    hx, hy, hw, hd, hh = -2.0, -1.0, 1.7, 1.5, 1.25
    for loc, sc_, m in (((hx, hy, top + hh / 2), (hw, hd, hh), brick),
                        ((hx, hy, top + hh + 0.07), (hw + 0.14, hd + 0.14, 0.15), roof)):
        bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
        o = bpy.context.object
        o.scale = sc_
        o.data.materials.append(m)
        bv = o.modifiers.new("bevel", "BEVEL")
        bv.width = 0.05
        bv.segments = 2
    # lit windows on the two camera-facing sides of the hall
    win_m = emission_mat("HallWin", "#FFC867", 3.0)
    wb = bmesh.new()
    for i in range(2):
        box(wb, (hx - 0.4 + i * 0.8, hy - hd / 2 - 0.01, top + 0.7), (0.4, 0.03, 0.46))
        box(wb, (hx + hw / 2 + 0.01, hy - 0.35 + i * 0.7, top + 0.7), (0.03, 0.4, 0.46))
    mesh_obj("HallWindows", wb, win_m)
    # chimney (behind-left of the hall), slightly tapered, white with a red band near the top
    cx, cy, ch = -2.55, 0.1, 3.4
    chim = mat("Chimney", "#E6EAEE", rough=0.6)
    band = mat("ChimBand", "#D9483B", rough=0.5)
    bpy.ops.mesh.primitive_cone_add(vertices=40, radius1=0.52, radius2=0.4, depth=ch, location=(cx, cy, top + ch / 2))
    o = bpy.context.object
    o.data.materials.append(chim)
    shade_smooth(o)
    for z in (ch - 0.75, ch - 1.35):
        bpy.ops.mesh.primitive_cylinder_add(vertices=40, radius=0.44, depth=0.32, location=(cx, cy, top + z))
        o = bpy.context.object
        o.data.materials.append(band)
        shade_smooth(o)
    steam = mat("Steam", "#E9EDF1", rough=1.0)
    for (dx, dy, dz, r) in ((0.05, 0.0, 0.4, 0.46), (0.5, 0.2, 0.8, 0.52), (1.05, 0.4, 1.0, 0.44)):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=(cx + dx, cy + dy, top + ch + dz), segments=32,
                                             ring_count=16)
        o = bpy.context.object
        o.data.materials.append(steam)
        shade_smooth(o)


def cooling_tower(top):
    # cooling tower (screen-left): hyperboloid lathe, light concrete with a teal band
    bm = bmesh.new()
    H, seg = 3.3, 40
    prof = []
    for i in range(15):
        t = i / 14
        r = 1.35 - 0.62 * math.sin(math.pi * min(t / 0.82, 1) * 0.72) + (0.16 * ((t - 0.82) / 0.18) if t > 0.82 else 0)
        prof.append((r, t * H))
    rings = [[bm.verts.new((r * math.cos(2 * math.pi * k / seg), r * math.sin(2 * math.pi * k / seg), z))
              for k in range(seg)] for r, z in prof]
    for a, b in zip(rings, rings[1:]):
        for k in range(seg):
            bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    ct = mesh_obj("CoolingTower", bm, mat("Concrete", "#CFD9E3", rough=0.8))
    ct.location = (-1.9, -1.3, top)
    sol = ct.modifiers.new("solid", "SOLIDIFY")
    sol.thickness = 0.1
    shade_smooth(ct)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.95, minor_radius=0.07, location=(-1.9, -1.3, top + 2.35))
    o = bpy.context.object
    o.scale = (0.99, 0.99, 1)
    o.data.materials.append(emission_mat("TowerBand", TEAL, 2.0))


def district_and_line(top, plant):
    # district (screen-right): chunky lit towers
    rng = random.Random(3)
    bld = mat("Bld", "#1E3048", rough=0.6)
    roof = mat("Roof", "#2C425E", rough=0.6)
    win_m = emission_mat("Win", "#FFC867", 3.4)
    wins = bmesh.new()
    spots = [(1.0, 1.7, 3.6, 1.25), (2.35, 0.45, 2.5, 1.25)]
    for (x, y, hgt, w) in spots:
        bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, top + hgt / 2))
        o = bpy.context.object
        o.scale = (w, w, hgt)
        o.data.materials.append(bld)
        bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, top + hgt + 0.05))
        o = bpy.context.object
        o.scale = (w * 1.05, w * 1.05, 0.1)
        o.data.materials.append(roof)
        h2 = w / 2 + 0.005
        for j in range(int((hgt - 0.3) / 0.42)):
            zc = top + 0.38 + j * 0.42
            for i in (-1, 1):
                if rng.random() < 0.9:
                    box(wins, (x + i * 0.3, y - h2, zc), (0.28, 0.03, 0.28))
                if rng.random() < 0.9:
                    box(wins, (x + h2, y + i * 0.3, zc), (0.03, 0.28, 0.28))
    mesh_obj("Windows", wins, win_m)
    bpy.ops.mesh.primitive_plane_add(size=(3.4 if plant == "tower" else 2.9), location=((1.4, 1.6) if plant == "tower" else (1.55, 1.25)) + (top + 0.01,))
    bpy.context.object.data.materials.append(emission_mat("Glow", "#FFB347", 0.7))
    # one pylon in the middle carrying a teal line from the plant to the district
    steel = mat("Steel", "#9FB2C6", rough=0.5, metal=0.4)
    line_m = emission_mat("Line", TEAL, 4.0)
    p, tips = pylon("Pylon", steel, h=3.2, base=0.5, w=0.15)
    px, py_ = (0.15, -0.9) if plant == "tower" else (0.55, -0.55)
    p.location = (px, py_, top)
    p.rotation_euler.z = math.radians(45)
    pa = Vector((px, py_, top + 3.2 * 0.62))
    a = (Vector((-1.2, -0.9, top + 1.35)) if plant == "steam" else Vector((-1.9 + 0.5, -1.3 + 0.2, top + 2.0)))
    b = Vector((1.0 - 0.45, 1.6 - 0.55, top + 2.6))
    for u, v in ((a, pa), (pa, b)):
        tube_curve("Line", catenary(u, v, 0.2, 16), 0.11, line_m)


def c4():
    """Light bulb whose glow is a lit skyline: the city's lights = the grid's job. Teal screw base."""
    reset()
    cam = camera((0, -40, -0.1), (0, 0, -0.1), ortho=9.7)
    backdrop(cam, gradient_mat("BG", [(0.0, "#08131C"), (0.6, "#14304A"), (1.0, "#0B1B29")], radial=1.1,
                               center=(0.5, 0.6)))
    cz, R = 1.1, 2.75
    neck_z, neck_r = -1.75, 1.15

    def bulb_r(z):
        if z >= cz - 0.5 * R:
            return math.sqrt(max(R * R - (z - cz) ** 2, 0))
        # smooth taper from the sphere to the neck
        z0 = cz - 0.5 * R
        r0 = math.sqrt(R * R - (z0 - cz) ** 2)
        t = (z0 - z) / (z0 - neck_z)
        t = min(max(t, 0), 1)
        s = t * t * (3 - 2 * t)
        return r0 + (neck_r - r0) * s

    # glowing glass: lathe solid
    bm = bmesh.new()
    seg = 64
    zs = [cz + R - (cz + R - neck_z) * i / 60 for i in range(61)]
    rings = []
    for z in zs:
        r = bulb_r(z)
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * k / seg), r * math.sin(2 * math.pi * k / seg) * 0.5, z))
                      for k in range(seg)])
    for a, b in zip(rings, rings[1:]):
        for k in range(seg):
            bm.faces.new((a[k], b[k], b[(k + 1) % seg], a[(k + 1) % seg]))
    bm.faces.new(rings[-1])
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-4)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    glass = gradient_mat("Glass", [(0.0, "#F28A2E"), (0.45, "#FFC24D"), (1.0, "#FFF2C4")])
    bulb = mesh_obj("Bulb", bm, glass)
    shade_smooth(bulb)
    # skyline silhouette clipped to the bulb (inset so a glowing rim stays)
    rng = random.Random(11)
    inset = 0.86

    def inside(x, z):
        return abs(x) < bulb_r(z) * inset - 0.05

    zb = neck_z - 0.1
    # hand-tuned rooftops so the silhouette reads: low - mid - TALL - mid - low
    tops = [(-3, -1.7, 0.1), (-1.7, -1.05, 1.05), (-1.05, -0.35, 0.35), (-0.35, 0.45, 2.15), (0.45, 1.1, 0.85),
            (1.1, 1.8, 1.45), (1.8, 3.0, 0.2)]
    pts = [(-3, zb)]
    rects = []
    for xa, xb, zt in tops:
        pts += [(xa, zt), (xb, zt)]
        rects.append((xa, xb, zb, zt))
    pts.append((3, zb))
    city_m = mat("City", "#0A1721", rough=0.8)
    sky = prism("Skyline", pts, 1.0, city_m)
    sky.location.y = -1.6
    clip_bm = bmesh.new()
    rings2 = []
    for z in zs:
        r = bulb_r(z) * inset
        rings2.append([clip_bm.verts.new((r * math.cos(2 * math.pi * k / seg), r * math.sin(2 * math.pi * k / seg) * 3,
                                          z)) for k in range(seg)])
    for a, b in zip(rings2, rings2[1:]):
        for k in range(seg):
            clip_bm.faces.new((a[k], b[k], b[(k + 1) % seg], a[(k + 1) % seg]))
    clip_bm.faces.new(rings2[-1])
    clip_bm.faces.new(list(reversed(rings2[0])))
    bmesh.ops.remove_doubles(clip_bm, verts=clip_bm.verts[:], dist=1e-4)
    bmesh.ops.recalc_face_normals(clip_bm, faces=clip_bm.faces[:])
    clip = mesh_obj("Clip", clip_bm)
    clip.hide_render = True
    clip.hide_viewport = True
    bo = sky.modifiers.new("clip", "BOOLEAN")
    bo.operation = "INTERSECT"
    bo.object = clip
    bo.solver = "EXACT"
    win_m = emission_mat("Win", "#FFD27A", 2.2)
    bm = bmesh.new()
    windows(bm, rects, -2.15, rng, cell=0.34, win=0.15, density=0.6, inside=inside, margin=0.16)
    mesh_obj("Windows", bm, win_m)
    # screw base (teal metal) + contact
    metal_m = mat("Base", "#2EC4B6", rough=0.3, metal=0.85)
    dark_m = mat("Contact", "#0E1E2E", rough=0.5, metal=0.3)
    z = neck_z + 0.05
    for i in range(4):
        r = neck_r * (1 - 0.035 * i)
        bpy.ops.mesh.primitive_torus_add(major_radius=r - 0.12, minor_radius=0.17, location=(0, 0, z - 0.33 * i),
                                         major_segments=48, minor_segments=12)
        o = bpy.context.object
        o.scale.y = 0.5
        o.data.materials.append(metal_m)
        shade_smooth(o)
    bpy.ops.mesh.primitive_cylinder_add(radius=neck_r * 0.92, depth=1.3, location=(0, 0, z - 0.5), vertices=48)
    o = bpy.context.object
    o.scale.y = 0.5
    o.data.materials.append(metal_m)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.62, depth=0.5, location=(0, 0, z - 1.35), vertices=48)
    o = bpy.context.object
    o.scale.y = 0.5
    o.data.materials.append(dark_m)
    bv = o.modifiers.new("bevel", "BEVEL")
    bv.width = 0.15
    bv.segments = 4
    light("AREA", (-5, -12, 4), 700, "#FFFFFF", size=8, look_at=(0, 0, -2))
    light("POINT", (0, -3, 1), 300, "#FFC24D", size=1.5)
    save("c4")


def c5():
    """Grid-keeper hard hat (no face) with a navy bolt emblem, night skyline with lit windows behind."""
    reset()
    cam = camera((9.0, -13.5, 6.0), (0, 0, 0.75), lens=74)
    sc = bpy.context.scene
    backdrop(cam, gradient_mat("BG", [(0.0, "#0C2A36"), (0.35, "#12263A"), (1.0, NAVY)], radial=0.7,
                               center=(0.5, 0.35)), dist=60)
    hat_m = mat("Hat", "#FFAA14", rough=0.3, coat=0.5)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=2.3, segments=64, ring_count=32, location=(0, 0, 0))
    dome = bpy.context.object
    dome.name = "Dome"
    dome.scale = (1.0, 1.08, 0.92)
    bpy.ops.object.transform_apply(scale=True)
    bm = bmesh.new()
    bm.from_mesh(dome.data)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, 0.02),
                           plane_no=(0, 0, -1), clear_outer=True, clear_inner=False)
    bm.to_mesh(dome.data)
    bm.free()
    dome.data.materials.append(hat_m)
    shade_smooth(dome)
    bpy.ops.mesh.primitive_cylinder_add(radius=1, depth=0.16, vertices=96, location=(0, -0.75, 0.02))
    brim = bpy.context.object
    brim.scale = (2.42, 3.25, 1)
    brim.data.materials.append(hat_m)
    bv = brim.modifiers.new("bevel", "BEVEL")
    bv.width = 0.07
    bv.segments = 4
    shade_smooth(brim)
    bpy.ops.mesh.primitive_cylinder_add(radius=1, depth=0.26, vertices=96, location=(0, 0, 0.18))
    band = bpy.context.object
    band.scale = (2.33, 2.52, 1)
    band.data.materials.append(mat("BandDark", "#D98A12", rough=0.35, coat=0.4))
    shade_smooth(band)
    # low ridge over the crown only (front kept clear for the emblem)
    pts = []
    for i in range(41):
        th = math.radians(68 + 112 * i / 40)
        pts.append((0, -2.46 * math.cos(th), 2.1 * math.sin(th)))
    tube_curve("Ridge", pts, 0.15, hat_m, resolution=6)
    # bolt emblem facing the camera, hugging the dome
    bolt_m = mat("Emblem", NAVY, rough=0.4)
    bp = [(x * 0.95, y * 0.95 + 1.05) for x, y in bolt_points()]
    em = prism("Emblem", bp, 8.0, bolt_m)
    em.rotation_euler.z = math.atan2(9.0, 13.5)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=2.3, segments=96, ring_count=48, location=(0, 0, 0))
    infl = bpy.context.object
    infl.name = "EmblemShell"
    infl.scale = (1.03, 1.08 * 1.03, 0.92 * 1.03)
    infl.hide_render = True
    infl.hide_viewport = True
    bo = em.modifiers.new("hug", "BOOLEAN")
    bo.operation = "INTERSECT"
    bo.object = infl
    bo.solver = "EXACT"
    shade_smooth(em)
    # skyline as a flat layer parented to the camera (screen-aligned, lower third of the frame, behind the hat)
    rng = random.Random(5)
    city_m = mat("City", "#06121B", rough=0.9)
    win_m = emission_mat("Win", "#FFC867", 2.6)
    pts2, rects = skyline_profile(-12, 12, -12, rng, 4.2, 7.0, 2.0, 3.4)
    s = prism("Skyline", pts2, 0.5, city_m)
    bm = bmesh.new()
    windows(bm, rects, -0.3, rng, cell=0.95, win=0.45, density=0.3)
    w = mesh_obj("Windows", bm, win_m)
    for o in (s, w):
        o.parent = cam
        o.rotation_euler = (math.radians(-90), 0, 0)
        o.location = (0, 0, -40)
        no_light_contrib(o)
    light("AREA", (2, 9, 5), 1300, "#A8F0EA", size=5, look_at=(0, 0, 0.8))
    light("AREA", (-6, -11, 10), 1400, "#FFF4E0", size=8, look_at=(0, 0, 0.6))
    light("AREA", (-7, 6, 3), 500, "#FF9A5C", size=6, look_at=(0, 0, 0.6))
    sc.world.node_tree.nodes["Background"].inputs[1].default_value = 0.4
    save("c5")


def c6():
    """Storm strike: neon-teal transmission tower, dark storm cloud, amber lightning hitting the peak."""
    reset()
    cam = camera((0, -60, 0.6), (0, 0, 0.6), lens=92)
    backdrop(cam, gradient_mat("BG", [(0.0, "#0B2A33"), (0.45, "#101E33"), (1.0, "#1C1934")], radial=0.7,
                               center=(0.5, 0.6)))
    tower_m = mat("Tower", "#0E3A3A", rough=0.4, emit=TEAL, emit_strength=2.2)
    py, tips = pylon("Pylon", tower_m, h=12.0, base=2.5, w=0.46)
    py.location = (1.6, 0, -10.2)
    line_m = emission_mat("Line", TEAL, 2.0)
    for t in tips:
        t = t + py.location
        bot = t - Vector((0, 0, 0.9))
        tube_curve("Ins", [t, bot], 0.16, line_m)
        s = 1 if t.x > py.location.x else -1
        tube_curve("Line", catenary(bot, (py.location.x + s * 22, -2, bot.z + 1), 2.0), 0.08, line_m)
    # storm cloud: lumpy cluster, lit from below by the strike
    cloud_m = mat("Cloud", "#3A4A70", rough=1.0)
    rng = random.Random(4)
    blobs = [(-3.2, 6.6, 2.4), (-0.6, 7.3, 2.6), (1.9, 7.0, 2.2), (4.4, 6.6, 1.8), (-5.4, 5.9, 1.7), (0.9, 5.7, 1.9),
             (-2.0, 5.6, 1.9), (3.4, 5.6, 1.6), (-4.0, 8.4, 1.6), (0.6, 8.8, 1.8)]
    for (x, z, r) in blobs:
        bpy.ops.mesh.primitive_uv_sphere_add(radius=r * 1.25, location=(x * 1.2 - 0.6, 2.5 + rng.uniform(-0.5, 0.5),
                                             z + 1.6), segments=32,
                                             ring_count=16)
        o = bpy.context.object
        o.scale.z = 0.8
        o.data.materials.append(cloud_m)
        shade_smooth(o)
    # lightning: jagged thick path from the cloud to the tower peak + a branch
    peak = Vector((py.location.x, 0, py.location.z + 12.0))
    start = Vector((-1.6, -1.0, 6.4))
    pts = [start]
    n = 5
    for i in range(1, n):
        t = i / n
        p = start.lerp(peak + Vector((0, 0, 0.2)), t)
        p.x += (-1.4 if i % 2 else 1.4)
        pts.append(p)
    pts.append(peak + Vector((0, 0, 0.2)))
    core_m = emission_mat("Bolt", "#FFF6D6", 8.0)
    halo_m = emission_mat("BoltHalo", AMBER, 2.0)
    tube_curve("Strike", pts, 0.38, core_m)
    tube_curve("StrikeHalo", [p + Vector((0, 0.5, 0)) for p in pts], 0.75, halo_m)
    tube_curve("Branch", [pts[2], pts[2] + Vector((-1.6, 0, -0.9)), pts[2] + Vector((-1.9, 0, -2.0))], 0.09, core_m)
    light("POINT", (0.0, -3, 4.0), 1500, AMBER, size=1.0)
    light("AREA", (-4, -14, 0), 450, "#9DB4E8", size=10, look_at=(-1, 0, 7))
    light("AREA", (0, -10, -2), 250, TEAL, size=10, look_at=(0, 0, 4))
    save("c6")


ALL = {"c1": c1, "c2": c2, "c3": c3, "c3b": lambda: c3("steam"), "c4": c4, "c5": c5, "c6": c6}
for k in (ONLY or list(ALL)):
    ALL[k]()
