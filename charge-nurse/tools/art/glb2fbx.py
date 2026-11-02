"""Blender headless: optimised GLB (tools/meshy/optimize.py output) -> FBX for Unity (Unity has no built-in glTF importer;
the project must not install one, docs/UNITY.md). Texture is NOT embedded: the PNG next to it is the material's map
(Unity: Editor/NurseArtImport assigns <name>_base.png / _base_low.png by tier).
  blender -b --python charge-nurse/tools/art/glb2fbx.py -- <in.glb> <out.fbx>
"""
import sys
import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                         object_types={'MESH', 'ARMATURE', 'EMPTY'}, path_mode='STRIP', embed_textures=False, add_leaf_bones=False,
                         mesh_smooth_type='FACE')
print("FBX_OK", dst)
