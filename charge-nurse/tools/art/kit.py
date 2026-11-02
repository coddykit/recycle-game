"""Charge Nurse — Blender kit (fleet: buildings / furniture / machines / vehicles = Blender, characters = Meshy). Builds
low-poly hospital props whose UVs point at swatches of the game palette (unity/Assets/Resources/materials/palette.png,
order = ChargeNurse.World.Pal), so the whole campus shares ONE material (a theme = a palette swap). Bevelled boxes +
cylinders, 1 unit = 1 m, pivot bottom-centre, front = -Y in Blender (Unity -Z after the FBX axis swap: the side the
camera sees), head of a bed at +Y. Exports FBX into unity/Assets/Resources/kit/ and a contact sheet. No text, no logos,
no real brand or emergency-service emblems (the ambulance carries plain stripes only).
  blender -b --python charge-nurse/tools/art/kit.py -- <unity dir> [<sheet.png>]
Budget (Low draws: one material, one mesh per prop): furniture ≤ 500 tris, machine ≤ 700, vehicle ≤ 900.
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
GRASS, PAVING, FLOOR, CONCRETE, CREAM, MINT, BLUEWALL, ROOF, ROOFGREY = 0, 2, 3, 4, 5, 6, 7, 8, 9
BRAND, PANEL, LINEN, CURTAIN, GLASS, WOOD, WINDOW = 10, 11, 12, 13, 15, 16, 20
TAGRED, SIREN, MATTRESS, MACHINE, SCREEN, RUBBER, WHITE = 24, 25, 41, 42, 43, 44, 45


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


def bed(bay):
    p = Prop("bay" if bay else "bed")
    p.box((0, 0, 0.18), (0.95, 2.05, 0.32), PANEL, top=MATTRESS, bevel=0.04)          # frame + mattress
    p.box((0, -0.15, 0.5), (0.9, 1.5, 0.1), LINEN, bevel=0.03)                         # blanket
    p.box((0, 0.78, 0.52), (0.62, 0.32, 0.14), WHITE, bevel=0.05)                      # pillow
    p.box((0, 1.0, 0.0), (1.0, 0.07, 1.0), PANEL, bevel=0.02)                          # headboard
    for x in (-0.4, 0.4):
        for y in (-0.9, 0.9):
            p.cyl((x, y, 0.09), 0.08, 0.06, RUBBER, segs=8, axis="X")
    if bay:
        p.box((-0.78, 0, 0), (0.04, 2.2, 2.0), CURTAIN)                                # curtain
        p.box((0.75, 0.75, 0), (0.3, 0.3, 1.15), MACHINE, bevel=0.03)                  # monitor stand
        p.box((0.75, 0.75, 1.15), (0.45, 0.12, 0.34), MACHINE, bevel=0.02)
        p.box((0.75, 0.68, 1.19), (0.38, 0.02, 0.26), SCREEN)
    return p.build()


def desk():
    p = Prop("desk")
    p.box((0, 0, 0), (2.6, 0.8, 1.05), BRAND, top=WOOD, bevel=0.04)
    p.box((0, -0.42, 0.15), (2.4, 0.04, 0.6), PANEL)
    p.box((-0.6, 0.1, 1.05), (0.5, 0.06, 0.35), MACHINE)
    p.box((-0.6, 0.06, 1.1), (0.44, 0.02, 0.27), SCREEN)
    p.box((0.7, 0.05, 1.05), (0.35, 0.25, 0.06), WHITE)                                # papers
    return p.build()


def chairs():
    p = Prop("chairs")
    for i in range(4):
        x = (i - 1.5) * 0.9
        p.box((x, 0, 0.0), (0.06, 0.06, 0.42), MACHINE)
        p.box((x, 0, 0.42), (0.7, 0.62, 0.08), BRAND, bevel=0.03)
        p.box((x, 0.28, 0.5), (0.7, 0.08, 0.5), BRAND, bevel=0.03)
    p.box((0, 0, 0.0), (3.4, 0.12, 0.08), MACHINE)
    return p.build()


def ambulance():
    p = Prop("ambulance")
    p.box((0, -1.6, 0.35), (1.95, 1.2, 1.55), WHITE, bevel=0.12)                       # cab
    p.box((0, 0.5, 0.35), (2.1, 3.2, 2.2), WHITE, bevel=0.08)                         # box
    p.box((0, 0.5, 1.15), (2.14, 3.0, 0.3), TAGRED)                                    # plain stripe, no emblem
    p.box((0, -2.21, 1.2), (1.6, 0.04, 0.55), WINDOW)
    p.box((0, -0.6, 2.55), (1.1, 0.3, 0.16), SIREN, bevel=0.03)                        # light bar
    for x in (-0.95, 0.95):
        for y in (-1.55, 1.35):
            p.wheel(x, y)
    return p.build()


def xray():
    p = Prop("xray")
    p.box((0, 0, 0), (0.8, 2.0, 0.75), MACHINE, top=LINEN, bevel=0.04)                # table
    p.box((0, 1.15, 0), (0.35, 0.35, 2.4), PANEL, bevel=0.03)                          # column
    p.box((0, 0.55, 1.75), (0.55, 1.2, 0.35), PANEL, top=BRAND, bevel=0.04)            # arm
    p.box((0, 0.1, 1.55), (0.45, 0.45, 0.2), MACHINE)                                  # tube head
    return p.build()


def lab():
    p = Prop("lab")
    p.box((0, 0, 0), (3.0, 0.8, 0.92), PANEL, top=WHITE, bevel=0.03)
    for i in range(3):
        p.box((-1 + i, 0.05, 0.92), (0.5, 0.5, 0.45), MACHINE, bevel=0.03)
        p.box((-1 + i, -0.21, 1.1), (0.34, 0.02, 0.18), SCREEN)
    p.box((1.2, 0.2, 0.92), (0.3, 0.2, 0.25), GLASS)                                   # sample rack
    return p.build()


def ct():
    p = Prop("ct")
    p.box((0, 0.65, 0), (2.4, 0.9, 2.2), WHITE, bevel=0.1)                            # gantry
    p.cyl((0, 0.65, 1.15), 0.48, 0.95, WINDOW, segs=16, axis="Y")                      # the ring opening
    p.box((0, -0.8, 0), (0.7, 2.0, 0.72), MACHINE, top=LINEN, bevel=0.04)             # couch
    return p.build()


def or_table():
    p = Prop("or_table")
    p.box((0, 0, 0), (0.35, 0.35, 0.75), MACHINE)
    p.box((0, 0, 0.75), (0.85, 2.0, 0.15), MACHINE, top=CURTAIN, bevel=0.03)
    p.box((0, 0, 0), (0.75, 0.75, 0.08), MACHINE)
    p.box((0, 0, 2.1), (0.08, 0.08, 0.5), PANEL)                                       # lamp arm
    p.cyl((0, 0, 2.05), 0.55, 0.12, PANEL, segs=14)                                    # lamp
    p.box((1.2, 0.6, 0), (0.6, 0.5, 1.3), MACHINE, bevel=0.03)                         # anaesthesia cart
    p.box((1.2, 0.33, 1.0), (0.45, 0.02, 0.25), SCREEN)
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
    x = -10.0
    for ob in objs:
        ob.location = (x, 0, 0)
        x += 4.5
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 44
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
    objs = [bed(True), bed(False), desk(), chairs(), ambulance(), xray(), lab(), ct(), or_table()]
    total = 0
    for ob in objs:
        ob.data.materials.append(mat)
        total += export(ob)
    print("KIT_DONE props", len(objs), "tris", total)
    if SHEET:
        sheet(objs)


main()
