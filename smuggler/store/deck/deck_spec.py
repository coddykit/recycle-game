"""Borderline Empire store deck — shared spec (devices, raw game shots, slide order).

Style: "Cargo Manifest" — night-asphalt caption band, stencil headline, a red rubber-stamp keyword,
a typewritten manifest sub-line and a yellow/black barrier stripe over the real, full-width game frame.
"""

# slide = final store size; band = caption band height on top; game = raw capture size (slide minus band)
DEVICES = {
    "iphone69": {"slide": (1320, 2868), "band": 640},
    "ipad13": {"slide": (2064, 2752), "band": 560},
    "playphone": {"slide": (1080, 1920), "band": 430},
    "tab10": {"slide": (1600, 2560), "band": 540},
}
for _d in DEVICES.values():
    _d["game"] = (_d["slide"][0], _d["slide"][1] - _d["band"])

# exports derived by resampling a rendered slide (aspect difference < 0.5 %)
DERIVED = {"iphone65": ("iphone69", (1284, 2778)), "tab7": ("tab10", (1200, 1920))}

# raw game moments (QA hooks in scripts/core/debug_tools.gd; isolated --fresh QA save)
SHOTS = {
    "01_empire": {"args": "--notut --unlock=3 --garage=4 --buy=small_van,cargo_van,pickup,box_truck,minibus --auto=3 --sim=40 --nomodal --focus=hq --zoom=34 --setcash=48250 --goal=10 --wait=2.5"},
    "02_contracts": {"args": "--notut --unlock=3 --garage=3 --biz=contract_office:2 --rep=40 --sim=70 --nomodal --setcash=12840 --goal=9 --panel=contracts --wait=1.5"},
    "03_dispatch": {"args": "--notut --unlock=3 --garage=3 --buy=small_van,pickup --rep=40 --sim=70 --nomodal --setcash=12840 --goal=9 --panel=dispatch --wait=2"},
    "04_event": {"args": "--notut --garage=2 --buy=small_van --autoplay=dispatch,event --event_id=inspection --setcash=3420 --goal=5 --nomodal --wait=1.5"},
    "05_fleet": {"args": "--notut --unlock=4 --garage=4 --buy=small_van,cargo_van,pickup,box_truck,minibus --auto=3 --sim=60 --nomodal --setcash=486000 --goal=11 --panel=fleet --wait=2"},
    "06_map": {"args": "--notut --unlock=5 --garage=3 --buy=small_van,pickup,cargo_van --auto=3 --sim=40 --nomodal --focus=2 --zoom=115 --setcash=186500 --goal=15 --wait=3"},
    "07_auto": {"args": "--notut --unlock=4 --garage=4 --buy=small_van,cargo_van,pickup,box_truck --auto=3 --sim=60 --nomodal --setcash=486000 --goal=12 --panel=business --wait=2"},
}
ORDER = list(SHOTS)
LANGS = ["en", "tr", "de", "es", "fr", "ja", "ko", "pt", "ru", "zh"]
