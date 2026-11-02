"""Procedural low-poly asset builder for Borderline Empire.

Run with Blender 4.x:
    blender -b -P tools/blender/build_assets.py
or with the `bpy` Python module:
    python tools/blender/build_assets.py

Every model is built from bevelled primitives and UV-mapped onto the shared
8x8 palette texture (tools/palette.py), then exported as GLB into
res://assets/models/. Model bounding boxes are written to
res://data/model_meta.json so gameplay code can place attachments.

Conventions (Blender space):
  * Z up, ground at z = 0, pivot at the footprint centre.
  * Vehicles and building fronts face +Y (becomes -Z / "forward" in Godot).
  * 1 unit = 1 Godot unit. A road is 2 units wide, a hatchback 1.6 long.
"""
import json
import math
import os
import sys

import bpy  # noqa: I001  (bpy must be imported before bmesh when used as a module)
import bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "tools"))
from palette import uv_for, write_png  # noqa: E402

PALETTE_PNG = os.path.join(ROOT, "assets", "textures", "palette.png")
OUT_DIR = os.path.join(ROOT, "assets", "models")
META_PATH = os.path.join(ROOT, "data", "model_meta.json")

META = {}
MAT = None
QUALITY_LOD = 1
SELECTED = set()


# --------------------------------------------------------------------------
# scene helpers
# --------------------------------------------------------------------------
def reset_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)


def palette_material():
    mat = bpy.data.materials.new("Palette")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(PALETTE_PNG)
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    return mat


