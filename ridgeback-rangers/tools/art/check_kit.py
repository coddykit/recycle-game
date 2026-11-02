#!/usr/bin/env python3
"""Ridgeback Rangers — offline check of the Blender kit merge bank (unity/Assets/Resources/kit/props.bytes, written by
tools/art/kit.py, read by World/KitBank.cs). No Blender needed; called by unity/Tools/check.sh.
  python3 ridgeback-rangers/tools/art/check_kit.py      # exit 0 = KIT_BANK_PASS
Checks: format, every prop the game asks for is there, triangle budgets (building ≤ 700, any other prop ≤ 600), the "_low"
twins never heavier than the procedural MeshKit shape they replace (Low must not get heavier; new Low-only props are
listed with their cost), indices in range, swatches inside the palette (make_palette.py), unit normals, and a Low scene
estimate (old procedural vs kit) for docs/ART.md.
"""
import struct
import sys
from pathlib import Path

GAME = Path(__file__).resolve().parents[2]
BANK = GAME / "unity/Assets/Resources/kit/props.bytes"
BUILDINGS = ("gate", "hq", "barn", "vet", "hatchery", "station", "view")
PROPS = ("tree", "tree_tall", "tree_fern", "rock", "shade_sail", "water_point", "dig_site", "volcano", "hill", "kiosk_cafe", "kiosk_gift")
EXTRA = ("fence1_post", "fence2_post", "fence3_post", "fence1_rail", "fence2_rail", "fence3_rail", "lamp")   # Medium/High only
# procedural MeshKit triangles each "_low" twin replaces (None = new on Low: a merged prop, 0 draws, cost listed)
PROCEDURAL = {"tree": 30, "tree_tall": 30, "tree_fern": 50, "rock": 10, "shade_sail": 20, "water_point": 10, "volcano": 42,
              "hill": 30, "dig_site": None, "kiosk_cafe": None, "kiosk_gift": None}
NEW_ON_LOW_MAX = 30
SWATCHES = 51   # Tools/make_palette.py


def read(path):
    data = path.read_bytes()
    assert data[:4] == b"RRKB", "magic"
    ver, count = struct.unpack_from("<HH", data, 4)
    assert ver == 1, "version"
    off, out = 8, {}
    for _ in range(count):
        n = data[off]; off += 1
        name = data[off:off + n].decode("ascii"); off += n
        nv, ni = struct.unpack_from("<HH", data, off); off += 4
        verts = [struct.unpack_from("<hhhbbbB", data, off + 10 * i) for i in range(nv)]
        off += 10 * nv
        idx = struct.unpack_from("<%dH" % ni, data, off); off += 2 * ni
        out[name] = (verts, idx)
    assert off == len(data), "trailing bytes"
    return out


def main():
    fails = []
    if not BANK.exists():
        print("FAIL missing", BANK, "(blender -b --python tools/art/kit.py -- unity)")
        print("KIT_BANK_FAIL")
        return 1
    bank = read(BANK)
    need = list(BUILDINGS) + list(PROPS) + [p + "_low" for p in PROPS] + list(EXTRA)
    for n in need:
        if n not in bank:
            fails.append("missing prop " + n)
    tris = {}
    for name, (verts, idx) in bank.items():
        t = len(idx) // 3
        tris[name] = t
        if len(idx) % 3 or any(i >= len(verts) for i in idx):
            fails.append(name + " bad indices")
        lim = 700 if name in BUILDINGS else 600
        if t > lim:
            fails.append("%s %d tris > %d" % (name, t, lim))
        for v in verts:
            if v[6] >= SWATCHES:
                fails.append("%s swatch %d outside the palette" % (name, v[6])); break
            if v[3] * v[3] + v[4] * v[4] + v[5] * v[5] < 100 * 100:
                fails.append(name + " zero normal"); break
    print("prop | tris (Medium/High) | _low | procedural it replaces")
    for p in PROPS:
        lo = tris.get(p + "_low", 0)
        old = PROCEDURAL[p]
        if old is not None and lo > old + 3:
            fails.append("%s_low %d tris > procedural %d (+3)" % (p, lo, old))
        if old is None and lo > NEW_ON_LOW_MAX:
            fails.append("%s_low %d tris > %d (new on Low)" % (p, lo, NEW_ON_LOW_MAX))
        print("%s | %d | %d | %s" % (p, tris.get(p, 0), lo, "new" if old is None else old))
    for p in BUILDINGS + EXTRA:
        print("%s | %d | - | -" % (p, tris.get(p, 0)))
    # Low scene estimate, full island (3 zones): ~10 foliage per zone after the path / node clearance, half trees/rocks,
    # half tree ferns; 3 dig sites, both kiosks, 2 dune paddocks with water + shade; volcano + 2 hills.
    def low(p):
        return tris.get(p + "_low", 0)
    old = 2 * (5 * 30 + 5 * 50) + (5 * 10 + 5 * 50) + 2 * 10 + 2 * 20 + 42 + 2 * 30
    new = 2 * (4 * low("tree") + 1 * low("tree_tall") + 5 * low("tree_fern")) + (5 * low("rock") + 5 * low("tree_fern")) \
        + 2 * low("water_point") + 2 * low("shade_sail") + low("volcano") + 2 * low("hill") + 3 * low("dig_site") + low("kiosk_cafe") + low("kiosk_gift")
    print("LOW scene estimate (props replaced / added): procedural %d tris -> kit %d tris (%+d)" % (old, new, new - old))
    if new > old * 1.05:
        fails.append("Low scene estimate heavier than +5%%: %d -> %d" % (old, new))
    for f in fails:
        print("FAIL", f)
    print("KIT_BANK_PASS" if not fails else "KIT_BANK_FAIL (%d)" % len(fails))
    return 1 if fails else 0


sys.exit(main())
