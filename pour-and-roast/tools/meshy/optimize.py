#!/usr/bin/env python3
"""Meshy GLB -> game-ready Godot GLBs (LOD0/1/2, 1024 + 512 textures) + a turntable strip, Blender headless.

  python3 optimize.py <in.glb> <out dir> --name chef --height 1.72 --lods 3000,1200,500 [options]

Runs /Volumes/BuildSSD/blender/blender -b (through novareka-dev/shot-lock.sh when it exists) on this same file.
Pipeline: import -> join -> merge by distance / drop loose -> apply transforms -> optional style edits (head scale,
squash above a height = cut a tall hat into a wrap) -> pivot bottom centre, +Y up, faces +Z (glTF), 1 unit = 1 m ->
texture: recolour hue ranges / paint out rectangles (stray text) / mild posterize + saturation -> 1024 + 512 PNG ->
one material (name = --material, e.g. the game's "paint" / "doner_base") -> Decimate (collapse) per LOD budget ->
export <name>_lod{0,1,2}.glb (1024 texture) + <name>_lod{0,1,2}_low.glb (512) -> turntable PNG (8 views of LOD0 + LOD2
row) -> report.json (triangles per LOD, size, texture sizes).

Options:
  --height H            uniform scale to this height (m)       --fit X,H,D   non-uniform fit to a box (m) instead
  --yaw DEG             extra rotation about up before export (if the model does not face +Z)
  --head-scale S --neck F   scale everything above F x height by S around the neck point (big-head stylisation)
  --squash-above F:K    vertices above F x height move to F + (z - F) x K (flatten a tall hat)
  --recolor H0-H1:R,G,B[:minsat]  hue range in degrees -> the target colour (keeps the texel's lightness pattern)
  --paint U0,V0,U1,V1[:R,G,B]     fill a UV rectangle (0..1, V down as in the PNG) with a colour (default = rect median)
  --posterize N         quantize the texture to N colours (flat / toon look; 0 = off)
  --saturation S        multiply saturation
  --material NAME       material name in the exported GLBs (game import scripts key on it)
  --roughness R         exported material roughness (default 0.85)
  --open-top Z,FRONT     remove roof / ceiling faces above Z m behind the front FRONT m (cut-away for top-down cameras)
  --extra-anims A.glb,B.glb   rigged: merge the clips of other GLBs of the same rig (Meshy basic walk / run)
  --keep-armature       rigged input: keep the armature + skin + animations (no style edits, pivot/scale only)
"""
import json
import os
import shutil
import subprocess
import sys

BLENDER = os.environ.get("BLENDER", shutil.which("blender") or "/Volumes/BuildSSD/blender/blender")
LOCK = "/Volumes/BuildSSD/novareka-dev/shot-lock.sh"


