#!/usr/bin/env python3
"""Volt City P4 — the ONE source of the IAP catalogue (docs/IAP.md) -> every generated file:

  Assets/Resources/data/iap.json   the game's catalogue (Driftwood / Grid Boss schema: prefix, langs, ad_free_features,
                                   ad_free_products, groups, order, offers, consumable_policy, products{slug: ...}) + the
                                   P1 sim keys (consumable_days); read by the sim (GameData) and the shop (IapCatalog)
  ../store/iap/rc-catalog.json     RevenueCat + Play contract (entitlements per product, PPP tiers, offerings) — same format as
                                   games/driftwood/store/iap/rc-catalog.json, for the Patron's rc-setup / Play tooling
  ../store/iap/catalog.json        the Play catalogue store-automation/iap_catalog_gp.ts reads, written by the SHARED tool
                                   games/tools/shop/tools/iap_store_catalog.py (skipped when it is absent, e.g. on Linux)
  Assets/Resources/i18n/strings.csv  the P4 UI keys (shop / rewards / settings) from Tools/i18n_p4/<lang>.json

Texts: Tools/i18n_p4/<lang>.json (en written here, the 9 others by in-session sub-agents — games/CLAUDE.md section 9, no
external MT). Product ids never change once live.

  python3 games/volt-city/unity/Tools/build_iap.py          # write everything
  python3 games/volt-city/unity/Tools/build_iap.py --check  # exit 1 when a generated file is stale or a rule fails
"""
import csv
import io
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
UNITY = HERE.parent
GAME = UNITY.parent
IAP = UNITY / "Assets/Resources/data/iap.json"
STRINGS = UNITY / "Assets/Resources/i18n/strings.csv"
RC = GAME / "store/iap/rc-catalog.json"
PLAY = GAME / "store/iap/catalog.json"
SHARED = GAME.parent / "tools/shop/tools/iap_store_catalog.py"
TEXTS = HERE / "i18n_p4"
LANGS = ["en", "tr", "de", "es", "fr", "pt", "ru", "ja", "ko", "hi"]
PLAY_LOCALE = {"en": "en-US", "tr": "tr-TR", "de": "de-DE", "es": "es-ES", "fr": "fr-FR", "pt": "pt-BR", "ru": "ru-RU",
               "ja": "ja-JP", "ko": "ko-KR", "hi": "hi-IN"}
PREFIX = "com.coddykit.voltcity."
PACKAGE = "com.coddykit.voltcity"
# store-automation/src/pricing-policy.ts income-group multipliers (IAP.md: T1 100 %, T2 ~60 %, T3 ~35 %, T4 ~20 %)
TIERS = {"T1_high": 1.0, "T2_upper_mid": 0.6, "T3_lower_mid": 0.35, "T4_low": 0.22}
TRIAL = re.compile(r"free\s+trial|try\s+(it\s+)?free|start\s+your\s+free|subscription", re.I)

LOOKS = ["livery_sunset", "livery_arctic", "pylons_deco", "blades_candy", "skyline_neon", "room_retro"]
# every permanent entitlement that ships today (IAP.md section 4: 14 entitlements; `scenarios` is reserved until its content
# exists — RevenueCat lets the Patron attach it to premium-less packs later without a new product id)
ENTITLEMENTS = ["noads", "theme_gold", "speed4", "dispatcher", "forecast_plus", "offline_plus", "slots"] + LOOKS
AD_FREE = ["noads", "premium", "grid_pass", "bundle"]

