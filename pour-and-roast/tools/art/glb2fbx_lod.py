"""Blender headless: LOD0 GLB -> decimated FBX (collapse to a triangle budget). Used for the Meshy bales: Blender 4.0's
glTF exporter fails on their decimated LODs ("Array length mismatch"), the FBX exporter does not.
  blender -b --python pour-and-roast/tools/art/glb2fbx_lod.py -- <lod0.glb> <out.fbx> <triangles>
"""
import sys
import bpy

src, dst, tris = sys.argv[sys.argv.index("--") + 1:][:3]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
for ob in [o for o in bpy.data.objects if o.type == "MESH"]:
    have = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    if have > int(tris):
        m = ob.modifiers.new("dec", "DECIMATE")
        m.ratio = int(tris) / have
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.modifier_apply(modifier="dec")
    print("LOD", ob.name, have, "->", sum(len(p.vertices) - 2 for p in ob.data.polygons))
bpy.ops.export_scene.fbx(filepath=dst, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", object_types={"MESH", "EMPTY"},
                         path_mode="STRIP", embed_textures=False, mesh_smooth_type="FACE", add_leaf_bones=False)
print("FBX_OK", dst)
