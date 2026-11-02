"""Rebuild review LOD1 from selected canonical LOD0 GLBs.
Run in Blender background via shot-lock, after the canonical model builders.
Runtime retains the existing Godot import LOD policy; these are explicit review/export assets.
"""
import bpy
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
MODELS = ROOT / 'assets/models'
IDS = ('barn', 'warehouse', 'house_cottage')
records = {}
for name in IDS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(MODELS / (name + '.glb')))
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    before = sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons)
    for ob in meshes:
        bpy.context.view_layer.objects.active = ob
        mod = ob.modifiers.new('review_lod1', 'DECIMATE')
        mod.ratio = .72
        mod.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    after = sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons)
    assert 0 < after < before <= 1500, (name, before, after)
    out = MODELS / 'lod1' / (name + '.glb')
    out.parent.mkdir(exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=str(out), export_format='GLB', export_yup=True,
                              export_texcoords=True, export_normals=True, export_materials='EXPORT')
    records[name] = {'lod0_triangles': before, 'lod1_triangles': after,
                     'lod1': 'res://assets/models/lod1/' + name + '.glb'}
(MODELS / 'lod1/manifest-round2.json').write_text(json.dumps({'generator':'tools/blender/build_review_lod2.py','models':records},indent=2)+'\n')
print('REVIEW_LOD_PASS', records)
