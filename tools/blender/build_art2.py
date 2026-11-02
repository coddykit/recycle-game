"""Art pass 2 (10-27): Dust Valley mesa + the locked-region clouds. Shared palette, no new textures.
Run AFTER build_assets.py (it overwrites mountain_desert.glb and cloud.glb):
    /Volumes/BuildSSD/blender/blender -b -P tools/blender/build_art2.py

- mountain_desert (Medium/High): the old one was a tapered red box under an overhanging tan slab — from the game camera
  it read as an orange house roof / a slanted slab (W66 model #2). Now a banded sandstone mesa: jittered strata,
  a stepped caprock set INSIDE the walls, a second smaller butte and talus blocks at the foot.
- mountain_desert_low (Low): the same stepped silhouette in <= 144 triangles (the old model's count).
- rock_desert (Medium/High) / rock_desert_low (Low, <= 132 = old): the "orange slab south of Cactus Junction" (W66
  model #2) was this rock — a tan block under an overhanging red slab. Now banded sandstone boulders (convex, no slab).
- cloud (every tier, <= 80 triangles = old): four faceted icospheres -> four flat-bottomed puffs with smooth normals.
"""
import json
import math
import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_assets as a  # noqa: E402
import bpy  # noqa: E402
from palette import uv_for  # noqa: E402

a.META = json.loads(Path(a.META_PATH).read_text())
a.reset_scene()
a.MAT = a.palette_material()
counts = {}


def ring(cx, cy, z, rx, ry, segs, jitter, rnd, rot=0.0):
    pts = []
    for i in range(segs):
        ang = rot + 2 * math.pi * i / segs
        k = 1.0 + rnd.uniform(-jitter, jitter)
        pts.append((cx + math.cos(ang) * rx * k, cy + math.sin(ang) * ry * k, z))
    return pts


def loft(m, rings, colors, cap_top=None):
    """Side bands between consecutive rings (same point count), band i painted colors[i]; optional flat top cap."""
    vrings = [[m.bm.verts.new(p) for p in r] for r in rings]
    for i in range(len(vrings) - 1):
        lo, hi = vrings[i], vrings[i + 1]
        n = len(lo)
        u, v = uv_for(colors[i])
        for j in range(n):
            k = (j + 1) % n
            f = m.bm.faces.new([lo[j], lo[k], hi[k], hi[j]])
            for loop in f.loops:
                loop[m.uv].uv = (u, v)
    if cap_top:
        u, v = uv_for(cap_top)
        f = m.bm.faces.new(vrings[-1])
        for loop in f.loops:
            loop[m.uv].uv = (u, v)


def butte(m, cx, cy, rx, ry, layers, segs, rnd, top, rot=0.0):
    """layers: [(z, scale, colour of the band above)] bottom-up; the last z is the cap height."""
    rings, cols = [], []
    for z, s, col in layers:
        rings.append(ring(cx, cy, z, rx * s, ry * s, segs, 0.06, rnd, rot))
        cols.append(col)
    loft(m, rings, cols, cap_top=top)


def mesa(m, detail):
    rnd = random.Random(11)
    if detail:
        # main mesa: talus skirt, three sandstone strata, a ledge step, caprock inset
        butte(m, 0.0, 0.0, 2.2, 1.8, [
            (0.0, 1.18, 'clay'), (0.35, 1.02, 'redrock'), (0.95, 0.98, 'terracotta'), (1.5, 0.95, 'redrock'),
            (1.95, 0.92, 'desertrock'), (2.2, 0.80, 'desertrock'), (2.5, 0.78, 'lightorange'), (2.62, 0.74, 'clay')],
            10, rnd, top='desertrock', rot=0.2)
        # caprock ledge stripe (thin darker band on top edge)
        butte(m, 0.25, -0.1, 1.15, 0.95, [(2.6, 1.0, 'redrock'), (2.95, 0.92, 'desertrock')], 8, rnd,
              top='lightorange', rot=0.5)
        # second, lower butte to the side (the old model's secondary block)
        butte(m, 2.7, 1.1, 1.05, 0.9, [
            (0.0, 1.2, 'clay'), (0.3, 1.0, 'terracotta'), (0.85, 0.95, 'redrock'), (1.3, 0.9, 'desertrock'),
            (1.55, 0.8, 'clay')], 8, rnd, top='desertrock', rot=0.1)
        # talus blocks
        for x, y, s in [(-2.2, -1.2, 0.45), (-1.6, 1.7, 0.35), (1.6, -1.9, 0.4), (3.6, 0.0, 0.3), (0.6, 2.0, 0.3)]:
            m.box((s * 1.4, s * 1.1, s), (x, y, s * 0.45), 'desertrock', rot=(0, 0, rnd.uniform(0, 3)),
                  taper=(0.7, 0.7))
    else:
        butte(m, 0.0, 0.0, 2.2, 1.8, [(0.0, 1.12, 'redrock'), (1.1, 0.97, 'terracotta'), (2.0, 0.92, 'desertrock'),
                                      (2.62, 0.76, 'clay')], 8, rnd, top='desertrock', rot=0.2)
        butte(m, 2.7, 1.1, 1.05, 0.9, [(0.0, 1.12, 'terracotta'), (0.9, 0.95, 'redrock'), (1.55, 0.8, 'clay')], 6,
              rnd, top='desertrock', rot=0.1)
        for x, y, s in [(-2.2, -1.2, 0.45), (1.6, -1.9, 0.4)]:
            m.box((s * 1.4, s * 1.1, s), (x, y, s * 0.45), 'desertrock', rot=(0, 0, rnd.uniform(0, 3)))


