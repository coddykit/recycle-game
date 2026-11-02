"""Static per-tier draw / primitive budget of the Volt City art (stdlib; ART section 3, games/CLAUDE.md section 10).
Unity is not installed yet, so this is the P2 perf line computed from the manifest + layout exactly as the game builds
the scene (one draw per group below); the runtime perf gate replaces it once the Unity project exists (P3).
    python3 blender/perf_budget.py            -> PERF_BUDGET <tier> <scene> draws=.. prims=.. PASS|FAIL
Draw groups (Low, whole city visible): terrain + sky dome (one merged mesh) 1 · districts 8 · plants (built set merged)
1 · grid (pylons + lines + substations merged) 1 · wind rotors (instanced) 1 · trucks 1 · cars 1 · fault markers 1 ·
trees 1 = 16. Medium: + shadow casters (districts, plants, grid, instanced props) — directional shadow, 1 cascade map
pass per caster group; trees / cars at full count."""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
M = json.loads((ROOT / "unity/Assets/Art/Models/manifest.json").read_text())["models"]
LAY = json.loads((ROOT / "unity/Assets/Art/Models/layout.json").read_text())
T = {k: v["triangles"] for k, v in M.items()}
LIMITS = {"low": (17, 84000), "medium": (40, 160000)}
SCENES = {
    # start: Ch.1 day 1 (Old Smoky L1, 1 crew truck, 1 fault); late: everything built at level 3, all crews, 8 faults
    "start": {"plants": {"steam": 1}, "trucks": 1, "markers": 1, "smart": 0},
    "late": {"plants": {t: 3 for t in LAY["plant_types"]}, "trucks": 5, "markers": 8, "smart": 3},
}


def scene(tier, s):
    low = tier == "low"
    trees = len(LAY["trees"]) // 2 if low else len(LAY["trees"])
    cars = 20 if low else 40
    wind = 8 * s["plants"].get("wind", 0)
    groups = {
        "terrain+sky": T["terrain_city"] + T["sky_dome"],
        **{f"district_{d['id']}": T["district_" + d["id"]] for d in LAY["districts"]},
        "plants": sum(T[f"plant_{t}_l{l}"] for t, l in s["plants"].items()),
        "grid": T["grid_lines"] + (10 - s["smart"]) * T["substation"] + s["smart"] * T["substation_smart"],
        "wind_rotors": wind * T["wind_blades"],
        "trucks": s["trucks"] * T["truck_crew"],
        "cars": cars * T["car"],
        "markers": s["markers"] * T["fault_marker"],
        "trees": trees * T["tree"],
    }
    groups = {k: v for k, v in groups.items() if v > 0}
    draws = len(groups)
    prims = sum(groups.values())
    if not low:
        casters = [k for k in groups if k not in ("terrain+sky",)]
        draws += len(casters)
        prims += sum(groups[k] for k in casters)
    return draws, prims, groups


def main():
    bad = 0
    for tier in ("low", "medium"):
        for name, s in SCENES.items():
            d, p, _g = scene(tier, s)
            md, mp = LIMITS[tier]
            ok = d <= md and p <= mp
            bad += not ok
            print(f"PERF_BUDGET {tier} {name} draws={d} prims={p} (limit {md} / {mp}) {'PASS' if ok else 'FAIL'}")
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
