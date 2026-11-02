"""Volt City city plan (P2): ONE fictional coastal city, authored in Godot city coordinates (X east, Y up, +Z south =
towards the portrait camera, metres). Shared by every builder in build_art.py and written to
assets/models/layout.json (district boxes for the camera fit, substations, plant sites, corridor paths, road graph,
tree / car / truck spawn data) so the game (P3) places nothing by hand.

Map (top of the screen = north = -Z):
    hills + wind ridge (NW)          hydro reservoir + dam (NE, the river's source)
    Hilltop        Hospital Hill                      ~river~
         Market Square                 Tech Park      peaker
    sea | Riverside   ~river~   battery park
    Old Harbor (river mouth)            Stadium Row
    Old Smoky (coast)          Airport Flats      solar field
"""
import math

WATER_Y = 0.0          # sea / river level; the terrain shader paints water below it

# id, centre (x, z), half size (hx, hz), pad height, substation (x, z) in city space, window-style mix
DISTRICTS = [
    ("old_harbor", (-160, 150), (62, 52), 1.2, (-128, 112)),
    ("riverside", (-55, 30), (56, 46), 1.6, (-30, 20)),
    ("hilltop", (-150, -205), (56, 46), 16.0, (-122, -180)),
    ("market_square", (-20, -95), (56, 50), 2.6, (6, -72)),
    ("tech_park", (172, -85), (58, 52), 3.2, (140, -60)),
    ("stadium_row", (138, 136), (60, 56), 1.6, (104, 110)),
    ("hospital_hill", (38, -232), (52, 44), 12.0, (62, -206)),
    ("airport_flats", (96, 288), (92, 48), 1.2, (54, 262)),
]
DISTRICT_IDS = [d[0] for d in DISTRICTS]

# hub substations (plants feed the network through them)
HUBS = [("west_hub", (-150, 236), 1.4), ("east_hub", (232, 26), 2.6)]

# plant sites: type -> (centre x, z, pad y, yaw degrees)
PLANTS = {
    "steam": (-176, 290, 1.4, 0.0),
    "peaker": (262, -10, 2.6, 0.0),
    "hydro": (156, -300, 0.0, 0.0),
    "wind": (-150, -318, 0.0, 0.0),
    "solar": (262, 232, 1.2, 0.0),
    "battery": (84, 34, 1.6, 0.0),
}
PLANT_TYPES = ["steam", "peaker", "hydro", "wind", "solar", "battery"]

RIVER = [(156, -286), (140, -230), (122, -180), (98, -122), (82, -62), (64, -8), (34, 40), (-6, 82), (-56, 98),
         (-110, 92), (-160, 84), (-210, 82), (-262, 86), (-320, 92)]
RIVER_HALF = 13.0

# reservoir behind the dam (a polygon in the valley north of the dam line z = -300)
DAM = ((112, -298), (200, -298))
RESERVOIR = [(108, -304), (204, -304), (226, -350), (214, -410), (170, -440), (120, -420), (96, -370)]
RESERVOIR_Y = 9.0      # full reservoir surface (the shader moves it 0..-6 m with the level uniform)


def coast_x(z):
    """Sea west of this x (the coastline wiggles; the harbour basin is cut in by harbour_basin())."""
    return -238 + 10 * math.sin(z / 47.0) + 6 * math.sin(z / 19.0 + 1.3)


def _smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def _seg_dist(px, pz, a, b):
    ax, az = a
    bx, bz = b
    dx, dz = bx - ax, bz - az
    L2 = dx * dx + dz * dz
    t = 0.0 if L2 == 0 else max(0.0, min(1.0, ((px - ax) * dx + (pz - az) * dz) / L2))
    cx, cz = ax + dx * t, az + dz * t
    return math.hypot(px - cx, pz - cz), t


def river_dist(x, z):
    best = 1e9
    for a, b in zip(RIVER, RIVER[1:]):
        d, _ = _seg_dist(x, z, a, b)
        best = min(best, d)
    return best