# slug: kind, T1 USD, group, grants (RC entitlements), icon, extras
P = [
    ("noads", "permanent", 2.99, "adfree", ["noads"], "bag", {}),
    ("premium", "permanent", 8.99, "adfree", ["noads", "forecast_plus", "slots", "theme_gold"], "star", {}),
    ("grid_pass", "permanent", 6.99, "adfree", ["noads", "slots", "skyline_neon"], "flag", {}),
    ("bundle", "permanent", 21.99, "adfree", list(ENTITLEMENTS), "star", {"parts": ["premium", "speed", "dispatcher", "offline_plus", "cosmetics_all"]}),
    ("speed", "permanent", 2.99, "upgrades", ["speed4"], "clock", {}),
    ("dispatcher", "permanent", 4.99, "upgrades", ["dispatcher"], "ops", {}),
    ("forecast_plus", "permanent", 1.99, "upgrades", ["forecast_plus"], "cloud", {}),
    ("offline_plus", "permanent", 2.99, "upgrades", ["offline_plus"], "moon", {}),
    ("slots", "permanent", 1.99, "upgrades", ["slots"], "city", {}),
    ("livery_sunset", "permanent", 1.49, "looks", ["livery_sunset"], "truck", {"slot": "truck"}),
    ("livery_arctic", "permanent", 1.49, "looks", ["livery_arctic"], "truck", {"slot": "truck"}),
    ("pylons_deco", "permanent", 1.49, "looks", ["pylons_deco"], "line", {"slot": "pylons"}),
    ("blades_candy", "permanent", 1.49, "looks", ["blades_candy"], "plant_wind", {"slot": "blades"}),
    ("skyline_neon", "permanent", 1.49, "looks", ["skyline_neon"], "district", {"slot": "skyline"}),
    ("room_retro", "permanent", 1.49, "looks", ["room_retro"], "gear", {"slot": "room"}),
    ("cosmetics_all", "permanent", 6.99, "looks", list(LOOKS), "star", {"parts": list(LOOKS)}),
    ("starter", "permanent", 2.99, "offer", ["forecast_plus", "livery_sunset"], "flag", {"offer_only": True}),
    ("cash_s", "consumable", 0.99, "budget", [], "cash", {"days": 1.0}),
    ("cash_m", "consumable", 2.49, "budget", [], "cash", {"days": 2.5}),
    ("cash_l", "consumable", 4.99, "budget", [], "cash", {"days": 5.0}),
    ("cash_xl", "consumable", 8.99, "budget", [], "cash", {"days": 10.0}),
    ("comeback", "consumable", 0.99, "offer", [], "cash", {"days": 1.0, "offer_only": True, "ladder": False}),
    ("storm_kit", "consumable", 1.99, "offer", [], "spark", {"days": 3.0, "offer_only": True, "ladder": False}),
]
GROUPS = ["offer", "adfree", "upgrades", "looks", "budget"]
OFFERS = {
    "cooldown_days": 3, "max_per_session": 1, "first_session": False, "during_onboarding": False, "during_live_shift": False,
    "starter": {"day_min": 3, "day_max": 7, "per_game": 1},
    "bundle": {"day_min": 20, "per_game": 2},
    "comeback": {"event": "comeback", "away_days_min": 3, "per_game": 2},
    "storm_kit": {"event": "storm_report", "stars_max": 1, "per_chapter": 1, "per_game": 99},
}
RESERVED = {
    "_doc": "Not sold yet: the content does not exist (IAP.md lists it). Never in order/products/Play/RC offerings until it ships; "
            "the id stays reserved. When it ships, add the `scenarios` entitlement to bundle in RevenueCat (owners get it).",
    "scenarios": {"kind": "permanent", "usd": 2.99, "grants": ["scenarios"],
                  "note": "5 hand-made scenarios on the same map (Heat Dome Week, Ice Storm Night, Final Match Night, Eclipse Day, Islanded Grid)"},
}
OFFERINGS = [
    {"id": "default", "title": "Ad-free and packs", "packages": ["noads", "premium", "grid_pass", "bundle"], "note": "the ad-free products: the shop's first block"},
    {"id": "upgrades", "title": "Control-room upgrades", "packages": ["speed", "dispatcher", "forecast_plus", "offline_plus", "slots"], "note": "convenience: never hides a free path (pay-to-win check in IAP.md)"},
    {"id": "looks", "title": "City looks", "packages": LOOKS + ["cosmetics_all"], "note": "6 looks + Style Collection"},
    {"id": "budget", "title": "Budget", "packages": ["cash_s", "cash_m", "cash_l", "cash_xl"], "note": "consumables, at most 3 a UTC day, never restored"},
    {"id": "offers", "title": "Offers", "packages": ["starter", "comeback", "storm_kit"], "note": "IapOffers rules (data/iap.json offers); never in the tutorial / first session / a live shift"},
]


def texts():
    out = {}
    for lang in LANGS:
        f = TEXTS / f"{lang}.json"
        out[lang] = json.loads(f.read_text(encoding="utf-8")) if f.exists() else {}
    return out


