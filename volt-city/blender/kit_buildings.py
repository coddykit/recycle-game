"""Volt City modular building kit (P2): every piece is a builder that writes into a district Model (never placed one by
one at runtime; build_art.py assembles them into the 8 merged district meshes). Godot city space, metres.

Window walls use the "w_<colour>" keys (code 0.4): the shared shader paints windows, floors and the light ripple from
world position + the data UVs, so a facade costs 2 triangles whatever its window count. Window wall data:
UV0.y = per-building seed, UV1 = (style, base height m) (see STYLE; vckit module doc).
"""
import math

# window styles (floor(v / 1000) in the shader; assets/art/vc_world.gdshaderinc window())
STYLE = {"none": 0, "home": 1, "office": 2, "curtain": 3, "ware": 4, "shop": 5, "data": 6, "clinic": 7, "villa": 8,
         "terminal": 9, "stadium": 10}

# kit piece triangle counts are measured by build_art.py (manifest "kit" section)
KIT = []


def piece(fn):
    KIT.append(fn.__name__)
    return fn


def wdata(style, base, seed):
    """Model.data() kwargs of a window wall."""
    return {"v": min(.999, max(0.0, seed % 1.0)), "w": STYLE[style], "z": round(base, 2)}


@piece
def block(m, x0, z0, x1, z1, base, h, wall, roof, style, seed, units=0):
    """Flat-roofed block: four window walls + roof (+ rooftop units)."""
    with m.data(**wdata(style, base, seed)):
        m.boxr(x0, base, z0, x1, base + h, z1, "w_" + wall, skip="tb")
    m.poly([(x0, base + h, z1), (x1, base + h, z1), (x1, base + h, z0), (x0, base + h, z0)], roof)
    w, d = x1 - x0, z1 - z0
    for i in range(units):
        ux = x0 + w * (.25 + .5 * ((seed * 7.3 + i * .37) % 1.0))
        uz = z0 + d * (.25 + .5 * ((seed * 3.1 + i * .61) % 1.0))
        s = min(w, d) * .16
        m.boxr(ux - s, base + h, uz - s * .7, ux + s, base + h + 1.6, uz + s * .7, "lgrey", skip="b")


@piece
def gable_house(m, x0, z0, x1, z1, base, h, wall, roof, style, seed, ridge_x=True):
    """House with a pitched roof (ridge along X when ridge_x)."""
    with m.data(**wdata(style, base, seed)):
        m.boxr(x0, base, z0, x1, base + h, z1, "w_" + wall, skip="tb")
    rh = min(x1 - x0, z1 - z0) * .42
    top = base + h
    if (seed * 13.7) % 1.0 < .55:      # chimney
        cx = x0 + (x1 - x0) * (.25 + .5 * ((seed * 5.3) % 1.0))
        cz = z0 + (z1 - z0) * .3
        m.boxr(cx - .45, top, cz - .45, cx + .45, top + rh + 1.2, cz + .45, "brickD", skip="b")
    if ridge_x:
        zm = (z0 + z1) / 2
        m.prism([(z0 - .3, top), (z1 + .3, top), (zm, top + rh)], x0 - .3, x1 + .3, roof, plane="zy")
    else:
        xm = (x0 + x1) / 2
        m.prism([(x0 - .3, top), (x1 + .3, top), (xm, top + rh)], z0 - .3, z1 + .3, roof, plane="xy")


@piece
def row_houses(m, x0, z0, x1, z1, base, rnd, walls, roofs, style="home", along_x=True, floors=(2, 4)):
    """A terrace: 3-6 narrow houses with their own heights, colours and roofs."""
    L = (x1 - x0) if along_x else (z1 - z0)
    n = max(2, min(6, int(L / 7.5)))
    for i in range(n):
        a = i / n
        b = (i + 1) / n
        h = 3.4 * rnd.randint(*floors)
        wall = rnd.choice(walls)
        roof = rnd.choice(roofs)
        seed = rnd.random()
        if along_x:
            xa, xb = x0 + (x1 - x0) * a, x0 + (x1 - x0) * b
            gable_house(m, xa, z0, xb, z1, base, h, wall, roof, style, seed, ridge_x=False)
        else:
            za, zb = z0 + (z1 - z0) * a, z0 + (z1 - z0) * b
            gable_house(m, x0, za, x1, zb, base, h, wall, roof, style, seed, ridge_x=True)


