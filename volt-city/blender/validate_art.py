"""Independent gate for the Volt City P2 art (stdlib only, no Blender, no Unity):
    python3 blender/validate_art.py   -> P2_ART_PASS ... (exit 1 on the first failed assertion)

Every model in unity/Assets/Art/Models/manifest.json has an FBX (binary FBX 7.x), a GLB and a sidecar JSON whose
bytes / sha256 match; GLB: one mesh, one primitive, one material (vc_world / vc_prop / vc_blade), COLOR_0 +
TEXCOORD_0 + TEXCOORD_1, no textures / cameras / lights / animations, triangles = manifest = sidecar <= budget;
names (files, GLB nodes / meshes / materials, the strings inside each FBX, palette keys) pass the denylist
(blender/ip_denylist_art.txt + tests/ip_denylist.txt when present; NFKD, leetspeak, separators folded, word-bounded,
~entry = substring), and a planted-name proof shows the checker bites. Also prints the static per-tier budget line
(blender/perf_budget.py)."""
import hashlib
import json
import re
import struct
import sys
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MODELS = ROOT / "unity/Assets/Art/Models"
LEET = {"0": "o", "3": "e", "4": "a", "@": "a", "$": "s", "5": "s", "7": "t", "1": "i"}
MATERIALS = {"vc_world", "vc_prop", "vc_blade"}


def normalize(text, keep_regex=False):
    s = unicodedata.normalize("NFKD", text.lower())
    s = "".join(LEET.get(ch, ch) for ch in s if not unicodedata.combining(ch))
    s = s.replace("&", "")
    return re.sub(r"[\s_\-/·]+" if keep_regex else r"[\s_.\-/\\·]+", " ", s)


def denylist():
    out = []
    files = [ROOT / "blender/ip_denylist_art.txt", ROOT / "tests/ip_denylist.txt"]
    for f in files:
        if not f.exists():
            continue
        for line in f.read_text().splitlines():
            e = line.strip()
            if not e or e.startswith("#"):
                continue
            if e.startswith("~"):
                out.append((e, None, normalize(e[1:]).strip()))
            else:
                out.append((e, re.compile(r"\b(?:" + re.escape(normalize(e, True).strip()).replace(r"\ ", r"\s?") + r")\b"), None))
    return out


def hits(name, entries):
    n = normalize(name)
    return [e for e, rx, sub in entries if (rx.search(n) if rx else sub in n.replace(" ", ""))]


def gltf_of(blob):
    assert struct.unpack_from("<4sII", blob) == (b"glTF", 2, len(blob))
    size, kind = struct.unpack_from("<II", blob, 12)
    assert kind == 0x4E4F534A
    return json.loads(blob[20:20 + size])


def fbx_strings(blob):
    assert blob.startswith(b"Kaydara FBX Binary  \x00"), "binary FBX"
    version = struct.unpack_from("<I", blob, 23)[0]
    assert version >= 7300, version
    return {m.decode() for m in re.findall(rb"[A-Za-z][A-Za-z0-9_ .:-]{3,}", blob)}


FBX_OK = re.compile(r"^(Kaydara|FBX|Model|Geometry|Material|Mesh|Layer|Properties|Lcl|Default|Global|Object|Blender|"
                    r"Creat|Document|Scene|Root|Ambient|Diffuse|Emissive|Specular|Shininess|Reflection|Transparen|"
                    r"Bump|Normal|Vertex|Polygon|Edges|UV|Color|Smoothing|Direct|IndexToDirect|ByPolygon|Time|"
                    r"Original|LastSaved|Up|Front|Coord|Unit|Custom|Current|Take|Connections|Definitions|Count|"
                    r"Template|References|Version|Header|Encryption|Shading|Culling|Lambert|Phong|Opacity|Display)")


def main():
    entries = denylist()
    assert entries, "denylist empty"
    manifest = json.loads((MODELS / "manifest.json").read_text())
    on_disk = {str(p.relative_to(ROOT)) for p in MODELS.rglob("*") if p.suffix in (".fbx", ".glb") or
               (p.suffix == ".json" and p.parent != MODELS)}
    listed = set()
    tris_total = 0
    size_total = 0
    for name, spec in manifest["models"].items():
        for kind in ("fbx", "glb", "sidecar"):
            f = spec[kind]
            listed.add(f["path"])
            blob = (ROOT / f["path"]).read_bytes()
            assert len(blob) == f["bytes"] and hashlib.sha256(blob).hexdigest() == f["sha256"], (name, kind, "sha256")
            size_total += len(blob)
        g = gltf_of((ROOT / spec["glb"]["path"]).read_bytes())
        for k in ("images", "textures", "cameras", "animations", "skins", "samplers"):
            assert not g.get(k), (name, k)
        assert "KHR_lights_punctual" not in g.get("extensionsUsed", []), name
        assert len(g["meshes"]) == 1 and len(g["meshes"][0]["primitives"]) == 1, (name, "one mesh, one primitive")
        mats = [m["name"] for m in g["materials"]]
        assert mats == [spec["material"]] and mats[0] in MATERIALS, (name, mats)
        prim = g["meshes"][0]["primitives"][0]
        assert {"POSITION", "NORMAL", "COLOR_0", "TEXCOORD_0", "TEXCOORD_1"} <= set(prim["attributes"]), (name, prim["attributes"])
        tris = g["accessors"][prim["indices"]]["count"] // 3
        side = json.loads((ROOT / spec["sidecar"]["path"]).read_text())
        assert tris == spec["triangles"] == side["triangles"] <= spec["budget"], (name, tris, spec["budget"])
        assert side["material"] == spec["material"] and sorted(side["palette"]) == sorted(spec["keys"]), name
        for key, c in side["palette"].items():
            assert c["index"] == round(c["code"] * 20) and re.fullmatch(r"#[0-9A-F]{6}", c["srgb"]), (name, key)
        fbx = fbx_strings((ROOT / spec["fbx"]["path"]).read_bytes())
        names = [Path(spec["fbx"]["path"]).name, Path(spec["glb"]["path"]).name, name, *spec["keys"]]
        names += [n.get("name", "") for n in g["nodes"]] + [m.get("name", "") for m in g["meshes"]] + mats
        names += [s for s in fbx if not FBX_OK.match(s)]
        for nm in names:
            h = hits(nm, entries)
            assert not h, (name, "IP denylist", nm, h)
        tris_total += tris
    assert listed == on_disk, ("manifest vs files", sorted(listed ^ on_disk))
    lay = (MODELS / "layout.json").read_bytes()
    assert hashlib.sha256(lay).hexdigest() == manifest["layout_sha256"], "layout hash"
    assert size_total < 12_000_000, ("model set size", size_total)
    planted = ["Con" + "Ed_truck", "pg" + "&e_pylon", "Ch3rn0" + "byl_plant", "hoo" + "ver-dam.fbx", "Power " + "Grid",
               "Ves" + "tas_blades", "sim" + "city_block"]
    caught = sum(1 for p in planted if hits(p, entries))
    assert caught == len(planted), ("denylist proof", caught, [p for p in planted if not hits(p, entries)])
    assert not hits("conductor", entries) and not hits("powerhouse", entries) and not hits("edifice", entries)
    print(f"P2_ART_PASS: {len(manifest['models'])} models (fbx+glb+sidecar), {tris_total} triangles, {size_total} bytes, "
          f"names clean ({len(entries)} denylist entries), proof {caught}/{len(planted)}")


try:
    main()
except AssertionError as e:
    print("P2_ART_FAIL", e)
    sys.exit(1)