def boulder(m, cx, cy, rx, ry, h, npts, rnd, bands):
    """Convex sandstone boulder from a jittered point cloud; faces painted by height (bands: [(z_frac, colour)])."""
    import bmesh
    pts = []
    for z_frac, r_frac, n in ((0.0, 1.0, npts), (0.42, 0.95, npts), (0.78, 0.62, max(3, npts - 2))):
        rot = rnd.uniform(0, 1)
        for i in range(n):
            ang = rot + 2 * math.pi * i / n + rnd.uniform(-0.2, 0.2)
            k = r_frac * rnd.uniform(0.86, 1.06)
            pts.append((cx + math.cos(ang) * rx * k, cy + math.sin(ang) * ry * k,
                        h * z_frac * (rnd.uniform(0.9, 1.1) if z_frac else 1.0)))
    for i in range(2 if npts > 6 else 1):
        pts.append((cx + rnd.uniform(-0.2, 0.2) * rx, cy + rnd.uniform(-0.2, 0.2) * ry, h * rnd.uniform(0.94, 1.0)))
    verts = [m.bm.verts.new(p) for p in pts]
    res = bmesh.ops.convex_hull(m.bm, input=verts)
    loose = {g for g in res.get('geom_interior', []) + res.get('geom_unused', []) if isinstance(g, bmesh.types.BMVert)}
    if loose:
        bmesh.ops.delete(m.bm, geom=list(loose), context='VERTS')
    for f in [g for g in res['geom'] if isinstance(g, bmesh.types.BMFace)]:
        zc = sum(v.co.z for v in f.verts) / len(f.verts) / max(h, 1e-6)
        col = bands[0][1]
        for frac, c in bands:
            if zc >= frac:
                col = c
        u, v = uv_for(col)
        for loop in f.loops:
            loop[m.uv].uv = (u, v)
    # the hull stays closed (its flat underside included): MB.build's normal recalculation needs a closed shell


ROCK_BANDS = [(0.0, 'desertrock'), (0.35, 'terracotta'), (0.62, 'clay'), (0.85, 'lightorange')]


def rock(m, detail):
    """Old rock_desert: a tapered tan block under an overhanging red slab = 'orange slab' from the camera (W66 #2)."""
    rnd = random.Random(7)
    if detail:
        boulder(m, 0.0, 0.0, 0.95, 0.75, 1.25, 9, rnd, ROCK_BANDS)
        boulder(m, 0.95, 0.5, 0.5, 0.42, 0.7, 7, rnd, ROCK_BANDS)
        boulder(m, -0.8, 0.6, 0.3, 0.26, 0.32, 6, rnd, ROCK_BANDS)
    else:
        boulder(m, 0.0, 0.0, 0.95, 0.75, 1.25, 7, rnd, ROCK_BANDS)
        boulder(m, 0.95, 0.5, 0.5, 0.42, 0.7, 5, rnd, ROCK_BANDS)


def cloud(m):
    """Four flat-bottomed puffs (hemispheres: 6 sides x 2 rings), smooth-shaded, white tops / ice undersides."""
    rnd = random.Random(5)
    puffs = [(0.0, 0.0, 0.0, 1.15), (1.2, 0.25, -0.08, 0.85), (-1.15, -0.15, -0.08, 0.8), (0.15, 0.1, 0.5, 0.62)]
    for x, y, z, r in puffs:
        rot = rnd.uniform(0, 1)
        rings = [ring(x, y, z, r * 1.25, r, 6, 0.0, rnd, rot),
                 ring(x, y, z + r * 0.42, r * 1.12, r * 0.9, 6, 0.0, rnd, rot + 0.26)]
        vr = [[m.bm.verts.new(p) for p in rr] for rr in rings]
        top = m.bm.verts.new((x, y, z + r * 0.72))
        for j in range(6):
            k = (j + 1) % 6
            f = m.bm.faces.new([vr[0][j], vr[0][k], vr[1][k], vr[1][j]])
            for loop in f.loops:
                loop[m.uv].uv = uv_for('ice')
            f = m.bm.faces.new([vr[1][j], vr[1][k], top])
            for loop in f.loops:
                loop[m.uv].uv = uv_for('white')


def emit(name, fn, budget, smooth=False):
    col = a.new_collection(name)
    mb = a.MB()
    fn(mb)
    obj = mb.build(name, col)
    if smooth:
        for p in obj.data.polygons:
            p.use_smooth = True
    obj.data.calc_loop_triangles()
    count = len(obj.data.loop_triangles)
    assert count <= budget, (name, count, budget)
    counts[name] = count
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(filepath=str(Path(a.OUT_DIR) / (name + '.glb')), export_format='GLB',
                              use_selection=True, export_yup=True, export_apply=True, export_materials='NONE',
                              export_normals=True, export_animations=False)
    bounds = [(v.co.x, v.co.z, -v.co.y) for v in obj.data.vertices]
    mn = [min(v[i] for v in bounds) for i in range(3)]
    mx = [max(v[i] for v in bounds) for i in range(3)]
    a.META[name] = {'min': [round(v, 3) for v in mn], 'max': [round(v, 3) for v in mx],
                    'size': [round(mx[i] - mn[i], 3) for i in range(3)]}
    bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.collections.remove(col)


emit('mountain_desert', lambda m: mesa(m, True), 420)
emit('mountain_desert_low', lambda m: mesa(m, False), 144)
emit('rock_desert', lambda m: rock(m, True), 200)
emit('rock_desert_low', lambda m: rock(m, False), 132)
emit('cloud', cloud, 80, smooth=True)
Path(a.META_PATH).write_text(json.dumps(a.META, indent=1, sort_keys=True) + '\n')
Path(a.ROOT, 'docs/art/art2-models.json').write_text(json.dumps(counts, indent=2) + '\n')
print('ART2', counts)
