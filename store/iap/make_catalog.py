#!/usr/bin/env python3
"""data/iap.json -> store/iap/catalog.json (the store-side catalog read by store-automation/iap_catalog_gp.ts and iap_rc_catalog.ts).
Runs the shared converter (games/tools/shop/tools/iap_store_catalog.py: 10-language checks, Play limits, price ladder), then adds
what RevenueCat needs: one entitlement per permanent product (lookup key = the slug; the game restores by it) and none for consumables.
    python3 games/smuggler/store/iap/make_catalog.py
"""
import json, subprocess, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
GAME = HERE.parent.parent
CONVERTER = GAME.parent / "tools/shop/tools/iap_store_catalog.py"
OUT = HERE / "catalog.json"

rc = subprocess.call([sys.executable, str(CONVERTER), str(GAME / "data/iap.json"), str(OUT)])
if rc:
    sys.exit(rc)
cat = json.loads(OUT.read_text(encoding="utf-8"))
names = {}
for p in cat["products"]:
    if p["kind"] == "permanent":
        p["entitlements"] = [p["slug"]]
        names[p["slug"]] = p["name"]["en"]
    else:
        p["entitlements"] = []
cat["entitlement_names"] = names
OUT.write_text(json.dumps(cat, ensure_ascii=False, indent=1), encoding="utf-8")
print("entitlements:", ", ".join(names))
