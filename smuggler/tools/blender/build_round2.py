"""Rebuild only the three live round-two buildings; preserve all other model metadata."""
import json,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import build_assets as source
IDS=('barn','warehouse','house_cottage')
source.META=json.loads(Path(source.META_PATH).read_text())
source.reset_scene()
source.MAT=source.palette_material()
original=source.single
def selected(name, builder):
    if name in IDS: original(name,builder)
source.single=selected
source.build_buildings()
Path(source.META_PATH).write_text(json.dumps(source.META,indent=1,sort_keys=True))