def in_poly(x, z, poly):
    inside = False
    n = len(poly)
    for i in range(n):
        x1, z1 = poly[i]
        x2, z2 = poly[(i + 1) % n]
        if (z1 > z) != (z2 > z):
            xi = x1 + (z - z1) * (x2 - x1) / (z2 - z1)
            if x < xi:
                inside = not inside
    return inside


def poly_dist(x, z, poly):
    best = 1e9
    for i in range(len(poly)):
        d, _ = _seg_dist(x, z, poly[i], poly[(i + 1) % len(poly)])
        best = min(best, d)
    return -best if in_poly(x, z, poly) else best


def _noise(x, z):
    return (math.sin(x * .021 + 1.7) * math.cos(z * .017 - .4) + .5 * math.sin(x * .047 - z * .039 + 2.1)
            + .25 * math.sin(x * .11 + z * .093))


def pads():
    """Flat building pads: (x0, z0, x1, z1, y, feather m)."""
    out = []
    for _id, (cx, cz), (hx, hz), y, _s in DISTRICTS:
        out.append((cx - hx - 4, cz - hz - 4, cx + hx + 4, cz + hz + 4, y, 26.0 if y > 5 else 14.0))
    for _id, (x, z), y in HUBS:
        out.append((x - 14, z - 12, x + 14, z + 12, y, 10.0))
    for t, (x, z, y, _yaw) in PLANTS.items():
        if t in ("hydro", "wind"):
            continue
        hx, hz = {"steam": (52, 40), "peaker": (34, 30), "solar": (58, 50), "battery": (24, 18)}[t]
        out.append((x - hx, z - hz, x + hx, z + hz, y, 12.0))
    return out


_PADS = None


def natural_height(x, z):
    """Land without pads: rolling plain, northern hills, the wind ridge, the hydro valley, a south-east farm plain."""
    h = 3.0 + 1.1 * _noise(x, z)
    # northern hills rise behind z = -170
    north = _smooth((-170 - z) / 220.0)
    h += north * (46 + 22 * _noise(x * .6 + 40, z * .6))
    # wind ridge NW (a long crest at z ~ -318)
    ridge = math.exp(-((z + 318) / 34.0) ** 2) * _smooth((x + 290) / 60.0) * _smooth((-20 - x) / 60.0)
    h += ridge * 26
    # Hilltop / Hospital Hill knolls
    h += 14 * math.exp(-(((x + 150) / 90.0) ** 2 + ((z + 205) / 80.0) ** 2))
    h += 9 * math.exp(-(((x - 38) / 70.0) ** 2 + ((z + 232) / 60.0) ** 2))
    # hydro valley: carve towards the reservoir floor between the ridges east of the river source
    pd = poly_dist(x, z, RESERVOIR)
    h = h * _smooth((pd + 4) / 46.0) + (RESERVOIR_Y - 9.0) * (1 - _smooth((pd + 4) / 46.0))
    # the valley walls stand up behind the dam (so the dam spans between two shoulders)
    if -330 < z < -280:
        sh = _smooth((abs(x - 156) - 46) / 14.0)
        h = max(h, (RESERVOIR_Y + 6) * sh)
    # the sea: everything west of the coast falls below the water
    cx = coast_x(z)
    sea = _smooth((cx + 6 - x) / 28.0)
    h = h * (1 - sea) - 7.0 * sea
    # south / south-west: open sea beyond z ~ 380 west of x = 60 (a bay in front of the harbour)
    bay = _smooth((z - 360) / 50.0) * _smooth((40 - x) / 120.0)
    h = h * (1 - bay) - 7.0 * bay
    return h


def height(x, z):
    """Final terrain height: natural land, flattened under the pads, the river carved through."""
    global _PADS
    if _PADS is None:
        _PADS = pads()
    h = natural_height(x, z)
    for x0, z0, x1, z1, y, fe in _PADS:
        dx = max(x0 - x, 0.0, x - x1)
        dz = max(z0 - z, 0.0, z - z1)
        d = math.hypot(dx, dz)
        if d < fe:
            w = 1 - _smooth(d / fe)
            h = h * (1 - w) + y * w
    rd = river_dist(x, z)
    if rd < RIVER_HALF + 16:
        w = 1 - _smooth((rd - RIVER_HALF + 3) / 16.0)
        h = h * (1 - w) + (WATER_Y - 2.4) * w
    # harbour basin west of Old Harbor's quay
    if -246 < x < -214 and 104 < z < 196:
        w = _smooth((x + 214) / -6.0) * _smooth(min(z - 104, 196 - z) / 8.0)
        h = h * (1 - w) - 5.0 * w
    return h