@piece
def tower(m, cx, cz, w, d, base, h, wall, roof, style, seed, tiers=2, beacon=True):
    """Office tower with setbacks; aviation light on top."""
    y = base
    for t in range(tiers):
        f = 1.0 - .18 * t
        th = h * (.62 if t == 0 and tiers > 1 else (.38 / max(1, tiers - 1)))
        if tiers == 1:
            th = h
        block(m, cx - w * f / 2, cz - d * f / 2, cx + w * f / 2, cz + d * f / 2, y, th, wall, roof, style, seed)
        y += th
    m.boxr(cx - w * .18, y, cz - d * .18, cx + w * .18, y + 3.0, cz + d * .18, "dgrey", skip="b")
    if beacon:
        m.boxr(cx - .6, y + 3.0, cz - .6, cx + .6, y + 4.2, cz + .6, "l_red", skip="b")
    return y


@piece
def warehouse(m, x0, z0, x1, z1, base, h, wall, roof, seed, saw=True):
    """Harbour warehouse: high clerestory windows, sawtooth roof (or a flat one), a big roller door on the street side."""
    with m.data(**wdata("ware", base, seed)):
        m.boxr(x0, base, z0, x1, base + h, z1, "w_" + wall, skip="tb")
    top = base + h
    if saw:
        n = max(2, int((x1 - x0) / 7))
        for i in range(n):
            a = x0 + (x1 - x0) * i / n
            b = x0 + (x1 - x0) * (i + 1) / n
            m.prism([(a, top), (b, top), (b, top + 3.2)], z0, z1, roof, plane="xy")
    else:
        m.poly([(x0, top, z1), (x1, top, z1), (x1, top, z0), (x0, top, z0)], roof)
    door_w = min(6.0, (x1 - x0) * .3)
    xm = (x0 + x1) / 2
    m.poly([(xm - door_w / 2, base, z1 + .05), (xm + door_w / 2, base, z1 + .05),
            (xm + door_w / 2, base + 5.0, z1 + .05), (xm - door_w / 2, base + 5.0, z1 + .05)], "steelD")


@piece
def harbour_crane(m, x, z, base, yaw=0.0):
    """Generic harbour gantry crane (no real maker's shape): four legs, a portal beam and a boom over the water."""
    with m.at((x, base, z), ry=yaw):
        for sx in (-4.5, 4.5):
            for sz in (-4.0, 4.0):
                m.boxr(sx - .6, 0, sz - .6, sx + .6, 22, sz + .6, "orange", skip="b")
        m.boxr(-5.5, 20, -4.6, 5.5, 23.5, 4.6, "orange")
        m.boxr(-1.2, 22, -26, 1.2, 24, 14, "orange", skip="b")        # boom over the water (-Z local)
        m.boxr(-2.4, 23.5, -2.4, 2.4, 27, 2.4, "white")               # machinery house
        m.boxr(-.5, 27, -.5, .5, 28, .5, "l_red", skip="b")


@piece
def containers(m, x0, z0, rows, cols, base, rnd, stack=(1, 3)):
    keys = ["container1", "container2", "container3", "container4", "white"]
    for r in range(rows):
        for c in range(cols):
            n = rnd.randint(*stack)
            for k in range(n):
                x = x0 + c * 3.0
                z = z0 + r * 6.6
                m.boxr(x, base + k * 2.6, z, x + 2.5, base + (k + 1) * 2.6 - .05, z + 6.1, rnd.choice(keys),
                       skip="b" if k == 0 else "")


@piece
def data_hall(m, x0, z0, x1, z1, base, seed, h=9.0):
    """Data hall: long flat box with LED strip windows + a rooftop cooler grid (high night load = bright at night)."""
    block(m, x0, z0, x1, z1, base, h, "concreteL", "roofL", "data", seed)
    nx = max(2, int((x1 - x0) / 9))
    for i in range(nx):
        cx = x0 + (x1 - x0) * (i + .5) / nx
        m.boxr(cx - 2.6, base + h, z0 + 3, cx + 2.6, base + h + 1.8, z1 - 3, "galv", skip="b")
    m.boxr(x0 + 1, base + h + 1.8, z0 + 2.2, x1 - 1, base + h + 2.1, z0 + 2.6, "l_cyan", skip="b")