class MB:
    """Mesh builder: accumulates coloured primitives into one bmesh."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    # -- internals ---------------------------------------------------------
    def _paint(self, verts, color):
        u, v = uv_for(color)
        faces = set()
        for vert in verts:
            for f in vert.link_faces:
                faces.add(f)
        for f in faces:
            for loop in f.loops:
                loop[self.uv].uv = (u, v)

    def _place(self, verts, loc, rot):
        rx, ry, rz = rot
        if rx:
            bmesh.ops.rotate(self.bm, verts=verts, cent=(0, 0, 0), matrix=Matrix.Rotation(rx, 3, "X"))
        if ry:
            bmesh.ops.rotate(self.bm, verts=verts, cent=(0, 0, 0), matrix=Matrix.Rotation(ry, 3, "Y"))
        if rz:
            bmesh.ops.rotate(self.bm, verts=verts, cent=(0, 0, 0), matrix=Matrix.Rotation(rz, 3, "Z"))
        bmesh.ops.translate(self.bm, verts=verts, vec=Vector(loc))

    def _bevel(self, verts, amount):
        if amount <= 0:
            return verts
        edges = list({e for v in verts for e in v.link_edges})
        res = bmesh.ops.bevel(self.bm, geom=list(verts) + edges, offset=amount, segments=1,
                              profile=0.5, affect="EDGES", clamp_overlap=True)
        new_verts = list(res["verts"])
        # gather the whole primitive again (bevel replaces corner verts)
        connected = set(new_verts)
        for f in res["faces"]:
            connected.update(f.verts)
        frontier = list(connected)
        while frontier:
            v = frontier.pop()
            for e in v.link_edges:
                o = e.other_vert(v)
                if o not in connected:
                    connected.add(o)
                    frontier.append(o)
        return list(connected)

    # -- primitives ----------------------------------------------------------
    def box(self, size, loc, color, rot=(0, 0, 0), bevel=0.0, taper=None):
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = res["verts"]
        bmesh.ops.scale(self.bm, vec=Vector(size), verts=verts)
        if taper:
            for v in verts:
                if v.co.z > 0:
                    v.co.x *= taper[0]
                    v.co.y *= taper[1]
        verts = self._bevel(verts, bevel)
        self._paint(verts, color)
        self._place(verts, loc, rot)
        return self

    def cyl(self, r, depth, loc, color, axis="Z", segs=8, r2=None, rot=(0, 0, 0), bevel=0.0):
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segs,
                                    radius1=r, radius2=r if r2 is None else r2, depth=depth)
        verts = res["verts"]
        if axis == "X":
            bmesh.ops.rotate(self.bm, verts=verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, "Y"))
        elif axis == "Y":
            bmesh.ops.rotate(self.bm, verts=verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, "X"))
        verts = self._bevel(verts, bevel)
        self._paint(verts, color)
        self._place(verts, loc, rot)
        return self

    def cone(self, r, depth, loc, color, segs=7, r2=0.0, rot=(0, 0, 0)):
        return self.cyl(r, depth, loc, color, segs=segs, r2=r2, rot=rot)

    def ball(self, r, loc, color, subdiv=1, scale=(1, 1, 1), rot=(0, 0, 0)):
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=r)
        verts = res["verts"]
        bmesh.ops.scale(self.bm, vec=Vector(scale), verts=verts)
        self._paint(verts, color)
        self._place(verts, loc, rot)
        return self

    def gable(self, w, d, h, loc, color, rot=(0, 0, 0), overhang=0.08):
        """Triangular prism roof; ridge runs along Y."""
        w2 = w / 2 + overhang
        d2 = d / 2 + overhang
        pts = [(-w2, -d2, 0), (w2, -d2, 0), (0, -d2, h), (-w2, d2, 0), (w2, d2, 0), (0, d2, h)]
        verts = [self.bm.verts.new(p) for p in pts]
        a0, a1, a2, b0, b1, b2 = verts
        for face in ((a0, a2, a1), (b0, b1, b2), (a0, a1, b1, b0), (a1, a2, b2, b1), (a2, a0, b0, b2)):
            self.bm.faces.new(face)
        self._paint(verts, color)
        self._place(verts, loc, rot)
        return self

    def build(self, name, collection, loc=(0, 0, 0)):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        for p in me.polygons:
            p.use_smooth = False
        me.materials.append(MAT)
        obj = bpy.data.objects.new(name, me)
        obj.location = loc
        collection.objects.link(obj)
        return obj


def new_collection(name):
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)
    return col


def export(name, collection):
    os.makedirs(OUT_DIR, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    objs = list(collection.objects)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    path = os.path.join(OUT_DIR, name + ".glb")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True,
                              export_yup=True, export_apply=True, export_texcoords=True,
                              export_normals=True, export_materials="EXPORT",
                              export_animations=False)
    # bounding box in Godot axes (x, z, -y)
    mn = [1e9, 1e9, 1e9]
    mx = [-1e9, -1e9, -1e9]
    for o in objs:
        for corner in o.bound_box:
            w = o.matrix_world @ Vector(corner)
            g = (w.x, w.z, -w.y)
            for i in range(3):
                mn[i] = min(mn[i], g[i])
                mx[i] = max(mx[i], g[i])
    META[name] = {"min": [round(v, 3) for v in mn], "max": [round(v, 3) for v in mx],
                  "size": [round(mx[i] - mn[i], 3) for i in range(3)]}
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.collections.remove(collection)
    print("exported", name)


def single(name, build_fn):
    if SELECTED and name not in SELECTED:
        return
    col = new_collection(name)
    mb = MB()
    build_fn(mb)
    mb.build(name, col)
    export(name, col)


# --------------------------------------------------------------------------
# vehicles
# --------------------------------------------------------------------------
def wheel_obj(col, name, r, w, loc, rim="chrome"):
    mb = MB()
    mb.cyl(r, w, (0, 0, 0), "tire", axis="X", segs=10)
    mb.cyl(r * 0.55, w + 0.02, (0, 0, 0), rim, axis="X", segs=8)
    return mb.build(name, col, loc)


def wheels(col, xs, ys, r, w, rim="chrome"):
    i = 0
    for y in ys:
        for x in xs:
            side = "L" if x < 0 else "R"
            wheel_obj(col, "Wheel_%d%s" % (i, side), r, w, (x, y, r), rim)
        i += 1


def lights(mb, front_y, back_y, z, xo, size=(0.16, 0.05, 0.08)):
    for x in (-xo, xo):
        mb.box(size, (x, front_y, z), "lamp")
        mb.box(size, (x, back_y, z), "taillight")


def vehicle(name, body_fn, wheel_fn):
    col = new_collection(name)
    mb = MB()
    body_fn(mb)
    mb.build("Body", col)
    wheel_fn(col)
    export(name, col)


def build_vehicles():
    # Old Hatchback ---------------------------------------------------------
    def hatch(mb):
        mb.box((0.9, 1.6, 0.36), (0, 0, 0.37), "paint", bevel=0.06)
        mb.box((0.8, 0.86, 0.32), (0, -0.14, 0.7), "glass", taper=(0.9, 0.78), bevel=0.03)
        mb.box((0.78, 0.66, 0.06), (0, -0.18, 0.87), "paint", bevel=0.02)
        mb.box((0.94, 0.1, 0.12), (0, 0.8, 0.25), "darkgray", bevel=0.02)
        mb.box((0.94, 0.1, 0.12), (0, -0.8, 0.25), "darkgray", bevel=0.02)
        mb.box((0.5, 0.04, 0.1), (0, 0.81, 0.4), "charcoal")
        lights(mb, 0.79, -0.79, 0.45, 0.32)
        mb.box((0.06, 0.12, 0.05), (0.47, 0.25, 0.62), "paint")
        mb.box((0.06, 0.12, 0.05), (-0.47, 0.25, 0.62), "paint")
    vehicle("hatchback", hatch, lambda c: wheels(c, (-0.42, 0.42), (0.5, -0.5), 0.2, 0.16))

    # Small Van -------------------------------------------------------------
    def small_van(mb):
        mb.box((0.96, 1.8, 0.78), (0, -0.05, 0.6), "paint", bevel=0.07)
        mb.box((0.84, 0.1, 0.32), (0, 0.86, 0.78), "glass", bevel=0.02)
        mb.box((0.98, 0.42, 0.26), (0, 0.5, 0.8), "glass")
        mb.box((0.98, 1.0, 0.1), (0, -0.3, 0.52), "white")
        mb.box((0.98, 0.1, 0.14), (0, 0.9, 0.26), "darkgray", bevel=0.02)
        mb.box((0.98, 0.1, 0.14), (0, -0.98, 0.26), "darkgray", bevel=0.02)
        mb.box((0.02, 0.5, 0.5), (0.49, -0.45, 0.62), "gray")
        lights(mb, 0.9, -0.97, 0.45, 0.34)
    vehicle("small_van", small_van, lambda c: wheels(c, (-0.44, 0.44), (0.58, -0.6), 0.21, 0.16))

    # Cargo Van -------------------------------------------------------------
    def cargo_van(mb):
        mb.box((1.0, 2.1, 0.9), (0, -0.05, 0.68), "paint", bevel=0.07)
        mb.box((0.9, 0.5, 0.38), (0, 0.85, 0.5), "paint", bevel=0.06)
        mb.box((0.86, 0.14, 0.3), (0, 0.98, 0.85), "glass", taper=(1, 0.5))
        mb.box((1.02, 0.36, 0.26), (0, 0.72, 0.9), "glass")
        mb.box((1.02, 1.3, 0.12), (0, -0.4, 0.62), "yellow")
        mb.box((1.02, 0.1, 0.14), (0, 1.1, 0.26), "darkgray", bevel=0.02)
        mb.box((1.02, 0.1, 0.14), (0, -1.12, 0.26), "darkgray", bevel=0.02)
        mb.box((0.04, 0.04, 0.8), (0, -1.11, 0.7), "charcoal")
        lights(mb, 1.09, -1.11, 0.46, 0.36)
    vehicle("cargo_van", cargo_van, lambda c: wheels(c, (-0.46, 0.46), (0.68, -0.68), 0.22, 0.17))

    # Pickup ----------------------------------------------------------------
    def pickup(mb):
        mb.box((1.0, 2.0, 0.34), (0, 0, 0.42), "paint", bevel=0.06)
        mb.box((0.96, 0.8, 0.44), (0, 0.25, 0.8), "paint", bevel=0.06)
        mb.box((0.98, 0.5, 0.24), (0, 0.27, 0.86), "glass")
        mb.box((0.86, 0.12, 0.26), (0, 0.66, 0.84), "glass", taper=(1, 0.4))
        for x in (-0.47, 0.47):
            mb.box((0.06, 0.82, 0.22), (x, -0.55, 0.68), "paint")
        mb.box((0.94, 0.06, 0.22), (0, -0.97, 0.68), "paint")
        mb.box((0.36, 0.36, 0.32), (-0.18, -0.45, 0.75), "wood", bevel=0.03)
        mb.box((0.32, 0.32, 0.28), (0.2, -0.7, 0.73), "wood", bevel=0.03)
        mb.box((1.04, 0.12, 0.14), (0, 1.0, 0.3), "chrome", bevel=0.02)
        mb.box((1.04, 0.1, 0.14), (0, -1.0, 0.3), "darkgray", bevel=0.02)
        lights(mb, 1.0, -1.0, 0.5, 0.36)
    vehicle("pickup", pickup, lambda c: wheels(c, (-0.46, 0.46), (0.62, -0.62), 0.25, 0.18))

    # Box Truck -------------------------------------------------------------
    def box_truck(mb):
        mb.box((0.9, 2.6, 0.22), (0, -0.1, 0.36), "charcoal")
        mb.box((1.0, 0.82, 0.86), (0, 0.82, 0.78), "paint", bevel=0.08)
        mb.box((0.9, 0.12, 0.36), (0, 1.22, 0.92), "glass", bevel=0.02)
        mb.box((1.02, 0.36, 0.28), (0, 0.98, 0.94), "glass")
        mb.box((1.08, 1.72, 1.28), (0, -0.48, 1.12), "white", bevel=0.05)
        mb.box((1.1, 1.5, 0.22), (0, -0.48, 1.16), "paint")
        mb.box((1.04, 0.1, 0.16), (0, 1.26, 0.38), "darkgray", bevel=0.02)
        mb.box((0.06, 0.06, 1.1), (0, -1.35, 1.1), "lightgray")
        lights(mb, 1.25, -1.35, 0.52, 0.36)
    vehicle("box_truck", box_truck, lambda c: wheels(c, (-0.46, 0.46), (0.8, -0.55, -0.98), 0.25, 0.2))

    # Minibus ---------------------------------------------------------------
    def minibus(mb):
        mb.box((1.04, 2.6, 0.62), (0, 0, 0.56), "paint", bevel=0.08)
        mb.box((1.06, 2.3, 0.34), (0, -0.05, 1.0), "glass", bevel=0.03)
        mb.box((1.02, 2.5, 0.12), (0, -0.02, 1.22), "white", bevel=0.04)
        mb.box((0.9, 0.12, 0.42), (0, 1.28, 0.95), "glass", taper=(1, 0.6))
        for y in (-0.7, 0.0, 0.7):
            mb.box((1.08, 0.06, 0.34), (0, y, 1.0), "paint")
        mb.box((1.08, 0.1, 0.16), (0, 1.3, 0.3), "darkgray", bevel=0.02)
        mb.box((1.08, 0.1, 0.16), (0, -1.3, 0.3), "darkgray", bevel=0.02)
        mb.box((0.5, 0.4, 0.12), (0, -0.6, 1.32), "lightgray", bevel=0.03)
        lights(mb, 1.3, -1.3, 0.5, 0.38)
    vehicle("minibus", minibus, lambda c: wheels(c, (-0.48, 0.48), (0.82, -0.82), 0.23, 0.18))

    # Heavy Truck -----------------------------------------------------------
    def heavy(mb):
        mb.box((0.96, 3.6, 0.26), (0, -0.2, 0.42), "charcoal")
        mb.box((1.12, 1.0, 1.08), (0, 1.15, 1.0), "paint", bevel=0.09)
        mb.box((1.0, 0.12, 0.42), (0, 1.66, 1.18), "glass", bevel=0.02)
        mb.box((1.14, 0.42, 0.32), (0, 1.36, 1.2), "glass")
        mb.box((0.9, 0.08, 0.36), (0, 1.67, 0.68), "chrome")
        for x in (-0.5, 0.5):
            mb.cyl(0.06, 1.0, (x, 0.6, 1.25), "chrome", segs=6)
        mb.box((1.18, 2.5, 1.38), (0, -0.82, 1.24), "steel", bevel=0.05)
        mb.box((1.2, 2.3, 0.3), (0, -0.82, 1.3), "paint")
        mb.box((1.18, 0.1, 0.18), (0, 1.7, 0.42), "darkgray", bevel=0.02)
        lights(mb, 1.68, -2.08, 0.6, 0.42)
    vehicle("heavy_truck", heavy, lambda c: wheels(c, (-0.5, 0.5), (1.2, -0.4, -1.1, -1.65), 0.28, 0.22))

    # Armored Transport -----------------------------------------------------
    def armored(mb):
        mb.box((1.14, 2.3, 0.7), (0, 0, 0.66), "paint", bevel=0.1, taper=(0.92, 0.9))
        mb.box((1.02, 1.5, 0.42), (0, -0.2, 1.18), "paint", bevel=0.08, taper=(0.88, 0.9))
        mb.box((0.8, 0.08, 0.12), (0, 0.56, 1.22), "glass")
        for x in (-0.54, 0.54):
            mb.box((0.04, 0.5, 0.1), (x, 0.1, 1.2), "glass")
            mb.box((0.06, 2.0, 0.08), (x * 1.04, 0, 0.5), "darkgray")
        mb.cyl(0.24, 0.1, (0, -0.3, 1.44), "darkgray", segs=8)
        mb.box((1.18, 0.16, 0.22), (0, 1.16, 0.36), "charcoal", bevel=0.03)
        mb.box((0.6, 0.06, 0.14), (0, 1.16, 0.6), "charcoal")
        lights(mb, 1.16, -1.16, 0.7, 0.4, size=(0.14, 0.05, 0.08))
    vehicle("armored", armored, lambda c: wheels(c, (-0.52, 0.52), (0.78, 0.0, -0.78), 0.27, 0.22, rim="darkgray"))

    # High-Tech Transporter -------------------------------------------------
    def hitech(mb):
        mb.box((1.1, 2.4, 0.5), (0, 0, 0.5), "charcoal", bevel=0.1, taper=(0.9, 0.85))
        mb.box((0.92, 1.4, 0.38), (0, -0.25, 0.92), "glass", bevel=0.1, taper=(0.78, 0.7))
        mb.box((1.12, 2.2, 0.08), (0, 0, 0.42), "neoncyan")
        mb.box((0.9, 0.06, 0.06), (0, 1.2, 0.58), "neoncyan")
        mb.box((0.94, 0.4, 0.06), (0, -1.2, 0.78), "paint", bevel=0.02)
        for x in (-0.36, 0.36):
            mb.box((0.06, 0.06, 0.24), (x, -1.12, 0.66), "paint")
        mb.box((0.5, 0.9, 0.06), (0, 0.6, 0.76), "paint", bevel=0.02)
        mb.box((0.9, 0.04, 0.06), (0, -1.21, 0.5), "taillight")
    vehicle("hitech", hitech, lambda c: wheels(c, (-0.48, 0.48), (0.72, -0.72), 0.22, 0.18, rim="neoncyan"))

    # Luxury Transport ------------------------------------------------------
    def luxury(mb):
        mb.box((1.0, 2.7, 0.42), (0, 0, 0.42), "paint", bevel=0.08)
        mb.box((0.9, 1.7, 0.36), (0, -0.25, 0.8), "glass", bevel=0.05, taper=(0.94, 0.9))
        mb.box((0.88, 1.5, 0.06), (0, -0.3, 1.0), "paint", bevel=0.03)
        mb.box((1.02, 2.5, 0.05), (0, 0, 0.58), "gold")
        mb.box((0.6, 0.06, 0.2), (0, 1.36, 0.4), "chrome")
        mb.box((0.06, 0.06, 0.1), (0, 1.3, 0.68), "gold")
        lights(mb, 1.34, -1.34, 0.46, 0.36)
    vehicle("luxury", luxury, lambda c: wheels(c, (-0.45, 0.45), (0.85, -0.85), 0.21, 0.16, rim="gold"))

    # ambient traffic ---------------------------------------------------------
    def sedan(mb):
        mb.box((0.84, 1.5, 0.32), (0, 0, 0.34), "paint", bevel=0.07)
        mb.box((0.74, 0.8, 0.28), (0, -0.08, 0.63), "glass", taper=(0.9, 0.7), bevel=0.03)
        mb.box((0.7, 0.5, 0.05), (0, -0.1, 0.78), "paint", bevel=0.02)
        lights(mb, 0.74, -0.74, 0.4, 0.28, size=(0.14, 0.04, 0.07))
    vehicle("sedan", sedan, lambda c: wheels(c, (-0.38, 0.38), (0.46, -0.46), 0.17, 0.14))

    def taxi(mb):
        sedan(mb)
        mb.box((0.3, 0.14, 0.12), (0, -0.1, 0.86), "neonyellow", bevel=0.02)
    vehicle("taxi", taxi, lambda c: wheels(c, (-0.38, 0.38), (0.46, -0.46), 0.17, 0.14))


# --------------------------------------------------------------------------
# vehicle upgrade attachments (pivot on the mount point)
# --------------------------------------------------------------------------
def build_attachments():
    def roofrack(mb):
        mb.box((0.7, 0.8, 0.04), (0, 0, 0.04), "darkgray")
        for x in (-0.33, 0.33):
            mb.box((0.04, 0.8, 0.1), (x, 0, 0.07), "charcoal")
        mb.box((0.3, 0.3, 0.24), (-0.14, 0.12, 0.2), "wood", bevel=0.02)
        mb.box((0.26, 0.26, 0.2), (0.16, -0.16, 0.18), "khaki", bevel=0.02)
    single("att_roofrack", roofrack)

    def spoiler(mb):
        mb.box((0.8, 0.18, 0.04), (0, 0, 0.18), "charcoal", bevel=0.01)
        for x in (-0.28, 0.28):
            mb.box((0.04, 0.06, 0.18), (x, 0, 0.09), "charcoal")
    single("att_spoiler", spoiler)

    def antenna(mb):
        mb.cyl(0.02, 0.4, (0, 0, 0.2), "steel", segs=5)
        mb.cyl(0.12, 0.04, (0, 0, 0.42), "white", segs=8, r2=0.06, rot=(0.5, 0, 0))
        mb.ball(0.03, (0, 0, 0.42), "neonorange", subdiv=1)
    single("att_antenna", antenna)

    def bullbar(mb):
        mb.box((0.84, 0.06, 0.06), (0, 0, 0.3), "chrome")
        mb.box((0.84, 0.06, 0.06), (0, 0, 0.12), "chrome")
        for x in (-0.3, 0.3):
            mb.box((0.06, 0.06, 0.34), (x, 0, 0.18), "chrome")
    single("att_bullbar", bullbar)

    def lightbar(mb):
        mb.box((0.62, 0.12, 0.08), (0, 0, 0.06), "charcoal", bevel=0.02)
        for x in (-0.2, 0.0, 0.2):
            mb.box((0.14, 0.04, 0.05), (x, 0.06, 0.07), "lamp")
    single("att_lightbar", lightbar)


# --------------------------------------------------------------------------
# characters (~0.55 tall, chunky toy style)
# --------------------------------------------------------------------------
def character(name, shirt, pants, hat):
    """Rigid-limb character: Torso + Leg_L/R + Arm_L/R objects whose origins sit
    on the hip / shoulder joints, so Godot can swing them procedurally."""
    col = new_collection(name)
    torso = MB()
    torso.box((0.24, 0.16, 0.22), (0, 0, 0.29), shirt, bevel=0.04)
    torso.ball(0.11, (0, 0, 0.5), "peach", subdiv=1)
    torso.box((0.04, 0.02, 0.03), (-0.035, 0.1, 0.52), "black")
    torso.box((0.04, 0.02, 0.03), (0.035, 0.1, 0.52), "black")
    if hat == "cap":
        torso.cyl(0.11, 0.06, (0, 0, 0.58), "red", segs=8)
        torso.box((0.12, 0.1, 0.02), (0, 0.1, 0.56), "red")
    elif hat == "hardhat":
        torso.cyl(0.12, 0.08, (0, 0, 0.58), "yellow", segs=8, r2=0.08)
        torso.box((0.25, 0.17, 0.06), (0, 0, 0.36), "neonorange")
    elif hat == "shades":
        torso.box((0.16, 0.04, 0.04), (0, 0.09, 0.52), "black")
        torso.cyl(0.11, 0.04, (0, 0, 0.6), "darkbrown", segs=8)
    elif hat == "tophat":
        torso.cyl(0.13, 0.02, (0, 0, 0.59), "black", segs=8)
        torso.cyl(0.08, 0.12, (0, 0, 0.66), "black", segs=8)
        torso.box((0.05, 0.03, 0.05), (0.07, 0.08, 0.36), "gold")
    elif hat == "bun":
        torso.ball(0.07, (0, -0.06, 0.61), "darkbrown", subdiv=1)
        torso.box((0.18, 0.04, 0.06), (0, 0.06, 0.47), "lavender")
    torso.build("Torso", col)
    for side, x in (("L", -0.06), ("R", 0.06)):
        leg = MB()
        leg.box((0.08, 0.1, 0.18), (0, 0, -0.09), pants)
        leg.box((0.09, 0.13, 0.04), (0, 0.02, -0.17), "charcoal")
        leg.build("Leg_" + side, col, (x, 0, 0.18))
    for side, x in (("L", -0.15), ("R", 0.15)):
        arm = MB()
        arm.box((0.06, 0.08, 0.18), (0, 0, -0.08), shirt, bevel=0.02)
        arm.ball(0.035, (0, 0, -0.18), "peach", subdiv=1)
        arm.build("Arm_" + side, col, (x, 0, 0.38))
    export(name, col)


def build_characters():
    character("driver", "blue", "navy", "cap")
    character("worker", "orange", "darkgray", "hardhat")
    character("passenger", "khaki", "darkbrown", "shades")
    character("vip", "black", "black", "tophat")
    character("mentor", "purple", "navy", "bun")


# --------------------------------------------------------------------------
# HQ garage progression (front faces +Y, doors toward the parking lot)
# --------------------------------------------------------------------------
def build_garages():
    def g1(mb):  # tiny garage
        mb.box((3.0, 2.6, 0.12), (0, 0, 0.06), "concrete")
        mb.box((2.2, 2.0, 1.4), (-0.3, -0.1, 0.76), "lightsand", bevel=0.06)
        mb.box((2.4, 2.2, 0.12), (-0.3, -0.1, 1.5), "roofred", bevel=0.04)
        mb.box((1.3, 0.06, 1.0), (-0.3, 0.92, 0.62), "steel")
        for z in (0.35, 0.6, 0.85, 1.1):
            mb.box((1.3, 0.08, 0.03), (-0.3, 0.93, z), "gray")
        mb.box((0.8, 1.0, 0.9), (1.25, 0.3, 0.55), "white", bevel=0.05)
        mb.box((0.4, 0.06, 0.3), (1.25, 0.82, 0.65), "glass")
        mb.box((0.9, 1.1, 0.08), (1.25, 0.3, 1.04), "darkgray")
        mb.box((1.0, 0.08, 0.36), (-0.3, 1.0, 1.72), "paint", bevel=0.02)
        mb.box((0.36, 0.36, 0.36), (1.15, 1.05, 0.3), "wood", bevel=0.03)
        mb.cyl(0.18, 0.4, (-1.55, 0.9, 0.32), "red", segs=8)
    single("garage_l1", g1)

    def g2(mb):  # local depot
        mb.box((5.0, 3.6, 0.12), (0, 0, 0.06), "concrete")
        mb.box((3.6, 2.6, 1.8), (-0.5, -0.4, 0.96), "lightsand", bevel=0.06)
        mb.gable(3.6, 2.6, 0.7, (-0.5, -0.4, 1.86), "roofred", rot=(0, 0, math.pi / 2))
        for x in (-1.35, 0.35):
            mb.box((1.3, 0.06, 1.2), (x, 0.92, 0.72), "steel")
            for z in (0.35, 0.65, 0.95, 1.25):
                mb.box((1.3, 0.08, 0.03), (x, 0.93, z), "gray")
        mb.box((1.2, 1.6, 1.4), (1.9, 0.2, 0.82), "white", bevel=0.06)
        mb.box((1.3, 1.7, 0.1), (1.9, 0.2, 1.56), "darkgray")
        mb.box((0.6, 0.06, 0.4), (1.9, 1.01, 0.9), "glass")
        mb.box((1.6, 0.1, 0.42), (-0.5, 0.95, 2.05), "paint", bevel=0.03)
        for x in (-2.4, -1.6, -0.8, 0.0, 0.8, 1.6, 2.4):
            mb.box((0.06, 0.06, 0.6), (x, -1.75, 0.4), "gray")
        mb.box((4.9, 0.04, 0.06), (0, -1.75, 0.6), "gray")
        mb.box((0.4, 0.4, 0.4), (2.2, 1.4, 0.32), "wood", bevel=0.03)
        mb.box((0.4, 0.4, 0.4), (2.3, 1.0, 0.32), "khaki", bevel=0.03)
    single("garage_l2", g2)

    def g3(mb):  # regional warehouse
        mb.box((7.0, 5.0, 0.12), (0, 0, 0.06), "concrete")
        mb.box((5.6, 3.6, 2.4), (-0.4, -0.5, 1.26), "steel", bevel=0.06)
        mb.box((5.8, 3.8, 0.14), (-0.4, -0.5, 2.5), "darkgray", bevel=0.04)
        for x in (-2.3, -0.4, 1.5):
            mb.box((1.4, 0.06, 1.5), (x, 1.32, 0.9), "lightgray")
            for z in (0.4, 0.75, 1.1, 1.45):
                mb.box((1.4, 0.08, 0.03), (x, 1.33, z), "gray")
            mb.box((1.6, 0.4, 0.06), (x, 1.5, 1.75), "paint")
        mb.box((5.8, 0.6, 0.3), (-0.4, 1.6, 0.27), "concrete")
        mb.box((1.4, 2.0, 1.8), (3.0, 0.4, 1.02), "white", bevel=0.06)
        for z in (0.8, 1.4):
            mb.box((1.42, 1.6, 0.26), (3.0, 0.4, z), "glass")
        mb.box((1.5, 2.1, 0.12), (3.0, 0.4, 1.98), "darkgray")
        for x in (-2.0, -0.4, 1.2):
            mb.box((0.6, 0.6, 0.36), (x, -1.0, 2.75), "lightgray", bevel=0.04)
        mb.box((2.6, 0.12, 0.6), (-0.4, 1.32, 2.15), "paint", bevel=0.03)
        mb.box((2.0, 0.14, 0.2), (-0.4, 1.36, 2.15), "white")
        for x in (-3.1, -2.7):
            mb.box((0.36, 0.36, 0.36), (x, 1.9, 0.3), "wood", bevel=0.03)
    single("garage_l3", g3)

    def g4(mb):  # logistics center
        mb.box((9.0, 6.4, 0.12), (0, 0, 0.06), "concrete")
        mb.box((6.4, 4.2, 2.8), (-1.0, -0.6, 1.46), "steel", bevel=0.06)
        mb.box((6.6, 4.4, 0.16), (-1.0, -0.6, 2.9), "darkgray", bevel=0.04)
        for x in (-3.3, -1.75, -0.2, 1.35):
            mb.box((1.2, 0.06, 1.6), (x, 1.52, 0.95), "lightgray")
            for z in (0.45, 0.85, 1.25, 1.6):
                mb.box((1.2, 0.08, 0.03), (x, 1.53, z), "gray")
        mb.box((6.6, 0.7, 0.3), (-1.0, 1.85, 0.27), "concrete")
        mb.box((2.2, 2.2, 4.4), (3.4, 0.6, 2.26), "white", bevel=0.08)
        for z in (1.0, 1.8, 2.6, 3.4):
            mb.box((2.22, 2.22, 0.34), (3.4, 0.6, z), "glass")
        mb.box((2.4, 2.4, 0.16), (3.4, 0.6, 4.5), "darkgray")
        mb.box((2.0, 0.14, 0.7), (3.4, 1.75, 4.9), "paint", bevel=0.03)
        mb.cyl(0.9, 1.1, (-3.4, -2.2, 4.1), "lightgray", segs=10)
        mb.cone(0.95, 0.5, (-3.4, -2.2, 4.9), "steel", segs=10)
        for x, y in ((-4.0, -2.8), (-2.8, -2.8), (-4.0, -1.6), (-2.8, -1.6)):
            mb.cyl(0.06, 3.5, (x, y, 1.8), "steel", segs=5)
        mb.box((3.6, 0.14, 0.8), (-1.0, 1.52, 2.4), "paint", bevel=0.03)
        for x in (-3.0, -1.0, 1.0):
            mb.box((0.7, 0.7, 0.4), (x, -1.2, 3.18), "lightgray", bevel=0.04)
    single("garage_l4", g4)

    def g5(mb):  # underground headquarters
        mb.box((11.0, 7.6, 0.14), (0, 0, 0.07), "charcoal")
        mb.box((7.0, 4.6, 2.6), (-1.6, -0.9, 1.4), "darkgray", bevel=0.08)
        mb.box((7.2, 4.8, 0.16), (-1.6, -0.9, 2.76), "charcoal")
        mb.box((7.04, 0.1, 0.1), (-1.6, 1.42, 2.5), "neoncyan")
        mb.box((7.04, 0.1, 0.1), (-1.6, 1.42, 0.25), "neonpink")
        for x in (-4.2, -2.6, -1.0, 0.6):
            mb.box((1.3, 0.06, 1.7), (x, 1.42, 1.05), "steel")
            mb.box((1.3, 0.08, 0.06), (x, 1.44, 1.92), "neoncyan")
        # ramp to the underground level
        mb.box((2.2, 2.4, 0.2), (-1.6, 3.0, 0.02), "asphalt", rot=(-0.12, 0, 0))
        mb.box((2.6, 0.3, 0.6), (-1.6, 2.0, 0.3), "charcoal")
        mb.box((2.0, 0.1, 0.5), (-1.6, 1.86, 0.3), "black")
        for x in (-2.75, -0.45):
            mb.box((0.2, 2.4, 0.4), (x, 3.0, 0.2), "concrete")
        # tower
        mb.box((2.6, 2.6, 6.4), (3.6, -0.4, 3.26), "charcoal", bevel=0.1)
        for z in (1.2, 2.2, 3.2, 4.2, 5.2):
            mb.box((2.62, 2.62, 0.4), (3.6, -0.4, z), "glass")
        for x in (2.28, 4.92):
            for y in (-1.72, 0.92):
                mb.box((0.14, 0.14, 6.2), (x, y, 3.2), "neonpurple")
        mb.box((2.8, 2.8, 0.2), (3.6, -0.4, 6.56), "darkgray")
        mb.cyl(1.0, 0.08, (3.6, -0.4, 6.7), "gold", segs=12)
        mb.box((0.9, 0.12, 0.12), (3.6, -0.4, 6.78), "white")
        mb.box((0.12, 0.6, 0.12), (3.6, -0.4, 6.78), "white")
        mb.cyl(0.04, 1.6, (2.6, -1.4, 7.5), "steel", segs=5)
        mb.ball(0.08, (2.6, -1.4, 8.3), "taillight")
        # emblem sign
        mb.box((3.0, 0.2, 1.0), (-1.6, 1.4, 3.4), "black", bevel=0.04)
        mb.box((2.6, 0.24, 0.16), (-1.6, 1.4, 3.6), "gold")
        mb.box((2.0, 0.24, 0.14), (-1.6, 1.4, 3.3), "neonyellow")
        mb.box((1.0, 1.0, 0.5), (-4.0, -2.0, 3.1), "steel", bevel=0.05)
        mb.box((1.0, 1.0, 0.5), (-2.6, -2.0, 3.1), "steel", bevel=0.05)
    single("garage_l5", g5)


# --------------------------------------------------------------------------
# buildings
# --------------------------------------------------------------------------
def windows_row(mb, w, y, z, count, color="glass", size=(0.3, 0.05, 0.3)):
    step = w / count
    for i in range(count):
        x = -w / 2 + step * (i + 0.5)
        mb.box(size, (x, y, z), color)


def build_buildings():
    def warehouse(mb):  # regional depot
        mb.box((4.0, 3.0, 0.1), (0, 0, 0.05), "concrete")
        mb.box((3.2, 2.2, 1.6), (0, -0.3, 0.86), "steel", bevel=0.05)
        mb.gable(2.2, 3.2, 0.5, (0, -0.3, 1.66), "darkgray", rot=(0, 0, math.pi / 2))
        for x in (-0.8, 0.8):
            mb.box((1.1, 0.06, 1.1), (x, 0.82, 0.66), "lightgray")
            for z in (0.35, 0.65, 0.95):
                mb.box((1.1, 0.08, 0.03), (x, 0.83, z), "gray")
        mb.box((1.6, 0.1, 0.4), (0, 0.86, 1.45), "paint", bevel=0.02)
        mb.box((0.34, 0.34, 0.34), (1.6, 1.1, 0.27), "wood", bevel=0.03)
        # Loading bays have projecting headers, bumpers and side-wall clerestory glazing.
        for x in (-0.8, 0.8):
            mb.box((1.22, 0.2, 0.1), (x, 0.86, 1.27), "darkgray")
            for xx in (x - 0.54, x + 0.54):
                mb.box((0.08, 0.12, 0.35), (xx, 0.88, 0.3), "charcoal")
        for x in (-1.62, 1.62):
            for y in (-0.95, -0.25, 0.45):
                mb.box((0.04, 0.48, 0.28), (x, y, 1.27), "glass")
                mb.box((0.055, 0.035, 0.3), (x, y, 1.27), "steel")
        for y in (-1.28, 0.68):
            mb.box((3.15, 0.06, 0.07), (0, y, 1.69), "lightgray")
        for z in (0.35, 0.75, 1.12):
            mb.box((3.05, 0.035, 0.04), (0, -1.415, z), "lightgray")
        mb.box((0.48, 0.045, 0.85), (-0.9, -1.43, 0.55), "charcoal")
    single("warehouse", warehouse)

    def office(mb):
        mb.box((2.0, 1.8, 2.2), (0, 0, 1.1), "white", bevel=0.06)
        for z in (0.7, 1.5):
            windows_row(mb, 1.6, 0.91, z, 3)
        mb.box((2.1, 1.9, 0.12), (0, 0, 2.24), "darkgray")
        mb.box((0.5, 0.06, 0.7), (0, 0.91, 0.35), "darkwood")
        # The free camera sees every facade: frames, sill bands and a grounded plinth.
        mb.box((2.08,1.88,.12),(0,0,.06),"concrete",bevel=.025)
        for z in (.7,1.5):
            for y in (-.48,.36):
                for side in (-1,1):
                    mb.box((.035,.5,.5),(side*1.005,y,z),"darkgray")
                    mb.box((.045,.4,.38),(side*1.014,y,z+.02),"glass")
            windows_row(mb,1.6,-.91,z,3,size=(.34,.05,.4))
            mb.box((1.95,1.85,.055),(0,0,z-.28),"lightgray")
        mb.box((.68,.22,.08),(0,.94,.77),"darkgray",bevel=.02)
        if QUALITY_LOD == 0:
            for side in (-1,1):
                for z in (.7,1.5):
                    mb.box((.055,1.4,.025),(side*1.018,0,z),"white")
    single("office", office)

    # border posts: one booth style per region family, roof/trim use the PAINT
    # cell so each region tints its own checkpoint colour
    def booth_base(mb):
        for i in range(4):
            mb.box((0.3, 0.3, 0.6), (-1.1, -0.8 + i * 0.5, 0.3), "concrete")
        mb.box((0.06, 0.06, 1.6), (-0.6, 0.6, 0.8), "steel")
        mb.box((0.5, 0.04, 0.3), (-0.6, 0.6, 1.5), "paint")

    def checkpoint(mb):  # classic
        mb.box((1.0, 1.0, 1.2), (0, 0, 0.6), "white", bevel=0.05)
        mb.box((1.02, 0.7, 0.4), (0, 0.05, 0.85), "glass")
        mb.box((1.3, 1.3, 0.12), (0, 0, 1.28), "paint", bevel=0.03)
        mb.box((0.9, 0.1, 0.14), (0, 0.6, 1.1), "paint")
        booth_base(mb)
    single("checkpoint", checkpoint)

    def checkpoint_adobe(mb):
        mb.box((1.1, 1.0, 1.15), (0, 0, 0.58), "lightsand", bevel=0.1)
        mb.box((0.5, 0.06, 0.4), (0, 0.5, 0.8), "glass")
        mb.box((1.2, 1.1, 0.1), (0, 0, 1.2), "desertrock")
        for x in (-0.4, 0.0, 0.4):
            mb.cyl(0.05, 0.36, (x, 0.6, 1.08), "wood", axis="Y", segs=5)
        mb.box((1.4, 0.7, 0.05), (0, 0.75, 1.35), "paint", rot=(-0.25, 0, 0))
        booth_base(mb)
        mb.cyl(0.18, 0.5, (0.75, -0.4, 0.25), "green", segs=7)
    single("checkpoint_adobe", checkpoint_adobe)

    def checkpoint_cabin(mb):
        mb.box((1.0, 1.0, 1.0), (0, 0, 0.5), "wood", bevel=0.04)
        for z in (0.25, 0.55, 0.85):
            mb.box((1.04, 1.04, 0.04), (0, 0, z), "darkwood")
        mb.box((0.5, 0.06, 0.32), (0, 0.51, 0.7), "lamp")
        mb.gable(1.0, 1.0, 0.6, (0, 0, 1.0), "white", rot=(0, 0, math.pi / 2), overhang=0.14)
        mb.box((1.1, 0.08, 0.12), (0, 0.6, 0.95), "paint")
        booth_base(mb)
    single("checkpoint_cabin", checkpoint_cabin)

    def checkpoint_port(mb):
        mb.box((1.0, 1.6, 1.0), (0, 0, 0.5), "paint", bevel=0.02)
        for y in (-0.6, -0.2, 0.2, 0.6):
            mb.box((1.04, 0.05, 0.9), (0, y, 0.5), "paint")
        mb.box((0.7, 0.06, 0.35), (0, 0.81, 0.65), "glass")
        mb.box((1.1, 1.7, 0.08), (0, 0, 1.04), "white")
        mb.cyl(0.04, 0.8, (0.35, -0.5, 1.45), "steel", segs=5)
        mb.ball(0.1, (0.35, -0.5, 1.88), "neonorange")
        booth_base(mb)
    single("checkpoint_port", checkpoint_port)

    def checkpoint_industrial(mb):
        mb.box((1.1, 1.1, 1.3), (0, 0, 0.65), "steel", bevel=0.04)
        mb.box((1.12, 0.8, 0.35), (0, 0.05, 0.9), "glass")
        for i in range(5):
            mb.box((0.24, 1.14, 0.14), (-0.44 + i * 0.22, 0, 0.12), "paint" if i % 2 == 0 else "charcoal")
        mb.box((1.3, 1.3, 0.14), (0, 0, 1.36), "darkgray")
        mb.box((0.4, 0.4, 0.3), (0.2, -0.2, 1.58), "lightgray", bevel=0.03)
        booth_base(mb)
    single("checkpoint_industrial", checkpoint_industrial)

    def checkpoint_neon(mb):
        mb.box((1.0, 1.0, 1.3), (0, 0, 0.65), "charcoal", bevel=0.05)
        mb.box((1.02, 0.7, 0.4), (0, 0.05, 0.9), "glass")
        mb.box((1.04, 1.04, 0.06), (0, 0, 0.2), "neoncyan")
        mb.box((1.04, 1.04, 0.06), (0, 0, 1.25), "neonpink")
        mb.box((1.3, 1.3, 0.12), (0, 0, 1.36), "paint", bevel=0.03)
        mb.box((0.8, 0.1, 0.3), (0, 0.6, 1.62), "neonyellow")
        booth_base(mb)
    single("checkpoint_neon", checkpoint_neon)

    # gantry spanning the road at the border, sign board in PAINT colour
    def border_gantry(mb):
        for x in (-2.3, 2.3):
            mb.box((0.22, 0.22, 2.9), (x, 0, 1.45), "steel")
            mb.box((0.5, 0.5, 0.12), (x, 0, 0.06), "concrete")
        mb.box((4.8, 0.2, 0.22), (0, 0, 2.85), "darkgray")
        mb.box((3.0, 0.14, 0.7), (0, 0.06, 3.2), "paint", bevel=0.04)
        mb.box((2.6, 0.16, 0.16), (0, 0.07, 3.28), "white")
        mb.box((1.6, 0.16, 0.1), (0, 0.07, 3.06), "white")
        for x in (-1.2, 1.2):
            mb.box((0.18, 0.12, 0.1), (x, 0.08, 2.7), "lamp")

    single("border_gantry", border_gantry)

    def border_flag(mb):
        mb.cyl(0.04, 2.6, (0, 0, 1.3), "chrome", segs=6)
        mb.ball(0.07, (0, 0, 2.62), "gold")
        mb.box((0.04, 0.9, 0.55), (0, 0.47, 2.25), "paint")
        mb.box((0.05, 0.9, 0.12), (0, 0.47, 2.25), "white")
        mb.box((0.4, 0.4, 0.12), (0, 0, 0.06), "concrete")

    single("border_flag", border_flag)

    def checkpoint_arm(mb):  # pivot at origin, arm extends along +X
        mb.box((0.18, 0.18, 0.5), (0, 0, 0.25), "darkgray")
        for i in range(6):
            col = "red" if i % 2 == 0 else "white"
            mb.box((0.36, 0.08, 0.08), (0.18 + i * 0.36, 0, 0.5), col)
    single("checkpoint_arm", checkpoint_arm)

    def house_adobe(mb):
        mb.box((1.8, 1.6, 1.2), (0, 0, 0.6), "paint", bevel=0.08)
        mb.box((1.9, 1.7, 0.1), (0, 0, 1.22), "desertrock", bevel=0.03)
        for x in (-0.6, 0.0, 0.6):
            mb.cyl(0.05, 0.4, (x, 0.85, 1.05), "wood", axis="Y", segs=5)
        mb.box((0.4, 0.06, 0.7), (0.3, 0.81, 0.35), "darkwood")
        mb.box((0.3, 0.06, 0.3), (-0.45, 0.81, 0.7), "charcoal")
        mb.box((0.8, 0.8, 0.6), (-0.4, -0.4, 1.5), "paint", bevel=0.06)
    single("house_adobe", house_adobe)

    def house_cottage(mb):
        mb.box((1.6, 1.4, 1.0), (0, 0, 0.5), "paint", bevel=0.05)
        mb.gable(1.4, 1.6, 0.75, (0, 0, 1.0), "roofred", rot=(0, 0, math.pi / 2))
        mb.box((0.36, 0.06, 0.6), (0, 0.71, 0.3), "darkwood")
        for x in (-0.5, 0.5):
            mb.box((0.28, 0.06, 0.28), (x, 0.71, 0.6), "glass")
        mb.box((0.22, 0.22, 0.6), (0.45, -0.3, 1.5), "brick" if False else "terracotta")
        # Sills, shutters and a shallow porch give the cottage a legible entrance.
        for x in (-0.5, 0.5):
            mb.box((0.37, 0.13, 0.045), (x, 0.75, 0.445), "white")
            for sx in (-1, 1):
                mb.box((0.09, 0.06, 0.3), (x + sx * 0.2, 0.735, 0.6), "darkwood")
            mb.box((0.03, 0.075, 0.28), (x, 0.735, 0.6), "white")
        mb.box((0.6, 0.38, 0.08), (0, 0.84, 0.04), "concrete")
        mb.box((0.68, 0.35, 0.06), (0, 0.83, 0.77), "roofred", rot=(-0.2, 0, 0))
        mb.box((0.3, 0.3, 0.07), (0.45, -0.3, 1.82), "darkgray")
        mb.box((0.5, 0.04, 0.4), (0, -0.715, 0.6), "white")
        mb.box((0.4, 0.055, 0.3), (0, -0.73, 0.6), "glass")
        mb.box((0.035, 0.07, 0.3), (0, -0.74, 0.6), "white")
        for x in (-0.815, 0.815):
            mb.box((0.04, 0.46, 0.4), (x, 0, 0.6), "white")
            mb.box((0.055, 0.36, 0.3), (x, 0, 0.6), "glass")
    single("house_cottage", house_cottage)

    def house_cabin(mb):
        mb.box((1.6, 1.4, 0.9), (0, 0, 0.45), "wood", bevel=0.04)
        for z in (0.2, 0.45, 0.7):
            mb.box((1.64, 1.44, 0.04), (0, 0, z), "darkwood")
        mb.gable(1.6, 1.4, 0.8, (0, 0, 0.9), "white", rot=(0, 0, math.pi / 2), overhang=0.14)
        mb.box((0.36, 0.06, 0.56), (0, 0.71, 0.28), "darkbrown")
        mb.box((0.3, 0.06, 0.26), (-0.5, 0.71, 0.55), "lamp")
        mb.box((0.22, 0.22, 0.7), (-0.45, -0.3, 1.4), "gray")
    single("house_cabin", house_cabin)

    def house_wood(mb):
        mb.box((1.7, 1.4, 1.1), (0, -0.1, 0.55), "paint", bevel=0.04)
        mb.box((1.9, 1.7, 0.1), (0, 0.0, 1.14), "darkwood", rot=(0.12, 0, 0))
        mb.box((1.7, 0.5, 0.08), (0, 0.85, 0.08), "wood")
        for x in (-0.75, 0.75):
            mb.box((0.08, 0.08, 0.9), (x, 1.05, 0.5), "wood")
        mb.box((1.8, 0.6, 0.06), (0, 0.85, 0.96), "wood")
        mb.box((0.38, 0.06, 0.66), (0, 0.61, 0.38), "darkwood")
    single("house_wood", house_wood)

    def house_villa(mb):
        mb.box((2.2, 1.6, 0.9), (0, 0, 0.45), "white", bevel=0.05)
        mb.box((1.4, 1.2, 0.8), (-0.3, -0.1, 1.3), "white", bevel=0.05)
        mb.box((2.3, 1.7, 0.08), (0, 0, 0.92), "paint")
        mb.box((1.5, 1.3, 0.08), (-0.3, -0.1, 1.72), "paint")
        windows_row(mb, 1.8, 0.81, 0.45, 3, size=(0.36, 0.05, 0.4))
        mb.box((1.0, 0.06, 0.4), (-0.3, 0.51, 1.3), "glass")
        mb.box((1.2, 0.9, 0.08), (0.6, 1.4, 0.05), "cyan")
        mb.box((1.3, 1.0, 0.06), (0.6, 1.4, 0.02), "white")
    single("house_villa", house_villa)

    def house_modern(mb):
        mb.box((1.6, 1.6, 2.0), (0, 0, 1.0), "charcoal", bevel=0.05)
        mb.box((1.62, 1.62, 0.08), (0, 0, 0.7), "paint")
        mb.box((1.62, 1.62, 0.08), (0, 0, 1.4), "paint")
        windows_row(mb, 1.2, 0.81, 1.05, 2, color="neonyellow", size=(0.4, 0.05, 0.3))
        windows_row(mb, 1.2, 0.81, 1.75, 2, color="glass", size=(0.4, 0.05, 0.3))
    single("house_modern", house_modern)

    def apartment(mb):
        mb.box((2.0, 2.0, 3.2), (0, 0, 1.6), "paint", bevel=0.06)
        for z in (0.7, 1.5, 2.3):
            windows_row(mb, 1.6, 1.01, z, 3)
        mb.box((2.1, 2.1, 0.12), (0, 0, 3.24), "darkgray")
        mb.box((0.6, 0.6, 0.4), (0.4, -0.4, 3.5), "lightgray")
        mb.box((0.5, 0.06, 0.6), (0, 1.01, 0.3), "darkwood")
        mb.box((2.08,2.08,.12),(0,0,.06),"concrete",bevel=.025)
        for z in (.7,1.5,2.3):
            for side in (-1,1):
                for y in (-.5,.5):
                    mb.box((.04,.5,.5),(side*1.01,y,z),"white")
                    mb.box((.05,.37,.37),(side*1.02,y,z+.015),"glass")
            for x in (-.6,0,.6):
                mb.box((.44,.04,.49),(x,-1.01,z),"white")
                mb.box((.33,.05,.36),(x,-1.02,z+.015),"glass")
            mb.box((2.06,2.06,.06),(0,0,z-.3),"lightgray")
        if QUALITY_LOD == 0:
            for x in (-.6,0,.6):
                for z in (.7,1.5,2.3):
                    mb.box((.025,.065,.37),(x,-1.025,z),"darkgray")
    single("apartment", apartment)

    def shop(mb):
        mb.box((1.8, 1.4, 1.2), (0, 0, 0.6), "paint", bevel=0.05)
        mb.box((1.9, 1.5, 0.1), (0, 0, 1.24), "darkgray")
        mb.box((1.4, 0.06, 0.6), (0, 0.71, 0.45), "glass")
        for i in range(5):
            col = "red" if i % 2 == 0 else "white"
            mb.box((0.38, 0.5, 0.05), (-0.76 + i * 0.38, 0.9, 0.98), col, rot=(-0.35, 0, 0))
        mb.box((1.0, 0.1, 0.24), (0, 0.72, 1.36), "yellow")
        mb.box((1.88,1.48,.12),(0,0,.06),"concrete",bevel=.02)
        for side in (-1,1):
            mb.box((.035,.78,.55),(side*.91,-.03,.62),"white")
            mb.box((.045,.65,.42),(side*.92,-.03,.64),"glass")
            mb.box((.12,.9,.06),(side*.9,-.03,.32),"darkgray")
        mb.box((.52,.045,.8),(.35,-.71,.46),"darkwood")
        mb.box((.07,.06,.08),(.52,-.74,.5),"steel")
        mb.box((1.82,.12,.14),(0,-.73,1.1),"white")
        if QUALITY_LOD == 0:
            for side in (-1,1):
                mb.box((.06,.025,.42),(side*.927,-.03,.64),"white")
    single("shop", shop)

    def barn(mb):
        mb.box((2.0, 2.6, 1.4), (0, 0, 0.7), "roofred", bevel=0.04)
        mb.gable(2.0, 2.6, 0.9, (0, 0, 1.4), "darkred")
        mb.box((0.9, 0.06, 1.0), (0, 1.31, 0.5), "white")
        mb.box((0.8, 0.07, 0.9), (0, 1.32, 0.48), "darkred")
        mb.cyl(0.5, 2.4, (1.5, -0.6, 1.2), "lightgray", segs=10)
        mb.ball(0.5, (1.5, -0.6, 2.4), "steel", subdiv=1)
        # Timber braces and siding break up the empty barn shell; silo bands keep its scale readable.
        for a in (-0.65, 0.65):
            mb.box((0.06, 0.035, 1.08), (0, 1.365, 0.5), "white", rot=(0, a, 0))
        mb.box((0.04, 0.04, 0.9), (0, 1.365, 0.48), "white")
        for x in (-1.015, 1.015):
            for y in (-1.15, -0.65, -0.15, 0.35, 0.85, 1.15):
                mb.box((0.025, 0.035, 1.24), (x, y, 0.7), "darkred")
        mb.box((0.8, 0.04, 0.35), (0, 1.315, 1.66), "white")
        mb.box((0.66, 0.06, 0.23), (0, 1.33, 1.66), "darkwood")
        for z in (0.45, 1.3, 2.15):
            mb.cyl(0.512, 0.05, (1.5, -0.6, z), "steel", segs=10)
        mb.box((0.65, 0.04, 0.3), (0, -1.315, 1.68), "white")
        mb.box((0.52, 0.06, 0.18), (0, -1.33, 1.68), "darkwood")
        for a in (-0.75, 0.75):
            mb.box((0.045, 0.03, 1.65), (0, -1.32, 0.7), "white", rot=(0, a, 0))
    single("barn", barn)

    def factory(mb):
        mb.box((3.2, 2.4, 1.6), (0, 0, 0.8), "steel", bevel=0.05)
        for i in range(3):
            mb.box((1.06, 2.4, 0.6), (-1.06 + i * 1.06, 0, 1.75), "darkgray", taper=(0.2, 1.0))
            mb.box((0.06, 2.3, 0.4), (-1.06 + i * 1.06 + 0.4, 0, 1.75), "glass")
        mb.cyl(0.3, 3.4, (1.1, -0.7, 1.7), "terracotta", segs=8)
        mb.cyl(0.32, 0.25, (1.1, -0.7, 3.0), "white", segs=8)
        mb.cyl(0.32, 0.25, (1.1, -0.7, 2.4), "white", segs=8)
        mb.box((1.0, 0.06, 0.9), (-0.6, 1.21, 0.45), "lightgray")
    single("factory", factory)

    def office_tower(mb):
        mb.box((2.0, 2.0, 5.0), (0, 0, 2.5), "glass", bevel=0.05)
        for z in (0.8, 1.6, 2.4, 3.2, 4.0, 4.8):
            mb.box((2.04, 2.04, 0.14), (0, 0, z), "white")
        for x in (-1.0, 1.0):
            mb.box((0.12, 2.06, 5.0), (x, 0, 2.5), "white")
        mb.box((1.2, 1.2, 0.5), (0, 0, 5.25), "lightgray")
    single("office_tower", office_tower)

    def neon_tower(mb):
        mb.box((2.0, 2.0, 6.0), (0, 0, 3.0), "charcoal", bevel=0.06)
        for z in (1.0, 2.0, 3.0, 4.0, 5.0):
            mb.box((2.02, 2.02, 0.3), (0, 0, z), "darkpurple")
        mb.box((0.12, 2.06, 5.8), (1.0, 0, 3.0), "neonpink")
        mb.box((2.06, 0.12, 5.8), (0, 1.0, 3.0), "neoncyan")
        mb.box((1.6, 0.2, 0.8), (0, 1.1, 6.2), "black", bevel=0.03)
        mb.box((1.4, 0.24, 0.3), (0, 1.12, 6.2), "neonyellow")
        mb.cyl(0.04, 1.4, (0.5, -0.5, 6.7), "steel", segs=5)
        mb.ball(0.08, (0.5, -0.5, 7.4), "taillight")
    single("neon_tower", neon_tower)

    def neon_block(mb):
        mb.box((2.2, 1.8, 2.6), (0, 0, 1.3), "darkpurple", bevel=0.05)
        windows_row(mb, 1.8, 0.91, 0.8, 3, color="neonyellow", size=(0.36, 0.05, 0.3))
        windows_row(mb, 1.8, 0.91, 1.6, 3, color="glass", size=(0.36, 0.05, 0.3))
        mb.box((1.6, 0.14, 0.4), (0, 0.95, 2.3), "neonpink")
        mb.box((2.3, 1.9, 0.1), (0, 0, 2.64), "charcoal")
    single("neon_block", neon_block)

    def crane(mb):
        for x in (-0.8, 0.8):
            for y in (-0.6, 0.6):
                mb.box((0.18, 0.18, 3.0), (x, y, 1.5), "orange")
        mb.box((1.9, 1.5, 0.3), (0, 0, 3.1), "orange", bevel=0.03)
        mb.box((0.5, 5.0, 0.24), (0, 1.0, 3.45), "yellow")
        mb.box((0.8, 0.8, 0.6), (0, -0.2, 3.6), "white", bevel=0.04)
        mb.box((0.04, 0.04, 1.6), (0, 3.2, 2.6), "charcoal")
        mb.box((0.5, 0.3, 0.2), (0, 3.2, 1.8), "yellow")
    single("crane", crane)

    def lighthouse(mb):
        mb.cyl(0.9, 0.4, (0, 0, 0.2), "gray", segs=10)
        for i in range(4):
            col = "red" if i % 2 == 0 else "white"
            r0 = 0.6 - i * 0.07
            mb.cyl(r0, 0.8, (0, 0, 0.8 + i * 0.8), col, segs=10, r2=r0 - 0.07)
        mb.cyl(0.42, 0.1, (0, 0, 3.6), "darkgray", segs=10)
        mb.cyl(0.3, 0.5, (0, 0, 3.9), "lamp", segs=8)
        mb.cone(0.38, 0.4, (0, 0, 4.35), "red", segs=8)
    single("lighthouse", lighthouse)

    def container(mb):
        mb.box((1.1, 2.4, 1.0), (0, 0, 0.5), "paint", bevel=0.02)
        for y in (-0.9, -0.45, 0.0, 0.45, 0.9):
            mb.box((1.14, 0.05, 0.9), (0, y, 0.5), "paint")
        mb.box((1.0, 0.04, 0.9), (0, 1.21, 0.5), "darkgray")
    single("container", container)

    def windmill(mb):
        mb.cyl(0.5, 2.6, (0, 0, 1.3), "white", segs=8, r2=0.32)
        mb.cone(0.42, 0.6, (0, 0, 2.9), "roofred", segs=8)
        for i in range(4):
            a = i * math.pi / 2 + 0.3
            mb.box((0.2, 0.04, 1.3), (math.cos(a) * 0.65, 0.45, 2.3 + math.sin(a) * 0.65), "wood",
                   rot=(0, -a + math.pi / 2, 0))
    single("windmill", windmill)

    def boat(mb):
        mb.box((0.9, 2.0, 0.4), (0, 0, 0.1), "paint", taper=(1.1, 1.0), bevel=0.06)
        mb.box((0.6, 0.7, 0.4), (0, -0.3, 0.5), "white", bevel=0.04)
        mb.cyl(0.03, 1.2, (0, 0.3, 0.8), "darkwood", segs=5)
    single("boat", boat)

    def yacht(mb):
        mb.box((1.2, 3.0, 0.5), (0, 0, 0.1), "white", taper=(1.1, 0.9), bevel=0.1)
        mb.box((0.9, 1.6, 0.4), (0, -0.3, 0.55), "white", bevel=0.06)
        mb.box((0.92, 1.4, 0.18), (0, -0.25, 0.6), "glass")
        mb.box((0.6, 0.8, 0.3), (0, -0.5, 0.9), "white", bevel=0.05)
        mb.box((1.22, 2.6, 0.06), (0, 0.0, 0.3), "gold")
    single("yacht", yacht)


# --------------------------------------------------------------------------
# nature + props
# --------------------------------------------------------------------------
def build_props():
    def tree_round(mb):
        mb.cyl(0.1, 0.8, (0, 0, 0.4), "wood", segs=6)
        mb.ball(0.55, (0, 0, 1.15), "green", subdiv=1)
        mb.ball(0.38, (0.25, 0.1, 1.5), "lime", subdiv=1)
    single("tree_round", tree_round)

    def tree_autumn(mb):
        mb.cyl(0.1, 0.8, (0, 0, 0.4), "darkwood", segs=6)
        mb.ball(0.55, (0, 0, 1.15), "orange", subdiv=1)
        mb.ball(0.36, (-0.2, 0.15, 1.5), "lightorange", subdiv=1)
    single("tree_autumn", tree_autumn)

    def tree_pine(mb):
        mb.cyl(0.09, 0.5, (0, 0, 0.25), "darkwood", segs=6)
        mb.cone(0.6, 0.8, (0, 0, 0.75), "darkgreen", segs=7)
        mb.cone(0.46, 0.7, (0, 0, 1.2), "darkgreen", segs=7)
        mb.cone(0.32, 0.6, (0, 0, 1.6), "forest", segs=7)
    single("tree_pine", tree_pine)

    def tree_snowpine(mb):
        mb.cyl(0.09, 0.5, (0, 0, 0.25), "darkwood", segs=6)
        mb.cone(0.6, 0.8, (0, 0, 0.75), "forest", segs=7)
        mb.cone(0.5, 0.25, (0, 0, 1.08), "white", segs=7, r2=0.3)
        mb.cone(0.42, 0.6, (0, 0, 1.35), "forest", segs=7)
        mb.cone(0.25, 0.4, (0, 0, 1.8), "white", segs=7)
    single("tree_snowpine", tree_snowpine)

    def tree_palm(mb):
        x = 0.0
        for i in range(5):
            x += 0.05
            mb.cyl(0.09 - i * 0.008, 0.38, (x, 0, 0.2 + i * 0.36), "wood", segs=6)
        top = (x + 0.05, 0, 1.95)
        for i in range(6):
            a = i * math.pi / 3
            mb.box((0.9, 0.22, 0.04), (top[0] + math.cos(a) * 0.4, math.sin(a) * 0.4, top[2] - 0.08),
                   "green", rot=(0, 0.35, a))
        mb.ball(0.12, top, "darkbrown", subdiv=1)
    single("tree_palm", tree_palm)

    def tree_dry(mb):
        mb.cyl(0.08, 1.0, (0, 0, 0.5), "darkwood", segs=5)
        mb.cyl(0.05, 0.6, (0.18, 0, 1.1), "darkwood", segs=5, rot=(0, 0.6, 0))
        mb.cyl(0.05, 0.5, (-0.15, 0.05, 1.0), "darkwood", segs=5, rot=(0, -0.7, 0))
        mb.ball(0.18, (0.36, 0, 1.38), "olive", subdiv=1)
    single("tree_dry", tree_dry)

    def cactus(mb):
        mb.cyl(0.14, 1.1, (0, 0, 0.55), "green", segs=7)
        mb.ball(0.14, (0, 0, 1.1), "green", subdiv=1)
        mb.cyl(0.08, 0.3, (0.2, 0, 0.55), "green", segs=6, axis="X")
        mb.cyl(0.08, 0.36, (0.32, 0, 0.75), "green", segs=6)
        mb.cyl(0.08, 0.26, (-0.18, 0, 0.7), "green", segs=6, axis="X")
        mb.cyl(0.08, 0.3, (-0.28, 0, 0.85), "green", segs=6)
        mb.ball(0.06, (0, 0.05, 1.22), "pink", subdiv=1)
    single("cactus", cactus)

    def bush(mb):
        mb.ball(0.32, (0, 0, 0.22), "darkgreen", subdiv=1, scale=(1, 1, 0.75))
        mb.ball(0.24, (0.25, 0.1, 0.2), "green", subdiv=1, scale=(1, 1, 0.75))
    single("bush", bush)

    def rock_small(mb):
        mb.ball(0.4, (0, 0, 0.15), "gray", subdiv=1, scale=(1.2, 1.0, 0.7))
        mb.ball(0.25, (0.35, 0.2, 0.1), "lightgray", subdiv=1, scale=(1, 1, 0.8))
    single("rock_small", rock_small)

    def rock_desert(mb):
        mb.box((1.6, 1.3, 1.2), (0, 0, 0.6), "desertrock", taper=(0.75, 0.75), bevel=0.12)
        mb.box((1.8, 1.5, 0.18), (0, 0, 1.22), "redrock", bevel=0.05)
        mb.box((0.8, 0.7, 0.7), (0.9, 0.5, 0.35), "desertrock", taper=(0.7, 0.7), bevel=0.08)
    single("rock_desert", rock_desert)

    def mountain(mb):
        mb.cone(3.0, 4.0, (0, 0, 2.0), "gray", segs=6)
        mb.cone(2.2, 3.0, (2.2, 1.0, 1.5), "darkgray", segs=6, rot=(0, 0, 0.4))
        mb.cone(1.8, 2.4, (-2.0, -0.8, 1.2), "steel", segs=6, rot=(0, 0, 0.2))
        mb.cone(1.1, 1.2, (0, 0, 3.4), "white", segs=6)
    single("mountain", mountain)

    def mountain_desert(mb):
        mb.box((4.0, 3.4, 2.4), (0, 0, 1.2), "redrock", taper=(0.7, 0.75), bevel=0.2)
        mb.box((4.6, 3.8, 0.3), (0, 0, 2.5), "desertrock", taper=(0.95, 0.95), bevel=0.08)
        mb.box((2.4, 2.0, 1.6), (2.6, 1.2, 0.8), "desertrock", taper=(0.7, 0.7), bevel=0.15)
        mb.box((2.6, 2.2, 0.2), (2.6, 1.2, 1.65), "redrock")
    single("mountain_desert", mountain_desert)

    def mountain_snow(mb):
        mb.cone(3.2, 4.4, (0, 0, 2.2), "lightgray", segs=6)
        mb.cone(1.9, 1.9, (0, 0, 3.4), "white", segs=6)
        mb.cone(2.2, 3.0, (2.4, 0.8, 1.5), "gray", segs=6, rot=(0, 0, 0.5))
        mb.cone(1.0, 1.0, (2.4, 0.8, 2.6), "white", segs=6, rot=(0, 0, 0.5))
    single("mountain_snow", mountain_snow)

    def street_light(mb):
        mb.cyl(0.05, 1.8, (0, 0, 0.9), "darkgray", segs=6)
        mb.box((0.06, 0.6, 0.06), (0, 0.27, 1.8), "darkgray")
        mb.box((0.18, 0.24, 0.08), (0, 0.55, 1.74), "lamp")
    single("street_light", street_light)

    def sign_post(mb):
        mb.box((0.06, 0.06, 1.2), (0, 0, 0.6), "steel")
        mb.box((0.9, 0.05, 0.4), (0, 0, 1.1), "darkgreen", bevel=0.02)
        mb.box((0.7, 0.06, 0.06), (0, 0, 1.12), "white")
    single("sign_post", sign_post)

    def crate(mb):
        mb.box((0.4, 0.4, 0.4), (0, 0, 0.2), "wood", bevel=0.03)
        mb.box((0.42, 0.06, 0.42), (0, 0, 0.2), "darkwood")
        mb.box((0.06, 0.42, 0.42), (0, 0, 0.2), "darkwood")
    single("crate", crate)

    def barrier(mb):
        mb.box((1.2, 0.3, 0.42), (0, 0, 0.21), "concrete", taper=(1.0, 0.6), bevel=0.03)
        for x in (-0.4, 0.0, 0.4):
            mb.box((0.2, 0.32, 0.12), (x, 0, 0.3), "red")
    single("barrier", barrier)

    def traffic_cone(mb):
        mb.box((0.3, 0.3, 0.04), (0, 0, 0.02), "orange")
        mb.cone(0.12, 0.4, (0, 0, 0.22), "orange", segs=6)
        mb.cyl(0.085, 0.06, (0, 0, 0.22), "white", segs=6, r2=0.07)
    single("traffic_cone", traffic_cone)

    def bridge(mb):  # deck along Y, 5 long, top surface at z = 0
        mb.box((2.6, 5.0, 0.3), (0, 0, -0.15), "concrete")
        for x in (-1.25, 1.25):
            mb.box((0.12, 5.0, 0.1), (x, 0, 0.32), "red")
            for y in (-2.2, -1.1, 0.0, 1.1, 2.2):
                mb.box((0.08, 0.08, 0.32), (x, y, 0.16), "white")
        for y in (-1.2, 1.2):
            mb.box((1.6, 0.5, 1.6), (0, y, -1.0), "gray", taper=(0.8, 0.8))
    single("bridge", bridge)

    def tunnel(mb):  # rocky arch over a road along Y
        for x in (-1.75, 1.75):
            mb.box((1.3, 2.2, 2.2), (x, 0, 1.1), "gray", taper=(0.8, 0.9), bevel=0.15)
        mb.box((4.8, 2.4, 1.0), (0, 0, 2.5), "darkgray", taper=(0.7, 0.8), bevel=0.2)
        mb.cone(1.4, 1.4, (0.6, 0, 3.4), "gray", segs=6)
        mb.box((2.6, 0.12, 0.3), (0, 1.15, 1.95), "yellow")
        mb.box((2.0, 0.14, 0.2), (0, 1.17, 1.95), "black")
    single("tunnel", tunnel)

    def cloud(mb):
        mb.ball(1.0, (0, 0, 0), "white", subdiv=1, scale=(1.3, 1.0, 0.7))
        mb.ball(0.8, (1.1, 0.2, -0.1), "white", subdiv=1, scale=(1.2, 1.0, 0.7))
        mb.ball(0.7, (-1.0, -0.1, -0.1), "white", subdiv=1, scale=(1.2, 1.0, 0.7))
        mb.ball(0.6, (0.3, -0.6, 0.3), "ice", subdiv=1)
    single("cloud", cloud)

    def lock(mb):
        mb.box((0.9, 0.4, 0.8), (0, 0, 0.4), "gold", bevel=0.08)
        mb.box((0.12, 0.42, 0.24), (0, 0, 0.45), "darkbrown")
        for x in (-0.28, 0.28):
            mb.box((0.14, 0.18, 0.4), (x, 0, 0.98), "steel")
        mb.box((0.7, 0.18, 0.14), (0, 0, 1.2), "steel", bevel=0.04)
    single("lock", lock)

    def pin(mb):
        mb.cone(0.18, 0.5, (0, 0, 0.25), "paint", segs=6, r2=0.0, rot=(math.pi, 0, 0))
        mb.ball(0.3, (0, 0, 0.7), "paint", subdiv=1)
        mb.ball(0.13, (0, 0.2, 0.75), "white", subdiv=1)
    single("pin", pin)

    def depot_sign(mb):
        mb.box((0.08, 0.08, 1.6), (0, 0, 0.8), "darkgray")
        mb.box((1.0, 0.1, 0.6), (0, 0, 1.6), "paint", bevel=0.03)
        mb.box((0.7, 0.12, 0.14), (0, 0, 1.62), "white")
    single("depot_sign", depot_sign)

    def fence(mb):
        for x in (-0.9, 0.0, 0.9):
            mb.box((0.08, 0.08, 0.5), (x, 0, 0.25), "wood")
        mb.box((1.9, 0.05, 0.08), (0, 0, 0.38), "wood")
        mb.box((1.9, 0.05, 0.08), (0, 0, 0.18), "wood")
    single("fence", fence)


def main():
    global MAT
    if not os.path.exists(PALETTE_PNG):
        write_png(PALETTE_PNG)
    reset_scene()
    MAT = palette_material()
    build_vehicles()
    build_attachments()
    build_characters()
    build_garages()
    build_buildings()
    build_props()
    os.makedirs(os.path.dirname(META_PATH), exist_ok=True)
    with open(META_PATH, "w") as f:
        json.dump(META, f, indent=1, sort_keys=True)
    print("done: %d models" % len(META))


if __name__ == "__main__":
    main()
