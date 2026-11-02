"""Charge Nurse — Blender kit (fleet: buildings / machines / vehicles = Blender, characters = Meshy). Builds low-poly props
whose UVs point at swatches of the game palette (unity/Assets/Resources/materials/palette.png, order = ChargeNurse.World.Pal),
so the whole town shares ONE material. Bevelled boxes + cylinders, 1 unit = 1 m, pivot bottom-centre, front = -Y in
Blender (Unity -Z after the FBX axis swap). Exports FBX into unity/Assets/Resources/kit/ and a contact sheet.
  blender -b --python charge-nurse/tools/art/kit.py -- <unity dir> [<sheet.png>]
Budget (Low draws: one material, one mesh per prop): machine ≤ 400 tris, vehicle ≤ 900, building ≤ 600.
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

# swatches (ChargeNurse.World.Pal)
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
    if SHEET:
        sheet(objs)


main()