@piece
def stadium(m, cx, cz, base, rx=34.0, rz=26.0):
    """Generic oval stadium: outer wall ring with stadium windows, seating bowl, pitch, 4 floodlight masts."""
    n = 18
    with m.data(**wdata("stadium", base, .5)):
        m.lathe([(1.0, 0), (1.0, 16)], "w_concreteL", n=n, c=(cx, base, cz), sx=rx, sz=rz, cap_bottom=False, cap_top=False)
    # seating bowl: from the rim inwards and down (faces up-inwards)
    m.lathe([(rx * .62, 4), (rx * 1.0, 16)], "navy", n=n, c=(cx, base, cz), sx=1.0, sz=rz / rx,
            cap_bottom=False, cap_top=False, flip=True)
    m.ell((cx, base + 3.9, cz), (rx * .62, .1, rz * .62), "grass", n=n, rings=1, bottom=0.0, cap=False)
    for sx in (-1, 1):
        for sz in (-1, 1):
            px, pz = cx + sx * rx * .9, cz + sz * rz * .9
            m.boxr(px - .6, base, pz - .6, px + .6, base + 34, pz + .6, "galv", skip="b")
            m.boxr(px - 2.6, base + 34, pz - .8, px + 2.6, base + 37, pz + .8, "e_lamp")


@piece
def hospital(m, cx, cz, base, seed):
    """Generic hospital: a main block, two wings, an entrance canopy and a rooftop helipad (white H on a grey disc)."""
    block(m, cx - 22, cz - 9, cx + 22, cz + 9, base, 22, "plasterW", "roofL", "clinic", seed, units=2)
    block(m, cx - 30, cz - 20, cx - 18, cz + 20, base, 14, "plasterW", "roofL", "clinic", seed + .1)
    block(m, cx + 18, cz - 20, cx + 30, cz + 20, base, 14, "plasterW", "roofL", "clinic", seed + .2)
    m.boxr(cx - 7, base + 4, cz + 9, cx + 7, base + 4.6, cz + 15, "white")
    for sx in (-6, 6):
        m.boxr(cx + sx - .3, base, cz + 14.4, cx + sx + .3, base + 4, cz + 15, "lgrey", skip="b")
    m.cyl((cx, base + 22, cz), 7.0, .3, "grey", n=10)
    y = base + 22.32
    m.poly([(cx - 3, y, cz + 3.4), (cx - 1.8, y, cz + 3.4), (cx - 1.8, y, cz - 3.4), (cx - 3, y, cz - 3.4)], "white")
    m.poly([(cx + 1.8, y, cz + 3.4), (cx + 3, y, cz + 3.4), (cx + 3, y, cz - 3.4), (cx + 1.8, y, cz - 3.4)], "white")
    m.poly([(cx - 1.8, y, cz + .6), (cx + 1.8, y, cz + .6), (cx + 1.8, y, cz - .6), (cx - 1.8, y, cz - .6)], "white")
    m.boxr(cx + 24, base, cz + 22, cx + 30, base + 3.2, cz + 27, "green", skip="b")    # backup generator house


@piece
def terminal(m, x0, z0, x1, z1, base, seed):
    """Airport terminal: long low glass hall with a curved roof band + a control tower."""
    with m.data(**wdata("terminal", base, seed)):
        m.boxr(x0, base, z0, x1, base + 11, z1, "w_glass", skip="tb")
    m.poly([(x0 - 2, base + 11, z1 + 3), (x1 + 2, base + 11, z1 + 3), (x1 + 2, base + 12.5, z0), (x0 - 2, base + 12.5, z0)],
           "white")
    m.poly([(x0 - 2, base + 12.5, z0), (x1 + 2, base + 12.5, z0), (x1 + 2, base + 11, z0 - 3), (x0 - 2, base + 11, z0 - 3)],
           "lgrey")


@piece
def control_tower(m, x, z, base, seed):
    m.cyl((x, base, z), 2.6, 30, "concreteL", n=8, r2=2.2, cap_bottom=False)
    with m.data(**wdata("curtain", base + 30, seed)):
        m.cyl((x, base + 30, z), 4.6, 4.2, "w_glassD", n=8, r2=5.2, caps=False)
    m.cyl((x, base + 34.2, z), 5.4, .8, "white", n=8, cap_bottom=True)
    m.boxr(x - .4, base + 35, z - .4, x + .4, base + 39, z + .4, "galv", skip="b")
    m.boxr(x - .6, base + 39, z - .6, x + .6, base + 40, z + .6, "l_red", skip="b")


@piece
def hangar(m, x0, z0, x1, z1, base):
    """Barrel-roofed hangar, open to the apron (+Z)."""
    w = x1 - x0
    r = w / 2
    pts = [(x0 + r - r * math.cos(math.pi * i / 6), base + 6 + r * .55 * math.sin(math.pi * i / 6)) for i in range(7)]
    pts = [(x0, base)] + pts + [(x1, base)]
    m.prism(pts, z0, z1, "galv", plane="xy", skip="")
    m.poly([(x0 + 2, base, z1 + .05), (x1 - 2, base, z1 + .05), (x1 - 2, base + 7, z1 + .05), (x0 + 2, base + 7, z1 + .05)],
           "charcoal")


