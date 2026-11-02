"""Shared 8x8 colour palette for Borderline Empire.

Every 3D asset is UV-mapped onto a single tiny palette texture so the whole
world can be rendered with one shared material (cheap on mobile GPUs).

Cells are addressed as (column, row) where row 0 is the TOP row of the PNG.
Row 7 is treated as emissive by res://assets/shaders/palette.gdshader.
Cell (7, 4) is the "PAINT" cell which the shader replaces with a per-material
paint colour (used for player vehicle liveries).
"""
import os
import struct
import zlib

GRID = 8
CELL_PX = 4

ROWS = [
    # row 0: neutrals
    ["white", "F4F1EA"], ["lightgray", "C9CCD1"], ["gray", "8C929C"], ["darkgray", "4A505A"],
    ["charcoal", "2B2F36"], ["black", "17191D"], ["tire", "23252A"], ["chrome", "DDE3EA"],
    # row 1: reds / oranges / browns
    ["red", "E5484D"], ["darkred", "A8323A"], ["orange", "F28C28"], ["lightorange", "F7B267"],
    ["peach", "F4D1AE"], ["brown", "8B5A3C"], ["darkbrown", "5C3A28"], ["terracotta", "C4693D"],
    # row 2: yellows / greens
    ["yellow", "FFD23F"], ["mustard", "D9A520"], ["lime", "A7D46F"], ["green", "5DBB63"],
    ["darkgreen", "3E8E4E"], ["forest", "2F6B3F"], ["olive", "7A8B3A"], ["mint", "9FE2BF"],
    # row 3: blues
    ["sky", "8EC9F0"], ["blue", "3D8BD9"], ["darkblue", "2A5B9E"], ["navy", "1F3A63"],
    ["teal", "2BB3A8"], ["cyan", "5FD8E8"], ["glass", "9CCFEA"], ["ice", "DDF1FA"],
    # row 4: purples / pinks (+ PAINT cell at col 7)
    ["purple", "8E5CD9"], ["darkpurple", "5B3A99"], ["pink", "F06EAA"], ["magenta", "D6338C"],
    ["lavender", "C7B3F0"], ["rose", "F7A8B8"], ["plum", "6B2F5B"], ["paint", "E05A47"],
    # row 5: sand / earth / wood
    ["sand", "E8C47A"], ["lightsand", "F3DDA5"], ["desertrock", "C98F5A"], ["redrock", "B8573A"],
    ["clay", "D9825B"], ["khaki", "BFA77A"], ["wood", "A0703F"], ["darkwood", "6E4A2A"],
    # row 6: building materials
    ["concrete", "B5B0A6"], ["asphalt", "3E4148"], ["marking", "F2F2F2"], ["roofred", "C2453D"],
    ["roofblue", "3F6FB5"], ["roofgreen", "4F8A5B"], ["gold", "F2C14E"], ["steel", "7D8794"],
    # row 7: emissive
    ["neonpink", "FF4FD8"], ["neoncyan", "3CF2FF"], ["neonyellow", "FFE45C"], ["neongreen", "5CFF8A"],
    ["lamp", "FFE7A3"], ["taillight", "FF3B3B"], ["neonpurple", "B36BFF"], ["neonorange", "FF9A3C"],
]

COLORS = {}
CELLS = {}
for i, (name, hexv) in enumerate(ROWS):
    col, row = i % GRID, i // GRID
    COLORS[name] = tuple(int(hexv[j:j + 2], 16) for j in (0, 2, 4))
    CELLS[name] = (col, row)


def uv_for(name):
    """Blender UV (origin bottom-left) for the centre of a palette cell."""
    col, row = CELLS[name]
    return ((col + 0.5) / GRID, 1.0 - (row + 0.5) / GRID)


def write_png(path):
    size = GRID * CELL_PX
    raw = bytearray()
    for y in range(size):
        raw.append(0)
        row = y // CELL_PX
        for x in range(size):
            col = x // CELL_PX
            name = ROWS[row * GRID + col][0]
            raw.extend(COLORS[name])
    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        return c + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    png += chunk(b"IEND", b"")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(png)


if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    out = os.path.join(here, "..", "assets", "textures", "palette.png")
    write_png(out)
    print("wrote", os.path.normpath(out))
