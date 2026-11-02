"""W66: six adobe houses + three plants, one existing palette, no new textures.
Run: blender -b -t 2 -P tools/blender/build_desert_redo.py
Low homes <=204 triangles (old house_adobe); detail only on Medium/High.
"""
import json, math, sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_assets as a
from mathutils import Vector
from palette import uv_for
import bpy

a.META=json.loads(Path(a.META_PATH).read_text())
a.reset_scene()
a.MAT=a.palette_material()
counts={}

def face(m, pts, color):
    # Thin cloth / plaster patches need both sides with the shared culled material.
    for ring in (pts, list(reversed(pts))):
        f=m.bm.faces.new([m.bm.verts.new(p) for p in ring])
        for l in f.loops: l[m.uv].uv=uv_for(color)

def house(m, k, detail):
    w,d,h=[(1.8,1.6,1.24),(1.8,1.6,1.36),(1.8,1.6,1.12),(1.8,1.6,1.25),(1.8,1.6,1.15),(1.8,1.6,1.72)][k]
    # Recessed doorway: front wall panels stop before a dark inset surface.
    tint=['lightsand','peach','sand','khaki','clay','lightsand'][k]
    m.box((w,d-.14,h),(0,-.07,h/2),tint)
    m.box((.66,.14,h),(-.57,d/2-.07,h/2),tint)
    m.box((.66,.14,h),(.57,d/2-.07,h/2),tint)
    m.box((.48,.14,h-.76),(0,d/2-.07,(h+.76)/2),tint)
    m.box((.45,.02,.76),(0,d/2-.13,.38),'darkwood')
    # Raised parapet: genuinely open roof, not another solid box.
    for x in [-w/2+.06,w/2-.06]: m.box((.12,d,.22),(x,0,h+.11),tint)
    for y in [-d/2+.06,d/2-.06]: m.box((w-.24,.12,.22),(0,y,h+.11),tint)
    m.box((w-.24,d-.24,.04),(0,0,h+.015),'desertrock')
    # Side window sits behind a thick surround; visible from the default camera.
    side=-1 if k%2==0 else 1
    x=side*(w/2+.005)
    face(m,[(x,-.35,.49),(x,.12,.49),(x,.12,.91),(x,-.35,.91)],'darkbrown')
    # Small sill, baked weathering chips and foundation colour need no texture.
    m.box((.10,.56,.07),(x,-.12,.47),'desertrock')
    for z,y in [(.16,.46),(.32,-.5),(.94,.35)]:
        face(m,[(x+side*.002,y-.14,z),(x+side*.002,y+.12,z),(x+side*.002,y+.08,z+.11)],'desertrock')
    face(m,[(-.32,-d/2-.003,.48),(.32,-d/2-.003,.48),(.32,-d/2-.003,.88),(-.32,-d/2-.003,.88)],'darkwood')
    if k == 4:
        m.box((.36,.38,.40),(.48,-.36,h+.20),'clay') # roof vent silhouette
    else:
        m.box((.72,.09,.07),(0,-d/2-.02,.46),'desertrock')
    if k in [0,2,4]:
        # Thin solid cloth canopy with two wooden legs; reliable normals at every LOD.
        m.box((1.52,.46,.035),(0,1.01,.915),['teal','roofred','khaki'][k//2],rot=(-.28,0,0))
        for xx in [-.7,.7]: m.box((.055,.055,.86),(xx,1.18,.43),'darkwood')
    else:
        # Six-sided rooftop water barrel, visually unlike the old rooftop box.
        if k == 3:
            for xx in [-.40,.22]: m.cyl(.23,.39,(xx,-.25,h+.235),'darkgray',segs=6)
        else:
            m.cyl(.28,.43,(-.40,-.25,h+.255),'teal' if k==1 else 'darkgray',segs=6)
            m.cyl(.30,.045,(-.40,-.25,h+.49),'steel',segs=6)
    if detail:
        for xx in [-.62,0,.62]: m.cyl(.045,.25,(xx,.86,h-.12),'wood',axis='Y',segs=5)
        for yy in [-.51,.27]:
            m.box((.055,.09,.48),(x+side*.04,yy,.71),tint)
        # Stepped roof access, trim and irregular worn plaster patches.
        for i in range(5): m.box((.38,.20,.16*(i+1)),(.56,-.58+i*.18,.08*(i+1)),'desertrock')
        for i in range(14):
            yy=-.7+(i%7)*.2; zz=.12+(i//7)*.62
            face(m,[(x+side*.004,yy,zz),(x+side*.004,yy+.1,zz+.02),(x+side*.004,yy+.07,zz+.08)],'khaki')
        for xx in [-.28,.28]: m.box((.07,.06,.8),(xx,.81,.4),'desertrock')
        m.box((.64,.06,.08),(0,.81,.8),'desertrock')
    # Face normals are recalculated by MB.build; canopy is opaque, single surface.

def plant(m,k):
    if k==0: # acacia: broad, flattened umbrella, forked trunk
        m.cyl(.075,.85,(0,0,.425),'darkwood',segs=5)
        m.ball(.65,(0,0,1.02),'olive',scale=(1,.85,.32))
        m.ball(.38,(.33,.03,1.0),'khaki',scale=(1,.8,.42))
    elif k==1: # low multi-lobed sage shrub
        for x,y,r in [(-.18,0,.38),(.25,.09,.32),(0,-.18,.28)]:
            m.ball(r,(x,y,.30),'olive' if x<0 else 'khaki',scale=(1,1,.85))
    else: # narrow young cypress
        m.cyl(.065,.42,(0,0,.21),'wood',segs=5)
        m.cone(.36,1.12,(0,0,.91),'forest',segs=6)
        m.cone(.27,.82,(0,0,1.34),'olive',segs=6)

# Export without embedded image copies: runtime Models supplies the existing shared palette.
def emit(name, fn, budget):
    col=a.new_collection(name); mb=a.MB(); fn(mb); obj=mb.build(name,col)
    obj.data.calc_loop_triangles(); count=len(obj.data.loop_triangles)
    assert count<=budget,(name,count,budget)
    counts[name]=count
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active=obj
    bpy.ops.export_scene.gltf(filepath=str(Path(a.OUT_DIR)/(name+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_apply=True,export_materials='NONE',export_animations=False)
    bounds=[(v.co.x,v.co.z,-v.co.y) for v in obj.data.vertices]
    mn=[min(v[i] for v in bounds) for i in range(3)]
    mx=[max(v[i] for v in bounds) for i in range(3)]
    a.META[name]={'min':[round(v,3) for v in mn], 'max':[round(v,3) for v in mx],
                  'size':[round(mx[i]-mn[i],3) for i in range(3)]}
    bpy.data.objects.remove(obj,do_unlink=True); bpy.data.collections.remove(col)

for k in range(6):
    emit(f'adobe_{k}',lambda m,k=k:house(m,k,True),500)
    emit(f'adobe_{k}_low',lambda m,k=k:house(m,k,False),204)
for k in range(3): emit(f'desert_plant_{k}',lambda m,k=k:plant(m,k),68)
Path(a.META_PATH).write_text(json.dumps(a.META,indent=1,sort_keys=True)+'\n')
Path(a.ROOT,'docs/art/desert-redo-models.json').write_text(json.dumps(counts,indent=2)+'\n')
print('DESERT_REDO',counts)