# ------------------------------------------------------------------------------------------------ grid corridors
# 16 corridor indices (the shader's corridor_state[16]): 0-7 = the 8 upgradeable city corridors (DESIGN section 6),
# 8-15 = plant ties + the two loop closers. P3 maps the sim's line ids onto these indices (ART-IDS.md).
SUB = {d[0]: d[4] for d in DISTRICTS}
SUB.update({h[0]: h[1] for h in HUBS})
CORRIDORS = [
    ("harbor_line", "west_hub", "old_harbor", []),
    ("river_line", "old_harbor", "riverside", [(-92, 70)]),
    ("market_line", "riverside", "market_square", [(-18, -26)]),
    ("hill_line", "market_square", "hilltop", [(-70, -140)]),
    ("hospital_line", "hilltop", "hospital_hill", [(-40, -214)]),
    ("tech_line", "market_square", "tech_park", [(70, -86)]),
    ("stadium_line", "riverside", "stadium_row", [(40, 66)]),
    ("airport_line", "stadium_row", "airport_flats", [(92, 196)]),
    ("steam_tie", "steam", "west_hub", []),
    ("peaker_tie", "peaker", "east_hub", []),
    ("hydro_tie", "hydro", "hospital_hill", [(110, -262)]),
    ("wind_tie", "wind", "hilltop", [(-140, -262)]),
    ("solar_tie", "solar", "airport_flats", [(190, 262)]),
    ("battery_tie", "battery", "east_hub", [(160, 30)]),
    ("east_loop", "east_hub", "tech_park", [(206, -20)]),
    ("west_loop", "west_hub", "stadium_row", [(-40, 214), (40, 170)]),
]
PLANT_TIE_POINT = {"steam": (-150, 262), "peaker": (240, -4), "hydro": (150, -278), "wind": (-150, -292),
                   "solar": (232, 236), "battery": (98, 34)}


def corridor_points(c):
    _id, a, b, via = c
    pa = PLANT_TIE_POINT.get(a) or SUB[a]
    pb = PLANT_TIE_POINT.get(b) or SUB[b]
    return [pa] + list(via) + [pb]


# ------------------------------------------------------------------------------------------------ roads (terrain)
# Main roads between districts (the vehicles' road graph is built from these + the district streets).
ROADS = [
    ("coast_road", [(-200, 300), (-196, 220), (-190, 150), (-188, 60), (-176, -40), (-168, -130), (-160, -170)]),
    ("harbor_bridge", [(-190, 150), (-120, 150), (-80, 120), (-60, 76), (-55, 30)]),
    ("river_road", [(-55, 30), (-30, -20), (-20, -95)]),
    ("hill_road", [(-20, -95), (-70, -130), (-120, -168), (-150, -205)]),
    ("hospital_road", [(-20, -95), (2, -150), (24, -196), (38, -232)]),
    ("tech_bridge", [(-20, -95), (40, -94), (100, -90), (172, -85)]),
    ("east_road", [(172, -85), (206, -30), (220, 40), (190, 100), (138, 136)]),
    ("stadium_bridge", [(-55, 30), (0, 46), (40, 72), (90, 110), (138, 136)]),
    ("airport_road", [(138, 136), (120, 200), (96, 288)]),
    ("south_road", [(-196, 220), (-110, 238), (-20, 262), (40, 280), (96, 288)]),
    ("solar_road", [(96, 288), (180, 270), (262, 232)]),
    ("peaker_road", [(220, 40), (262, -10)]),
    ("dam_road", [(172, -85), (160, -170), (150, -250), (130, -292)]),
]
ROAD_HALF = 4.5
