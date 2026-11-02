#!/usr/bin/env python3
"""Offline store checks for Hospital Manager: Charge Nurse (charge-nurse/) — Play listing + screenshot copy. No store / API access.
Called by unity/Tools/check.sh.

  python3 charge-nurse/store/validate.py      # exit 0 = STORE_LISTING_PASS, else a FAIL list + exit 1

Thin config: all logic (limits, per-language banned wording in the 10 locales, title = "<keyword>: Charge Nurse", ad-removal product
names from iap.json, privacy URL last, screenshot limits, Data safety CSV, store art sizes) is in
tools/store_templates/store_lint.py. Word lists below: banned_all = proper nouns (every locale), banned_en* = English only,
banned_lang = per-language lists. Plan 1-3, 11, 13 originality list; no medical claims, nothing graphic.
"""
import re
import sys
from pathlib import Path

_here = Path(__file__).resolve()
sys.path.insert(0, str(next(p / "tools/store_templates" for p in _here.parents if (p / "tools/store_templates/store_lint.py").exists())))
import store_lint  # noqa: E402

def en_listing(ctx, full):
    ctx.check(re.search(r"^[A-Z][A-Z ,&'-]{3,}$", full, re.M), "en-US fullDescription needs CAPS section headers")
    ctx.check(re.search(r"^- ", full, re.M), "en-US fullDescription needs '- ' bullets")


# Store text makes no medical claims and stays non-graphic (plan section 5 tone, section 10 health-ads note). Per language
# (the English "die" must not hit the German article): cure / heal / diagnose claims, "real doctors", blood, corpses, death.
MEDICAL = {
    "en": [r"cures?", r"heal(?:s|ing)?\s+(?:you|real)", r"diagnose\s+you", r"real\s+doctors?", r"medically\s+accurate",
           r"clinically", r"blood", r"bleed\w*", r"gore", r"corpse\w*", r"die", r"dies", r"died", r"death", r"dead",
           r"surgery\s+simulator"],
    "tr": [r"tedavi\s+eder\w*", r"iyileştirir\w*", r"teşhis\s+koy\w*", r"gerçek\s+doktor\w*", r"tıbben\s+doğru",
           r"kanama\w*", r"kanlı", r"ceset\w*", r"ölüm\w*", r"öldür\w*", r"ölü", r"ölür\w*"],
    "de": [r"heilt\s+(?:dich|dir|sie|echte)\w*", r"heilen\s+echte\w*", r"diagnostiziert\s+(?:dich|sie)", r"echte\s+[äa]rzt\w*",
           r"medizinisch\s+(?:korrekt|exakt|genau)", r"klinisch\w*", r"blut", r"blutig\w*", r"blutet", r"leichen?",
           r"tod", r"tot", r"stirbt", r"sterben", r"gestorben"],
    "es": [r"cura\s+(?:a\s+)?(?:pacientes\s+reales|enfermedades)", r"curar\s+de\s+verdad", r"diagnostica\s+(?:a\s+)?(?:ti|usted)",
           r"m[ée]dicos\s+reales", r"doctores\s+reales", r"m[ée]dicamente\s+(?:preciso|exacto|correcto)", r"cl[ií]nicamente",
           r"sangre", r"sangr\w*", r"cad[áa]ver\w*", r"gore", r"muerte", r"muere\w*", r"muert[oa]s?"],
    "fr": [r"gu[ée]rit", r"gu[ée]rir\s+(?:vraiment|pour\s+de\s+vrai)", r"diagnostique\s+(?:vous|tes)", r"vrais\s+m[ée]decins",
           r"m[ée]dicalement\s+(?:exact|pr[ée]cis|correct)", r"cliniquement", r"sang", r"saign\w*", r"cadavres?", r"gore",
           r"mort", r"morts", r"mourir", r"meurt"],
    "pt": [r"cura\s+(?:pacientes\s+reais|doen[çc]as)", r"curar\s+de\s+verdade", r"diagnostica\s+voc[êe]", r"m[ée]dicos\s+reais",
           r"medicamente\s+(?:preciso|exato|correto)", r"clinicamente", r"sangue", r"sangr\w*", r"cad[áa]ver\w*", r"gore",
           r"morte", r"morre\w*", r"mort[oa]s?"],
    "ru": [r"лечит\s+(?:вас|настоящ\w*)", r"вылечит\w*", r"диагностирует\s+вас", r"настоящи[ехй]\s+врач\w*",
           r"медицинск\w+\s+точн\w*", r"клиническ\w*", r"кровь", r"крови", r"кровав\w*", r"кровотеч\w*", r"труп\w*",
           r"смерть", r"смерти", r"умира\w*", r"мертв\w*", r"умер\w*"],
    "ja": [r"治療します", r"病気を治", r"診断します", r"本物の医師", r"医学的に正確", r"臨床", r"血液", r"出血", r"血まみれ",
           r"流血", r"死亡", r"死ぬ", r"死んだ", r"死体", r"遺体", r"グロテスク"],
    "ko": [r"치료합니다", r"질병을\s*치료", r"진단해", r"진짜\s*의사", r"의학적으로\s*정확", r"임상", r"혈액", r"출혈",
           r"피투성이", r"유혈", r"사망", r"죽음", r"죽는", r"죽은", r"시체", r"고어"],
    "hi": [r"इलाज\s+करता", r"असली\s+डॉक्टर", r"चिकित्सकीय\s+रूप\s+से\s+सटीक", r"खून", r"रक्तस्राव", r"मौत", r"मृत्यु",
           r"लाश", r"मरता", r"मरते"],
}

CFG = {
    "file": __file__,
    "brand": "Charge Nurse",
    # reference game + publisher, store competitors named in the plan, real hospitals / services / emblems, medicine brands, TV shows
    "banned_all": [
        r"codigames", r"hospital\s+empire", r"empire\s+tycoon", r"\bidle\s+hospital", r"the\s+ward\b", r"er\s+hospital\s+drama",
        r"happy\s+clinic", r"nordcurrent", r"pocket\s+hospital", r"operate\s+now", r"dream\s+hospital", r"hospital\s+rush",
        r"two\s+point", r"theme\s+hospital", r"project\s+hospital", r"grey.?s\s+anatomy", r"\bhouse\s+m\.?d\b", r"\ber\s+tv",
        r"mayo\s+clinic", r"cleveland\s+clinic", r"johns\s+hopkins", r"charit[ée]\b", r"\bnhs\b", r"red\s+cross",
        r"red\s+crescent", r"kızılay", r"croix.rouge", r"cruz\s+roja", r"rotes\s+kreuz", r"tylenol", r"aspirin", r"ibuprofen",
        r"paracetamol", r"advil", r"panadol", r"pfizer", r"bayer", r"novartis",
    ],
    "banned_lang": {"medical claim / graphic wording": MEDICAL},
    "en_listing": en_listing,
}

if __name__ == "__main__":
    store_lint.run(CFG)