def launch():
    a = parse(sys.argv[1:])
    os.makedirs(a.out, exist_ok=True)
    # texture work happens here (system python has PIL; Blender's bundled python does not)
    img = glb_base_color(a.src)
    if img is not None:
        from PIL import Image
        im = _texture(img.convert("RGB"), a)
        for size, tag in ((a.tex, ""), (a.tex_low, "_low")):
            im.resize((size, size), Image.LANCZOS).save(os.path.join(a.out, f"{a.name}_base{tag}.png"), optimize=True)
    here = os.path.abspath(__file__)
    cmd = [BLENDER, "-b", "--factory-startup", "-P", here, "--"] + sys.argv[1:]
    if os.path.exists(LOCK) and not os.environ.get("NO_SHOT_LOCK"):
        cmd = ["sh", LOCK] + cmd
    env = dict(os.environ)
    env.setdefault("TMPDIR", os.environ.get("TMPDIR", "/tmp"))
    r = subprocess.run(cmd, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    for line in r.stdout.splitlines():
        if line.startswith(("OPT ", "Error", "Traceback")) or "Error:" in line:
            print(line)
    if r.returncode != 0 or "OPT DONE" not in r.stdout:
        print(r.stdout[-3000:])
        sys.exit(r.returncode or 1)
    if not a.no_turntable:
        compose_turntable(a)


def glb_base_color(path):
    """The base-colour image embedded in a GLB (first image of the first material's baseColorTexture), as PIL."""
    import io
    import struct
    from PIL import Image
    with open(path, "rb") as f:
        data = f.read()
    magic, _ver, _len = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF":
        return None
    off, js, binc = 12, None, None
    while off < len(data):
        clen, ctype = struct.unpack_from("<I4s", data, off)
        chunk = data[off + 8: off + 8 + clen]
        if ctype == b"JSON":
            js = json.loads(chunk)
        elif ctype == b"BIN\x00":
            binc = chunk
        off += 8 + clen
    if not js or not js.get("images"):
        return None
    idx = 0
    for m in js.get("materials", []):
        t = m.get("pbrMetallicRoughness", {}).get("baseColorTexture")
        if t is not None:
            idx = js["textures"][t["index"]]["source"]
            break
    im = js["images"][idx]
    bv = js["bufferViews"][im["bufferView"]]
    raw = binc[bv.get("byteOffset", 0): bv.get("byteOffset", 0) + bv["byteLength"]]
    return Image.open(io.BytesIO(raw))


def compose_turntable(a):
    from PIL import Image
    tiles = sorted(p for p in os.listdir(a.out) if p.startswith("_tt_") and p.endswith(".png"))
    if not tiles:
        return
    rows = sorted({int(p.split("_")[2]) for p in tiles})
    strip = Image.new("RGB", (384 * 8, 384 * len(rows)), (40, 40, 40))
    for p in tiles:
        r, k = int(p.split("_")[2]), int(p.split("_")[3].split(".")[0])
        strip.paste(Image.open(os.path.join(a.out, p)).convert("RGB"), (k * 384, rows.index(r) * 384))
        os.remove(os.path.join(a.out, p))
    sp = os.path.join(a.out, f"{a.name}_turntable.png")
    strip.save(sp)
    rep = os.path.join(a.out, f"{a.name}_report.json")
    if os.path.exists(rep):
        d = json.load(open(rep)); d["turntable"] = sp
        json.dump(d, open(rep, "w"), indent=1)
    print("OPT turntable", sp)


def parse(argv):
    import argparse
    ap = argparse.ArgumentParser()
    ap.add_argument("src"); ap.add_argument("out")
    ap.add_argument("--name", required=True)
    ap.add_argument("--height", type=float); ap.add_argument("--fit")
    ap.add_argument("--yaw", type=float, default=0.0)
    ap.add_argument("--lods", default="3000,1200,500")
    ap.add_argument("--tex", type=int, default=1024); ap.add_argument("--tex-low", type=int, default=512)
    ap.add_argument("--head-scale", type=float, default=1.0); ap.add_argument("--neck", type=float, default=0.84)
    ap.add_argument("--squash-above")
    ap.add_argument("--open-top")
    ap.add_argument("--recolor", action="append", default=[])
    ap.add_argument("--paint", action="append", default=[])
    ap.add_argument("--posterize", type=int, default=0)
    ap.add_argument("--saturation", type=float, default=1.0)
    ap.add_argument("--material", default="paint")
    ap.add_argument("--roughness", type=float, default=0.85)
    ap.add_argument("--keep-armature", action="store_true")
    ap.add_argument("--extra-anims", default="", help="rigged: more Meshy clip GLBs (same rig) merged in as actions")
    ap.add_argument("--no-turntable", action="store_true")
    return ap.parse_args(argv)


# ------------------------------------------------------------------------------------------------ inside Blender
def run_blender(a):
    import bpy
    import bmesh
    import colorsys
    from mathutils import Vector, Matrix

    os.makedirs(a.out, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=a.src)
    for extra in [x for x in a.extra_anims.split(",") if x]:
        keep = set(bpy.context.scene.objects)
        had = set(bpy.data.actions)
        bpy.ops.import_scene.gltf(filepath=extra)
        base = os.path.basename(extra).lower()
        label = "Walk" if "walk" in base else "Run" if "run" in base else os.path.splitext(base)[0]
        for k, act in enumerate([x for x in bpy.data.actions if x not in had]):
            act.name = label if k == 0 else f"{label}_{k}"
            act.use_fake_user = True
        for o in list(bpy.context.scene.objects):
            if o not in keep:
                bpy.data.objects.remove(o)
    for act in bpy.data.actions:   # "Armature|Idle|baselayer" -> "Idle"
        if act.name.count("|") >= 2:
            act.name = act.name.split("|")[1]
    arm = next((o for o in bpy.context.scene.objects if o.type == "ARMATURE"), None)
    if arm is not None:
        # the glTF importer adds a bone-shape helper mesh ("Icosphere") to armatures: keep only skinned meshes
        for o in list(bpy.context.scene.objects):
            if o.type == "MESH" and not any(m.type == "ARMATURE" for m in o.modifiers):
                print("OPT drop unskinned mesh", o.name)
                bpy.data.objects.remove(o)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for o in bpy.context.scene.objects:
        o.select_set(o in meshes)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    src_img = None
    for m in obj.data.materials:
        if m and m.use_nodes:
            for n in m.node_tree.nodes:
                if n.type == "TEX_IMAGE" and n.image is not None and src_img is None:
                    src_img = n.image
    src_tris = _tris(obj)

    # ---- clean + bake the object transform (rigged: weld only, leave the skin transforms alone)
    if arm is not None:
        # Meshy's rigged mesh comes split at every UV seam; Decimate on an unwelded mesh tears holes, so weld
        # coincident vertices first (they carry the same weights).
        bm = bmesh.new(); bm.from_mesh(obj.data)
        before = len(bm.verts)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
        print(f"OPT weld {before} -> {len(bm.verts)} verts")
        bm.to_mesh(obj.data); bm.free(); obj.data.update()
    if arm is None:
        obj.select_set(True)
        bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        bm = bmesh.new(); bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        loose = [v for v in bm.verts if not v.link_faces]
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(obj.data); bm.free()
        for o in list(bpy.context.scene.objects):
            if o.type == "EMPTY":
                bpy.data.objects.remove(o)

    root = arm if arm is not None else obj

    def bbox():
        obj.data.update()
        bpy.context.view_layer.update()
        pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
        mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        return mn, mx

    # ---- style edits on the raw mesh (Blender Z up)
    if arm is None:
        mn, mx = bbox(); h = mx.z - mn.z
        if a.squash_above:
            f, k = (float(x) for x in a.squash_above.split(":"))
            zt = mn.z + f * h
            for v in obj.data.vertices:
                if v.co.z > zt:
                    v.co.z = zt + (v.co.z - zt) * k
            mn, mx = bbox(); h = mx.z - mn.z
        if abs(a.head_scale - 1.0) > 1e-3:
            zn = mn.z + a.neck * h
            band = 0.04 * h
            neck_pts = [v.co for v in obj.data.vertices if abs(v.co.z - zn) < band]
            cx = sum(p.x for p in neck_pts) / max(1, len(neck_pts))
            cy = sum(p.y for p in neck_pts) / max(1, len(neck_pts))
            pivot = Vector((cx, cy, zn))
            for v in obj.data.vertices:
                t = (v.co.z - (zn - band)) / (2 * band)
                t = 0.0 if t < 0 else 1.0 if t > 1 else t * t * (3 - 2 * t)
                s = 1.0 + (a.head_scale - 1.0) * t
                v.co = pivot + (v.co - pivot) * s
        # yaw, then pivot bottom centre + scale
        if a.yaw:
            obj.data.transform(Matrix.Rotation(__import__("math").radians(a.yaw), 4, "Z"))
        mn, mx = bbox(); h = mx.z - mn.z
        c = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
        obj.data.transform(Matrix.Translation(-c))
        if a.fit:
            fx, fh, fd = (float(x) for x in a.fit.split(","))
            sx, sz, sy = fx / (mx.x - mn.x), fh / h, fd / (mx.y - mn.y)
            obj.data.transform(Matrix.Diagonal((sx, sy, sz, 1.0)))
        elif a.height:
            s = a.height / h
            obj.data.transform(Matrix.Scale(s, 4))
        obj.data.update()
        if a.open_top:
            # cut-away roof for a top-down game camera: drop the horizontal faces above Z (m) that lie behind the
            # front strip (glTF z < FRONT m; the awning / fascia at the front stay)
            zc, front = (float(x) for x in a.open_top.split(","))
            bm = bmesh.new(); bm.from_mesh(obj.data)
            kill = [f for f in bm.faces if abs(f.normal.z) > 0.7 and f.calc_center_median().z >= zc
                    and f.calc_center_median().y > -front]
            bmesh.ops.delete(bm, geom=kill, context="FACES")
            bm.to_mesh(obj.data); bm.free(); obj.data.update()
            print(f"OPT open-top removed {len(kill)} faces")
    else:
        # rigged: Meshy returns the character at the height it was rigged at (height_meters), feet on the ground,
        # facing +Z; the armature carries a 0.01 scale (cm bones). Leave the transforms alone (applying the scale
        # would break the location keys of the clips); only report the size.
        mn, mx = bbox()
        print(f"OPT rigged size h={mx.z - mn.z:.3f} minz={mn.z:.3f} actions={[x.name for x in bpy.data.actions]}")

    # ---- texture: the launcher wrote <name>_base.png / _base_low.png (PIL work outside Blender)
    tex_paths = {}
    for tag in ("", "_low"):
        p = os.path.join(a.out, f"{a.name}_base{tag}.png")
        if src_img is not None and os.path.exists(p):
            tex_paths[tag] = p

    def set_material(texpath):
        mat = bpy.data.materials.get(a.material) or bpy.data.materials.new(a.material)
        mat.use_nodes = True
        nt = mat.node_tree
        nt.nodes.clear()
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.inputs["Roughness"].default_value = a.roughness
        bsdf.inputs["Metallic"].default_value = 0.0
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
        if texpath:
            tn = nt.nodes.new("ShaderNodeTexImage")
            img = bpy.data.images.load(texpath, check_existing=False)
            tn.image = img
            nt.links.new(tn.outputs["Color"], bsdf.inputs["Base Color"])
        obj.data.materials.clear()
        obj.data.materials.append(mat)
        return mat

    # ---- LODs
    budgets = [int(x) for x in a.lods.split(",")]
    report = {"name": a.name, "source": os.path.basename(a.src), "source_tris": src_tris, "lods": [],
              "textures": {k or "main": [a.tex if not k else a.tex_low] * 2 for k in tex_paths}}
    base_mesh = obj.data.copy()
    lod_objs = []
    for i, budget in enumerate(budgets):
        obj.data = base_mesh.copy()
        for m in list(obj.modifiers):   # keep the Armature modifier (skin export needs it)
            if m.type == "DECIMATE":
                obj.modifiers.remove(m)
        n = _tris(obj)
        if n > budget:
            mod = obj.modifiers.new("dec", "DECIMATE")
            mod.decimate_type = "COLLAPSE"
            mod.ratio = budget / n
            mod.use_collapse_triangulate = True
            # keep the skin weights / UV seams as well as collapse can
            for x in bpy.context.scene.objects:
                x.select_set(x == obj)
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.modifier_move_to_index(modifier="dec", index=0)   # before the Armature: no pose baked
            bpy.ops.object.modifier_apply(modifier="dec")
        tris = _tris(obj)
        mn, mx = bbox()
        size = [round(mx.x - mn.x, 3), round(mx.z - mn.z, 3), round(mx.y - mn.y, 3)]
        entry = {"lod": i, "budget": budget, "triangles": tris, "size_xhd_m": size, "files": []}
        for tag, tp in (tex_paths.items() if tex_paths else [("", None)]):
            set_material(tp)
            path = os.path.join(a.out, f"{a.name}_lod{i}{tag}.glb")
            for x in bpy.context.scene.objects:
                x.select_set(x is obj or x is arm)
            bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_yup=True,
                                      export_apply=False, export_animations=arm is not None,
                                      export_image_format="AUTO", export_texcoords=True, export_normals=True,
                                      export_materials="EXPORT", export_skins=arm is not None)
            entry["files"].append({"path": path, "bytes": os.path.getsize(path)})
        report["lods"].append(entry)
        keep = bpy.data.objects.new(f"{a.name}_lod{i}", obj.data.copy())
        lod_objs.append(keep)
        print(f"OPT LOD{i} budget={budget} triangles={tris} size={size}")

    if not a.no_turntable:
        _turntable(a, obj, lod_objs, report)
    with open(os.path.join(a.out, f"{a.name}_report.json"), "w") as f:
        json.dump(report, f, indent=1)
    print("OPT DONE", json.dumps({"lods": [(l["lod"], l["triangles"]) for l in report["lods"]]}))