@piece
def market_hall(m, cx, cz, base, seed):
    """Covered market with a clock tower (the festival square's landmark)."""
    with m.data(**wdata("shop", base, seed)):
        m.boxr(cx - 14, base, cz - 9, cx + 14, base + 7, cz + 9, "w_sandstone", skip="tb")
    m.prism([(cz - 10, base + 7), (cz + 10, base + 7), (cz, base + 12)], cx - 15, cx + 15, "roofT", plane="zy")
    tx, tz = cx + 18, cz - 6
    with m.data(**wdata("none", base, seed)):
        m.boxr(tx - 2.6, base, tz - 2.6, tx + 2.6, base + 24, tz + 2.6, "sandstone", skip="tb")
    m.boxr(tx - 2.8, base + 19, tz + 2.6, tx + 2.8, base + 21.6, tz + 2.75, "l_warm")      # clock faces glow
    m.boxr(tx - 2.8, base + 19, tz - 2.75, tx + 2.8, base + 21.6, tz - 2.6, "l_warm")
    m.prism([(tx - 3, base + 24), (tx + 3, base + 24), (tx, base + 30)], tz - 3, tz + 3, "copper", plane="xy")


@piece
def villa(m, x, z, base, rnd):
    """Hillside villa: two offset volumes, a hipped roof, a terrace."""
    w = rnd.uniform(9, 13)
    d = rnd.uniform(8, 11)
    wall = rnd.choice(["plasterW", "plaster", "plasterY", "plasterP", "cream"])
    roof = rnd.choice(["roofR", "roofT", "roofG"])
    seed = rnd.random()
    gable_house(m, x - w / 2, z - d / 2, x + w / 2, z + d / 2, base, 6.4, wall, roof, "villa", seed, ridge_x=rnd.random() < .5)
    gable_house(m, x + w / 2 - 1, z - d / 2 + 2, x + w / 2 + 5, z + d / 2 - 1, base, 3.4, wall, roof, "villa", seed + .3,
                ridge_x=True)
    m.boxr(x - w / 2, base, z + d / 2, x + w / 2 - 2, base + .5, z + d / 2 + 4, "woodL", skip="b")


@piece
def lamp(m, x, z, base, h=6.0):
    """Street lamp: post + glowing head (code 0.35: lit with the district's ripple)."""
    m.boxr(x - .18, base, z - .18, x + .18, base + h, z + .18, "dgrey", skip="tb")
    m.boxr(x - .7, base + h, z - .45, x + .7, base + h + .45, z + .45, "e_lamp", skip="")


ROOFS_FLAT = ["roofL", "roofL", "roofK", "roofG", "roofB", "roofT"]


@piece
def apartment(m, x0, z0, x1, z1, base, rnd, floors=(4, 7), style="home"):
    h = 3.2 * rnd.randint(*floors)
    wall = rnd.choice(["plaster", "plasterY", "plasterB", "brickL", "concreteL", "cream", "plasterP", "brick"])
    roof = rnd.choice(ROOFS_FLAT)
    block(m, x0, z0, x1, z1, base, h, wall, roof, style, rnd.random(), units=2)
    # roof terrace / plant room in a second tone
    w, d = x1 - x0, z1 - z0
    if rnd.random() < .6:
        m.boxr(x0 + w * .55, base + h, z0 + d * .2, x1 - w * .08, base + h + 2.8, z0 + d * .6, "w_" + wall, skip="b")
    # balcony band on the street side
    m.boxr(x0 + .5, base + 3.2, z1, x1 - .5, base + 3.5, z1 + 1.2, "lgrey")


@piece
def shop_row(m, x0, z0, x1, z1, base, rnd, along_x=True):
    L = (x1 - x0) if along_x else (z1 - z0)
    n = max(2, min(5, int(L / 9)))
    for i in range(n):
        a, b = i / n, (i + 1) / n
        h = 3.6 * rnd.randint(2, 4)
        wall = rnd.choice(["plaster", "plasterY", "plasterP", "plasterG", "brickL", "stone", "cream"])
        if along_x:
            block(m, x0 + (x1 - x0) * a, z0, x0 + (x1 - x0) * b, z1, base, h, wall, rnd.choice(ROOFS_FLAT), "shop", rnd.random(), units=1)
        else:
            block(m, x0, z0 + (z1 - z0) * a, x1, z0 + (z1 - z0) * b, base, h, wall, rnd.choice(ROOFS_FLAT), "shop", rnd.random(), units=1)
        # shop awning on the street side (+Z)
        if along_x:
            xa, xb = x0 + (x1 - x0) * a + .6, x0 + (x1 - x0) * b - .6
            m.poly([(xa, base + 3.6, z1 + 1.6), (xb, base + 3.6, z1 + 1.6), (xb, base + 4.4, z1), (xa, base + 4.4, z1)],
                   rnd.choice(["red", "teal", "orange", "cream", "green", "blue"]))


