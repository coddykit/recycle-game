#!/usr/bin/env python3
"""Volt City road graph (P3, P1 open item #2): rebuilds Assets/Resources/data/roads.json from P2's art layout
(Assets/Art/Models/layout.json) so crew travel times in the sim are the distances the trucks really drive in the diorama.

  python3 games/volt-city/unity/Tools/road_graph.py           # write roads.json
  python3 games/volt-city/unity/Tools/road_graph.py --check   # exit 1 when roads.json is stale

Graph = the 13 art roads (centre polylines, Unity metres: x east, z north), simplified (Douglas-Peucker 3 m, junctions
kept), road ends snapped onto the road they touch (<= 30 m link), plus the sim's named nodes inserted ON the road
(projection of the target point onto the nearest road segment):
  d_<x>  district transformer = nearest road point to the district substation (layout districts[].substation)
  l_<x>  line fault site      = nearest road point to the midpoint of the district's art corridor (SIM-API section 3)
  depot  crew depot           = the Riverside junction (-55, -30) where harbor_bridge / river_road / stadium_bridge meet
Node value = [x, z, y]: the sim reads only [0], [1] (plan metres; edge length = straight distance between consecutive
nodes, which follows the simplified polyline), the diorama uses y for the truck height. Deterministic output.
"""
import json
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
UNITY = HERE.parent
LAYOUT = UNITY / "Assets/Art/Models/layout.json"
ROADS = UNITY / "Assets/Resources/data/roads.json"
LINES = UNITY / "Assets/Resources/data/lines.json"
DISTRICTS = UNITY / "Assets/Resources/data/districts.json"

# sim corridor id -> art corridor index (SIM-API section 3 / ART-IDS corridors 0-7)
SIM_TO_ART = {"c_harbor": 0, "c_riverside": 1, "c_market": 2, "c_hilltop": 3, "c_hospital": 4, "c_tech": 5,
              "c_stadium": 6, "c_airport": 7}
DEPOT_AT = (-55.0, -30.0)
SIMPLIFY_M = 3.0
SNAP_M = 30.0


