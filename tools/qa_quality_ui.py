#!/usr/bin/env python3
"""Actual rendered panels and interruption states; isolated saves, sequential runs."""
import subprocess,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'docs/qa/quality';OUT.mkdir(parents=True,exist_ok=True)
(ROOT/'docs/qa/.gdignore').touch()
rows=[]
cases=[]
for lang in 'en,tr,de,es,fr,ja,ko,pt,ru,zh'.split(','):
 cases.append((lang+'-dark', '360x720', ['--lang='+lang,'--theme=dark','--matrix_dir='+str(OUT/(lang+'-dark'))]))
for lang in ['en','de','ru','ja']:
 cases.append((lang+'-light','320x640',['--lang='+lang,'--theme=light','--matrix_dir='+str(OUT/(lang+'-light'))]))
for i,kind in enumerate(['privacy','advisor','offline','reset']):
 cases.append((kind,'320x640',['--lang=de','--theme=dark','--review_modal='+kind]))
cases.append(('tablet','768x1024',['--lang=zh','--theme=light','--matrix_dir='+str(OUT/'tablet')]))
for name,size,args in cases:
 shot=OUT/(name+'.png')
 cmd=['godot','--path',str(ROOT),'--resolution',size,'--','--qa','--fresh','--notitle','--notut','--nomodal','--delay=0.4','--wait=0.4','--audit','--shot='+str(shot),*args]
 r=subprocess.run(cmd,text=True,capture_output=True,timeout=240)
 log=r.stdout+r.stderr;(OUT/(name+'.log')).write_text(log)
 errors=[l for l in log.splitlines() if any(x in l for x in ['SCRIPT ERROR','ERROR:','QA_SMALL_BUTTON','QA_HORIZONTAL_OVERFLOW','QA_VERTICAL_OVERFLOW','QA_BACK_DISMISS:false','QA_RESET_DIALOG: false','QA_RESET_DIALOG:false','QA_RESET_KEPT:false'])]
 if name=='reset' and 'QA_RESET_DIALOG:true' not in log.replace(' ',''): errors.append('reset capture is not the real reset confirmation')
 rows.append({'case':name,'resolution':size,'passed':not errors and r.returncode==0 and shot.exists(),'panels':log.count('QA_PANEL:'),'errors':errors})
 print(name, 'PASS' if rows[-1]['passed'] else errors,flush=True)
(ROOT/'docs/ui-quality.json').write_text(json.dumps({'cases':rows,'limits':'Desktop rendered sizes; native density/safe-area/device tests recorded separately.'},indent=2)+'\n')
raise SystemExit(0 if all(r['passed'] for r in rows) else 1)
