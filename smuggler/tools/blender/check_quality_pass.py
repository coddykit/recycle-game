import json,struct,subprocess,sys,math,io
from pathlib import Path
from PIL import Image

def inspect(data):
 n=struct.unpack_from('<I',data,12)[0];g=json.loads(data[20:20+n]);binary=data[28+n:]
 points=[];tris=0;surfaces=0;used=set();transforms={}
 def apply(p,node):
  if 'matrix' in node:
   m=node['matrix'];return [sum(m[c*4+r]*p[c] for c in range(3))+m[12+r] for r in range(3)]
  p=[v*s for v,s in zip(p,node.get('scale',[1,1,1]))];x,y,z,w=node.get('rotation',[0,0,0,1]);u=[x,y,z]
  uv=[u[1]*p[2]-u[2]*p[1],u[2]*p[0]-u[0]*p[2],u[0]*p[1]-u[1]*p[0]]
  uuv=[u[1]*uv[2]-u[2]*uv[1],u[2]*uv[0]-u[0]*uv[2],u[0]*uv[1]-u[1]*uv[0]]
  return [p[i]+2*(w*uv[i]+uuv[i])+node.get('translation',[0,0,0])[i] for i in range(3)]
 def walk(idx,parents):
  nonlocal tris,surfaces
  node=g['nodes'][idx];chain=[node]+parents
  if node.get('name','').startswith('Rotor_'):transforms[node['name']]={k:node[k] for k in ('translation','rotation','scale','matrix') if k in node}
  if 'mesh' in node:
   for pr in g['meshes'][node['mesh']]['primitives']:
    assert pr.get('mode',4)==4
    a=g['accessors'][pr['attributes']['POSITION']];bv=g['bufferViews'][a['bufferView']];assert a['componentType']==5126
    off=bv.get('byteOffset',0)+a.get('byteOffset',0);stride=bv.get('byteStride',12)
    for i in range(a['count']):
     p=list(struct.unpack_from('<fff',binary,off+i*stride))
     for t in chain:p=apply(p,t)
     assert all(math.isfinite(v) for v in p);points.append(p)
    tris+=g['accessors'][pr['indices']]['count']//3 if 'indices' in pr else a['count']//3
    surfaces+=1;used.add(pr.get('material',-1))
  for c in node.get('children',[]):walk(c,chain)
 for idx in g['scenes'][g.get('scene',0)]['nodes']:walk(idx,[])
 textures=[]
 for im in g.get('images',[]):
  if 'bufferView' in im:
   b=g['bufferViews'][im['bufferView']];o=b.get('byteOffset',0);img=Image.open(io.BytesIO(binary[o:o+b['byteLength']]));textures.append(list(img.size))
 lo=[min(p[i] for p in points) for i in range(3)];hi=[max(p[i] for p in points) for i in range(3)]
 return {'tris':tris,'materials':len(used),'surfaces':surfaces,'textures':textures,'min':lo,'max':hi,'size':[hi[i]-lo[i] for i in range(3)],'rotors':transforms}

ROOT=Path(__file__).resolve().parents[2]
meta=json.loads((ROOT/'docs/art/quality-pass-lods.json').read_text())
baseline=json.loads((ROOT/'docs/art/quality-pass-baseline.json').read_text())
for name,levels in meta.items():
 before=baseline[name]
 for lod,row in levels.items():
  path=ROOT/row['path'].removeprefix('res://');now=inspect(path.read_bytes())
  assert __import__('hashlib').sha256(path.read_bytes()).hexdigest()==row['sha256'],(name,lod,'hash')
  assert now['tris']==row['tris'] and now['tris']<=1500,(name,lod,'triangle budget')
  assert now['materials']<=before['materials'],(name,'material count')
  assert all(max(s)<=512 for s in now['textures']),(name,'texture size')
  assert all(abs(now['size'][i]-before['size'][i])<=max(.025,before['size'][i]*.12) for i in range(3)),(name,'bounds')
  assert abs(now['min'][1]-before['min'][1])<.025,(name,'pivot')
  assert now['rotors']==before['rotors'],(name,'rotor transforms')
  assert now["surfaces"]<=before["surfaces"],(name,"surfaces")
  print(name,'LOD'+lod,now['tris'],'tris',now['materials'],'materials',now['size'])
print('QUALITY_PASS_ASSETS PASS')