def build_iap(t):
    problems = []
    products = {}
    for slug, kind, usd, group, grants, icon, extra in P:
        title = {l: t[l].get(f"iap.{slug}.t", "") for l in LANGS}
        desc = {l: t[l].get(f"iap.{slug}.d", "") for l in LANGS}
        for l in LANGS:
            if not title[l] or not desc[l]:
                problems.append(f"{slug}: {l} title/desc missing")
            if len(title[l]) > 55:
                problems.append(f"{slug}: {l} title over 55")
            if len(desc[l]) > 200:
                problems.append(f"{slug}: {l} description over 200 ({len(desc[l])})")
            if TRIAL.search(title[l] + " " + desc[l]):
                problems.append(f"{slug}: {l} trial / subscription wording")
        p = {"kind": kind, "usd": usd, "group": group, "grants": grants, "icon": icon, "ad_free": "noads" in grants}
        for k, v in extra.items():
            if k == "days":
                p["days_x10"] = int(round(v * 10))
            else:
                p[k] = v
        p["title"] = title
        p["desc"] = desc
        products[slug] = p
    ad_free = [s for s, *_ in P if "noads" in products[s]["grants"]]
    if ad_free != AD_FREE:
        problems.append(f"ad_free products {ad_free} != {AD_FREE} (games/CLAUDE.md section 8)")
    d = {
        "_doc": "Volt City IAP catalogue (P4). GENERATED by Tools/build_iap.py from docs/IAP.md + Tools/i18n_p4/*.json - do not edit. "
                "No trial, no subscription, no loot box, no premium currency. Product ids never change once live. ad_free = grants "
                "include noads (Remove Ads, Chief Engineer Edition, Utility Pass, Complete Grid Keeper Collection). consumable_days = "
                "days of current income granted by the sim's GrantPurchaseFunds (P1 key).",
        "prefix": PREFIX,
        "langs": LANGS,
        "legacy": {},
        "revenuecat": {"android_key": "", "ios_key": "", "note": "public goog_ key goes into VoltGameConfig.RevenueCatAndroidKey (Patron)"},
        "ad_free_features": ["noads"],
        "ad_free_products": ad_free,
        "entitlements": ENTITLEMENTS,
        "groups": [{"id": g, "title": {l: t[l].get(f"iap.group.{g}", "") for l in LANGS}} for g in GROUPS],
        "order": [s for s, *_ in P],
        "offers": OFFERS,
        "consumable_policy": {"utc_daily_pack_limit": 3, "reward_single_max_small_pack_fraction": 0.2,
                              "reward_daily_max_small_pack_fraction": 0.5, "delivery": "transaction_id_once_after_store_confirmation",
                              "restore": False},
        "consumable_days": {s: e["days"] for s, k, *_r, e in P if "days" in e},
        "reserved": RESERVED,
        "products": products,
    }
    return d, problems


def build_rc(d):
    names = {}
    prods = []
    for slug in d["order"]:
        p = d["products"][slug]
        perm = p["kind"] == "permanent"
        ents = list(dict.fromkeys(p["grants"])) if perm else []
        for e in ents:
            names.setdefault(e, d["products"][e]["title"]["en"] if e in d["products"] and d["products"][e]["grants"] == [e]
                             else {"noads": "Remove Ads", "theme_gold": "Gold Control Room", "speed4": "Rush Hour Speed"}.get(e, e))
        prods.append({
            "slug": slug, "id": d["prefix"] + slug, "kind": p["kind"],
            "play_type": "non_consumable (one-time managed product)" if perm else "consumable (one-time managed product, consumed after delivery)",
            "usd": p["usd"], "price_tiers_usd": {k: round(p["usd"] * m, 2) for k, m in TIERS.items()}, "group": p["group"],
            "grants": p["grants"], "entitlements": ents, "ad_free": p["ad_free"], "name": p["title"], "desc": p["desc"]})
    return {
        "_": "Play only. GENERATED from Assets/Resources/data/iap.json by unity/Tools/build_iap.py - edit the source, not this. One-time products "
             "only (no subscription, no trial). Entitlements = the grant ids (docs/IAP.md section 4); consumables grant none "
             "(RevenueCat: attach to none; delivered once per transaction, never restored).",
        "package": PACKAGE, "langs": LANGS, "play_locale": PLAY_LOCALE,
        "price_policy": {"reference": "usd is the reference tier-1 price; store-automation/src/pricing-policy.ts scales it by income group (PPP) before it reaches Play",
                         "tiers": TIERS},
        "ad_free": {"entitlement": "noads", "products": d["ad_free_products"],
                    "rule": "games/CLAUDE.md sections 3 + 8: Remove Ads and the big packs only; a cheap product never removes ads"},
        "entitlement_names": names, "offerings": OFFERINGS, "reserved": {k: v for k, v in d["reserved"].items() if k != "_doc"},
        "products": prods}


