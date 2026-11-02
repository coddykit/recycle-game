"""Export only office/apartment/shop as canonical LOD1 + separate close-up LOD0."""
import sys,json,struct,hashlib
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import build_assets as art
ROOT=Path(__file__).resolve().parents[2]
art.SELECTED={'office','apartment','shop'}
meta=json.loads((ROOT/'data/model_meta.json').read_text())
report={name:{} for name in sorted(art.SELECTED)}
art.reset_scene();art.MAT=art.palette_material()
for lod in (0,1):
    art.QUALITY_LOD=lod
    art.OUT_DIR=str(ROOT/'assets/models'/('lod0' if lod==0 else ''))
    Path(art.OUT_DIR).mkdir(exist_ok=True)
    art.build_buildings()
    for name in sorted(art.SELECTED):
        path=Path(art.OUT_DIR)/(name+'.glb');data=path.read_bytes();gltf=json.loads(data[20:20+struct.unpack_from('<I',data,12)[0]])
        tris=sum(gltf['accessors'][p['indices']]['count']//3 for mesh in gltf['meshes'] for p in mesh['primitives'])
        assert tris<=1500 and len(gltf['materials'])==1,(name,lod,tris)
        report[name][str(lod)]={**art.META[name],'tris':tris,'path':'res://'+str(path.relative_to(ROOT)),'sha256':hashlib.sha256(data).hexdigest()}
        if lod==1:meta[name]=art.META[name]
(ROOT/'data/model_meta.json').write_text(json.dumps(meta,indent=1,sort_keys=True))
qa=ROOT/'docs/art';qa.mkdir(parents=True,exist_ok=True)
(qa/'quality-pass-lods.json').write_text(json.dumps(report,indent=2)+'\n')
print('QUALITY_EXPORT',json.dumps(report))
