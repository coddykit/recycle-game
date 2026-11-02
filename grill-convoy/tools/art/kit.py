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
        x += 6.0 if ob.name not in ("commissary",) else 18.0
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
    objs = [truck("truck_starter", False), truck("truck_box", True), parasol(), menu_board(), commissary()]
    total = 0
    for ob in objs:
        ob.data.materials.append(mat)
        total += export(ob)
    print("KIT_DONE props", len(objs), "tris", total)
    if SHEET:
        sheet(objs)


main()