def dist2(a, b):
    return (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2


def seg_project(p, a, b):
    ax, az = a[0], a[1]
    dx, dz = b[0] - ax, b[1] - az
    L = dx * dx + dz * dz
    t = 0.0 if L == 0 else max(0.0, min(1.0, ((p[0] - ax) * dx + (p[1] - az) * dz) / L))
    return t, (ax + dx * t, az + dz * t, a[2] + (b[2] - a[2]) * t)


def dp(points, keep, tol):
    """Douglas-Peucker on xz; indices in keep are never dropped."""
    n = len(points)
    mark = [False] * n
    mark[0] = mark[-1] = True
    for k in keep:
        mark[k] = True
    idx = [i for i in range(n) if mark[i]]
    stack = list(zip(idx, idx[1:]))
    while stack:
        i, j = stack.pop()
        best, bi = -1.0, -1
        for k in range(i + 1, j):
            _, q = seg_project(points[k], points[i], points[j])
            d = math.sqrt(dist2(points[k], q))
            if d > best:
                best, bi = d, k
        if bi >= 0 and best > tol:
            mark[bi] = True
            stack.append((i, bi))
            stack.append((bi, j))
    return [i for i in range(n) if mark[i]]


def build():
    lay = json.loads(LAYOUT.read_text())
    lines = json.loads(LINES.read_text())
    dists = json.loads(DISTRICTS.read_text())
    roads = []
    for r in sorted(lay["roads"], key=lambda r: r["id"]):
        pts = [(float(p[0]), float(p[2]), float(p[1])) for p in r["centre"]]   # (x, z, y)
        roads.append({"id": r["id"], "pts": pts, "keep": set(), "extra": []})

    # targets to insert on the road network: name -> (x, z)
    targets = {"depot": DEPOT_AT}
    subs = {d["id"]: d["substation"] for d in lay["districts"]}
    art_corr = {c["index"]: c for c in lay["corridors"]}
    dsite = {d["id"]: d["site"] for d in dists["districts"]}
    for d in dists["districts"]:
        s = subs[d["id"]]
        targets[d["site"]] = (float(s[0]), float(s[2]))
    for c in lines["corridors"]:
        pts = art_corr[SIM_TO_ART[c["id"]]]["points"]
        # midpoint by arc length of the art corridor
        segs = [math.dist(pts[i], pts[i + 1]) for i in range(len(pts) - 1)]
        half, acc = sum(segs) / 2.0, 0.0
        mid = pts[0]
        for i, L in enumerate(segs):
            if acc + L >= half:
                t = (half - acc) / L if L else 0.0
                mid = (pts[i][0] + (pts[i + 1][0] - pts[i][0]) * t, pts[i][1] + (pts[i + 1][1] - pts[i][1]) * t)
                break
            acc += L
        targets[c["site"]] = (float(mid[0]), float(mid[1]))
    _ = dsite

    # 1) junctions: every road end snaps to the nearest point of another road (vertex when within 1 m)
    links = []   # (road a, index a, road b, index b) after insertion
    def insert_point(road, p):
        """Insert projection point p onto road; returns its vertex index (existing vertex if within 1 m)."""
        best = None
        for i in range(len(road["pts"]) - 1):
            t, q = seg_project(p, road["pts"][i], road["pts"][i + 1])
            d = dist2(p, q)
            if best is None or d < best[0]:
                best = (d, i, t, q)
        d, i, t, q = best
        for k in (i, i + 1):
            if dist2(road["pts"][k], q) <= 1.0:
                road["keep"].add(k)
                return k
        road["pts"].insert(i + 1, q)
        road["keep"] = {k + 1 if k > i else k for k in road["keep"]}
        road["keep"].add(i + 1)
        return i + 1

    def nearest_road(p, exclude=None):
        best = None
        for r in roads:
            if r is exclude:
                continue
            for i in range(len(r["pts"]) - 1):
                _, q = seg_project(p, r["pts"][i], r["pts"][i + 1])
                d = dist2(p, q)
                if best is None or d < best[0]:
                    best = (d, r)
        return best

    ends = []
    for r in roads:
        for e in (0, -1):
            ends.append((r, e))
    for r, e in ends:
        p = r["pts"][e]
        d2, other = nearest_road(p, exclude=r)
        if d2 <= SNAP_M * SNAP_M:
            k = insert_point(other, p)
            ei = 0 if e == 0 else len(r["pts"]) - 1
            r["keep"].add(ei)
            links.append((r, "end0" if e == 0 else "end1", other, other["pts"][k]))

    # 2) named nodes on the network
    named = {}
    for name in sorted(targets):
        p = targets[name]
        d2, r = nearest_road((p[0], p[1], 0.0))
        k = insert_point(r, (p[0], p[1], 0.0))
        named[name] = (r, r["pts"][k])

    # 3) simplify, name vertices, edges
    def key(pt):
        return (round(pt[0], 3), round(pt[1], 3))

    node_of = {}        # key -> node name
    nodes = {}          # name -> [x, z, y]
    for name, (r, pt) in named.items():
        node_of[key(pt)] = name
        nodes[name] = [round(pt[0], 2), round(pt[1], 2), round(pt[2], 2)]
    edges = set()
    jn = 0
    for r in roads:
        keepi = dp(r["pts"], [i for i in range(len(r["pts"])) if i in r["keep"] or key(r["pts"][i]) in node_of], SIMPLIFY_M)
        prev = None
        for n, i in enumerate(keepi):
            pt = r["pts"][i]
            k = key(pt)
            if k not in node_of:
                nm = f"{r['id']}_{n:02d}"
                node_of[k] = nm
                nodes[nm] = [round(pt[0], 2), round(pt[1], 2), round(pt[2], 2)]
            nm = node_of[k]
            if prev is not None and prev != nm:
                edges.add(tuple(sorted((prev, nm))))
            prev = nm
    for r, which, other, pt in links:
        a = node_of[key(r["pts"][0] if which == "end0" else r["pts"][-1])]
        b = node_of[key(pt)]
        if a != b:
            edges.add(tuple(sorted((a, b))))
    # merge exact duplicates: two road ends at the same point already share the key
    out = {
        "_doc": ("Road graph for crew trucks, GENERATED by unity/Tools/road_graph.py from the P2 art layout (Unity metres, "
                 "node = [x, z, y]; the sim uses [x, z], the diorama y). Travel time = shortest path / truck speed "
                 "(balance.crew.speed_mps x crew speed upgrades). d_<x> = district transformer, l_<x> = line fault site "
                 "(middle of the art corridor), depot = crew depot. Do not edit by hand: run the tool."),
        "depot": "depot",
        "nodes": {k: nodes[k] for k in sorted(nodes)},
        "edges": [list(e) for e in sorted(edges)],
    }
    return out


def main():
    out = build()
    text = json.dumps(out, indent=1, ensure_ascii=False) + "\n"
    if "--check" in sys.argv:
        ok = ROADS.read_text() == text
        print("ROAD_GRAPH:", "fresh" if ok else "STALE (run Tools/road_graph.py)")
        return 0 if ok else 1
    ROADS.write_text(text)
    # summary: depot -> every named node, metres + seconds at 40 m/s
    import heapq
    adj = {}
    for a, b in out["edges"]:
        d = math.hypot(out["nodes"][a][0] - out["nodes"][b][0], out["nodes"][a][1] - out["nodes"][b][1])
        adj.setdefault(a, []).append((b, round(d)))
        adj.setdefault(b, []).append((a, round(d)))
    dist = {"depot": 0}
    pq = [(0, "depot")]
    while pq:
        d, u = heapq.heappop(pq)
        if d > dist[u]:
            continue
        for v, w in adj.get(u, []):
            if d + w < dist.get(v, 1e18):
                dist[v] = d + w
                heapq.heappush(pq, (d + w, v))
    print(f"ROAD_GRAPH: {len(out['nodes'])} nodes, {len(out['edges'])} edges -> {ROADS.relative_to(UNITY)}")
    for k in sorted(out["nodes"]):
        if k.startswith(("d_", "l_")):
            print(f"  depot -> {k:12s} {dist.get(k, -1):5d} m  {dist.get(k, 0) / 40.0:5.1f} s")
    missing = [k for k in out["nodes"] if k not in dist]
    if missing:
        print("UNREACHABLE:", missing)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