@piece
def parking(m, x0, z0, x1, z1, base, rnd, fill=.7):
    """Parking lot: dark asphalt pad with white bays and parked cars (static boxes, two tones)."""
    m.poly([(x0, base + .05, z1), (x1, base + .05, z1), (x1, base + .05, z0), (x0, base + .05, z0)], "asphaltD")
    paints = ["white", "lgrey", "charcoal", "red", "blue", "teal", "sandstone", "green"]
    rows = max(1, int((z1 - z0) / 6.5))
    cols = max(1, int((x1 - x0) / 2.8))
    for r_ in range(rows):
        for c in range(cols):
            if rnd.random() > fill:
                continue
            x = x0 + 1.4 + c * 2.8
            z = z0 + 1.2 + r_ * 6.5
            k = rnd.choice(paints)
            m.boxr(x - .85, base + .05, z, x + .85, base + 1.25, z + 4.0, k, skip="b")
            m.boxr(x - .7, base + 1.25, z + 1.0, x + .7, base + 1.7, z + 3.0, "glassD", skip="b")


@piece
def villa_pool(m, x, z, base, w=6.0, d=3.4):
    m.boxr(x - w / 2 - .4, base, z - d / 2 - .4, x + w / 2 + .4, base + .3, z + d / 2 + .4, "white", skip="b")
    m.poly([(x - w / 2, base + .32, z + d / 2), (x + w / 2, base + .32, z + d / 2), (x + w / 2, base + .32, z - d / 2),
            (x - w / 2, base + .32, z - d / 2)], "pool")


@piece
def cargo_ship(m, x, z, base, length=70.0, yaw=0.0):
    """Generic cargo ship along the quay: hull, deck containers, a bridge house aft (no real line's livery)."""
    w = length * .16
    with m.at((x, base, z), ry=yaw):
        hull = [(-length / 2, 0), (length / 2 - 6, 0), (length / 2, 3.5), (length / 2 - 2, 6), (-length / 2, 6)]
        m.prism([(p[0], p[1] - 4) for p in hull], -w / 2, w / 2, "navy", plane="xy", side_key="red")
        m.boxr(-length / 2, 1.9, -w / 2 - .02, length / 2 - 4, 2.2, w / 2 + .02, "white", skip="tb")
        keys = ["container1", "container2", "container3", "container4", "white"]
        for i in range(7):
            cx = -length / 2 + 16 + i * 6.8
            for j in range(2):
                m.boxr(cx, 2, -w / 2 + 1 + j * (w / 2 - 1), cx + 6.2, 4.6 + (i + j) % 2 * 2.6, -1 + j * (w / 2 - 1) + 1 - .2,
                       keys[(i * 3 + j) % 5], skip="b")
        m.boxr(-length / 2 + 2, 2, -w / 2 + 1, -length / 2 + 11, 12, w / 2 - 1, "white", skip="b")
        m.boxr(-length / 2 + 2.2, 9.5, -w / 2 + .9, -length / 2 + 10.8, 11, w / 2 - .9, "glassD", skip="tb")
        m.boxr(-length / 2 + 5, 12, -.6, -length / 2 + 7, 15, .6, "orange", skip="b")


@piece
def airliner(m, x, z, base, yaw=0.0):
    """Generic twin-engine airliner silhouette parked on the apron (no real maker's shape or livery)."""
    with m.at((x, base, z), ry=yaw):
        m.tube([(0, 3.2, -18), (0, 3.4, -10), (0, 3.4, 12), (0, 3.6, 17)], [.6, 2.0, 2.0, .5], "white", n=6)
        m.prism([(-1, 0), (1.5, 0), (5, 0)][:0] or [(-15, -2), (15, -2), (2, 6), (-2, 6)], 2.4, 2.8, "lgrey", plane="xz")
        m.prism([(-5, -16), (5, -16), (.8, -12), (-.8, -12)], 4.4, 4.7, "lgrey", plane="xz")
        m.prism([(-15.5, 0), (-12.5, 0), (-12.5, 7)][:0] or [(-.25, 3.6), (.25, 3.6), (.25, 10), (-.25, 10)], -17, -12.5,
                "teal", plane="xy")
        for sx in (-6.5, 6.5):
            m.cyl((sx, 1.6, 4.5), .9, 3.4, "grey", n=6, rot=(90, 0, 0))