UI_PREFIXES = ("shop.", "look.", "reward.", "report.", "settings.", "city.")
# strings.csv keys that reuse a P4 text (GameScreen.Follow shows "fail.<reason>")
ALIASES = {"fail.reward_unavailable": "reward.unavailable", "fail.unavailable": "shop.unavailable"}


def strings_rows(t):
    """P4 UI keys for strings.csv (product texts stay in iap.json)."""
    rows = []
    for k in t["en"]:
        if k.startswith(UI_PREFIXES):
            rows.append([k] + [t[l].get(k, "") for l in LANGS])
    for k, src in ALIASES.items():
        rows.append([k] + [t[l].get(src, "") for l in LANGS])
    return rows


def merged_strings(t):
    table = list(csv.reader(io.StringIO(STRINGS.read_text(encoding="utf-8-sig"))))
    head, body = table[0], table[1:]
    new = {r[0]: r for r in strings_rows(t)}
    out = []
    seen = set()
    for r in body:
        if r and r[0] in new:
            out.append(new[r[0]])
            seen.add(r[0])
        else:
            out.append(r)
    for k, r in new.items():
        if k not in seen:
            out.append(r)
    s = io.StringIO()
    w = csv.writer(s, lineterminator="\n")
    w.writerow(head)
    for r in out:
        w.writerow(r)
    return s.getvalue()


def play_catalog(src_text):
    if not SHARED.exists():
        return None, "shared tool absent"
    with tempfile.TemporaryDirectory() as tmp:
        # the shared tool resolves <game>/data/iap.json; give it that shape so its language check finds no project.godot
        g = Path(tmp) / "volt-city"
        (g / "data").mkdir(parents=True)
        (g / "data/iap.json").write_text(src_text, encoding="utf-8")
        out = Path(tmp) / "catalog.json"
        r = subprocess.run([sys.executable, str(SHARED), str(g / "data/iap.json"), str(out)], capture_output=True, text=True)
        if r.returncode != 0:
            return None, (r.stdout + r.stderr).strip()
        d = json.loads(out.read_text(encoding="utf-8"))
        d["_"] = "generated from games/volt-city/unity/Assets/Resources/data/iap.json by games/tools/shop/tools/iap_store_catalog.py (via unity/Tools/build_iap.py) — edit Tools/build_iap.py, not this"
        return json.dumps(d, ensure_ascii=False, indent=1) + "\n", ""


def main():
    check = "--check" in sys.argv
    t = texts()
    d, problems = build_iap(t)
    for k, v in t["en"].items():
        for l in LANGS[1:]:
            if k not in t[l]:
                problems.append(f"i18n_p4/{l}.json: missing {k}")
            elif sorted(re.findall(r"\{[a-z_]+\}", t[l][k])) != sorted(re.findall(r"\{[a-z_]+\}", v)):
                problems.append(f"i18n_p4/{l}.json: placeholders differ in {k}")
    iap_text = json.dumps(d, ensure_ascii=False, indent=1) + "\n"
    rc_text = json.dumps(build_rc(d), ensure_ascii=False, indent=1) + "\n"
    strings_text = merged_strings(t)
    play_text, err = play_catalog(iap_text)
    if err and err != "shared tool absent":
        problems.append("iap_store_catalog.py: " + err)
    outs = [(IAP, iap_text), (RC, rc_text), (STRINGS, strings_text)] + ([(PLAY, play_text)] if play_text else [])
    if check:
        stale = [str(p.relative_to(GAME)) for p, txt in outs if not p.exists() or p.read_text(encoding="utf-8") != txt]
        if not PLAY.exists():
            stale.append("store/iap/catalog.json (missing)")
        for x in problems:
            print("FAIL", x)
        print("VOLT_IAP_CATALOG:", "OK" if not stale and not problems else "STALE " + ", ".join(stale) if stale else "FAIL",
              f"({len(d['order'])} products, {len(ENTITLEMENTS)} entitlements, ad_free = {', '.join(d['ad_free_products'])})")
        return 1 if stale or problems else 0
    if problems:
        print("\n".join(problems))
        return 1
    for p, txt in outs:
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(txt, encoding="utf-8")
    print(f"iap.json + rc-catalog.json{' + catalog.json' if play_text else ' (catalog.json skipped: ' + err + ')'} + strings.csv: "
          f"{len(d['order'])} products, {len(ENTITLEMENTS)} entitlements")
    return 0


if __name__ == "__main__":
    sys.exit(main())