def _tris(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def _texture(im, a):
    from PIL import Image
    import colorsys
    px = im.load()
    w, h = im.size
    for spec in a.recolor:
        parts = spec.split(":")
        h0, h1 = (float(x) for x in parts[0].split("-"))
        tr, tg, tb = (int(x) for x in parts[1].split(","))
        minsat = float(parts[2]) if len(parts) > 2 else 0.2
        th, ts, tv = colorsys.rgb_to_hsv(tr / 255, tg / 255, tb / 255)
        for y in range(h):
            for x in range(w):
                r, g, b = px[x, y]
                hh, ss, vv = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
                if ss >= minsat and h0 <= hh * 360 <= h1:
                    nv = max(0.0, min(1.0, tv * (0.55 + 0.6 * vv)))
                    nr, ng, nb = colorsys.hsv_to_rgb(th, ts, nv)
                    px[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255))
    for spec in a.paint:
        parts = spec.split(":")
        u0, v0, u1, v1 = (float(x) for x in parts[0].split(","))
        box = (int(u0 * w), int(v0 * h), int(u1 * w), int(v1 * h))
        if len(parts) > 1:
            col = tuple(int(x) for x in parts[1].split(","))
        else:
            reg = im.crop(box).getdata()
            chans = list(zip(*reg))
            col = tuple(sorted(c)[len(c) // 2] for c in chans)
        im.paste(col, box)
        px = im.load()
    if abs(a.saturation - 1.0) > 1e-3:
        from PIL import ImageEnhance
        im = ImageEnhance.Color(im).enhance(a.saturation)
    if a.posterize:
        im = im.quantize(colors=a.posterize, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB")
    return im


def _turntable(a, obj, lod_objs, report):
    """8 yaw views of LOD0 (top row) and LOD2 (bottom row), Workbench + texture colour, one PNG strip."""
    import bpy
    import math
    from mathutils import Vector
    sc = bpy.context.scene
    for o in list(sc.objects):
        o.hide_render = True
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "TEXTURE"
    sc.display.shading.show_cavity = False
    sc.render.film_transparent = False
    sc.world = sc.world or bpy.data.worlds.new("w")
    sc.render.resolution_x = 384; sc.render.resolution_y = 384
    sc.render.image_settings.file_format = "PNG"
    cam_d = bpy.data.cameras.new("tt"); cam_d.type = "ORTHO"
    cam = bpy.data.objects.new("tt", cam_d); sc.collection.objects.link(cam); sc.camera = cam
    rows = [lod_objs[0], lod_objs[-1]]
    tiles = []
    for r, lo in enumerate(rows):
        sc.collection.objects.link(lo)
        lo.data.materials.clear(); lo.data.materials.append(obj.data.materials[0])
        pts = [Vector(c) for c in lo.bound_box]
        hgt = max(p.z for p in pts); wid = max(max(abs(p.x) for p in pts), max(abs(p.y) for p in pts)) * 2
        cam_d.ortho_scale = max(hgt, wid) * 1.02
        tgt = Vector((0, 0, hgt / 2))
        for k in range(8):
            ang = math.radians(45 * k)
            d = Vector((math.sin(ang), -math.cos(ang), 0.35)).normalized()   # k=0 = front (-Y in Blender = +Z glTF)
            cam.location = tgt + d * max(hgt, wid) * 3
            cam.rotation_euler = (tgt - cam.location).to_track_quat("-Z", "Y").to_euler()
            p = os.path.join(a.out, f"_tt_{r}_{k}.png")
            sc.render.filepath = p
            bpy.ops.render.render(write_still=True)
            tiles.append(p)
        sc.collection.objects.unlink(lo)
    print("OPT tiles", len(tiles))


if __name__ == "__main__":
    try:
        import bpy  # noqa: F401
        inside = True
    except ImportError:
        inside = False
    if inside:
        run_blender(parse(sys.argv[sys.argv.index("--") + 1:]))
    else:
        launch()
