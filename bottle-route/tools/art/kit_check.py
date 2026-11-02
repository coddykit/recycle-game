#!/usr/bin/env python3
"""Bottle Route — offline gate for the merged Blender-kit pieces (Resources/kit/*.bytes, BRKM v1; see kit.py / KitPiece.cs).

  python3 bottle-route/tools/art/kit_check.py      # exit 0 = KIT_CHECK: PASS

Checks per piece: header + exact size, indices in range, every UV on a palette swatch centre (one material), unit normals,
tris ≤ 600 (fleet prop budget), a *_low twin exists and is ≤ 40 % of the full piece (Low tier), and the clockwise-front
winding Unity expects (cross(b-a, c-a) agrees with the stored face normal).
"""
import math
import struct
import sys
from pathlib import Path

KIT = Path(__file__).resolve().parents[2] / "unity/Assets/Resources/kit"
PIECES = ["bld_harbour_a", "bld_harbour_b", "bld_old_a", "bld_old_b", "bld_uni_a", "bld_uni_b", "stadium", "depot_hall"]
MAX_TRIS = 600
fails = []


def load(name):
    p = KIT / (name + ".bytes")
    if not p.exists():
        fails.append(f"{name}: missing {p.name} (blender -b --python tools/art/kit.py -- unity)")
        return None
    b = p.read_bytes()
    if b[:4] != b"BRKM" or len(b) < 16:
        fails.append(f"{name}: bad magic")
        return None
    ver, _, nv, ni = struct.unpack_from("<HHII", b, 4)
    if ver != 1 or len(b) != 16 + nv * 32 + ni * 2 or ni % 3:
        fails.append(f"{name}: bad header / size (v{ver}, {nv} verts, {ni} idx, {len(b)} bytes)")
        return None
    verts = [struct.unpack_from("<8f", b, 16 + i * 32) for i in range(nv)]
    idx = struct.unpack_from("<%dH" % ni, b, 16 + nv * 32)
    return verts, idx


def check(name, verts, idx):
    if max(idx) >= len(verts):
        fails.append(f"{name}: index out of range")
        return
    for v in verts:
        u, w = v[6] * 8, (1 - v[7]) * 8
        if abs(u - math.floor(u) - 0.5) > 1e-3 or abs(w - math.floor(w) - 0.5) > 1e-3:
            fails.append(f"{name}: UV {v[6]:.3f},{v[7]:.3f} is not a palette swatch centre")
            break
        if abs(math.sqrt(v[3] ** 2 + v[4] ** 2 + v[5] ** 2) - 1) > 1e-3:
            fails.append(f"{name}: normal not unit length")
            break
    bad = 0
    for k in range(0, len(idx), 3):
        a, b, c = (verts[i] for i in idx[k:k + 3])
        e1 = (b[0] - a[0], b[1] - a[1], b[2] - a[2])
        e2 = (c[0] - a[0], c[1] - a[1], c[2] - a[2])
        cr = (e1[1] * e2[2] - e1[2] * e2[1], e1[2] * e2[0] - e1[0] * e2[2], e1[0] * e2[1] - e1[1] * e2[0])
        if cr[0] * a[3] + cr[1] * a[4] + cr[2] * a[5] < 0:
            bad += 1
    if bad:
        fails.append(f"{name}: {bad} triangles wound counter-clockwise (Unity culls them)")


print("piece | tris | low tris")
for name in PIECES:
    full, low = load(name), load(name + "_low")
    if not full or not low:
        continue
    check(name, *full)
    check(name + "_low", *low)
    t, tl = len(full[1]) // 3, len(low[1]) // 3
    if t > MAX_TRIS:
        fails.append(f"{name}: {t} tris > {MAX_TRIS}")
    if tl > 0.4 * t:
        fails.append(f"{name}_low: {tl} tris > 40 % of {t}")
    print(name, t, tl, sep=" | ")

if fails:
    print("\n".join("FAIL " + f for f in fails))
    print(f"KIT_CHECK: FAIL ({len(fails)})")
    sys.exit(1)
print("KIT_CHECK: PASS")
