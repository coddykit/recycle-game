// node play_images.mjs [--apply] — Borderline Empire Play images (phone/7"/10"/icon/featureGraphic) for 10 langs, one edit, read-back
import fs from 'fs'; import crypto from 'crypto'; import { createRequire } from 'module';
const SA=process.env.STORE_AUTOMATION||new URL('../../../../store-automation',import.meta.url).pathname; const require=createRequire(SA+'/package.json'); const { google }=require('googleapis');
const auth=new google.auth.GoogleAuth({ keyFile: SA+'/google-play-service-account.json', scopes:['https://www.googleapis.com/auth/androidpublisher'] });
const ap=google.androidpublisher({ version:'v3', auth: await auth.getClient() });
const PKG='com.coddykit.borderlineempire', APPLY=process.argv.includes('--apply');
const W=(process.env.WORK||(()=>{throw new Error('set WORK=<scratch dir>')})())+'/work';
const ICON=new URL('../icons/icon_512.png',import.meta.url).pathname;
const FG=process.env.FEATURE_GRAPHIC||(process.env.WORK+'/smuggler-be/feature_1024x500.png');
const LANG={en:'en-US',tr:'tr-TR',de:'de-DE',es:'es-ES',fr:'fr-FR',ja:'ja-JP',ko:'ko-KR',pt:'pt-BR',ru:'ru-RU',zh:'zh-CN'};
const shots=(d,l)=>fs.readdirSync(`${W}/out/${d}/${l}`).filter(f=>/^\d\d-.*\.png$/.test(f)).sort().map(f=>`${W}/out/${d}/${l}/${f}`);
const sha1=f=>crypto.createHash('sha1').update(fs.readFileSync(f)).digest('hex');
const plan={}; for(const [l,gl] of Object.entries(LANG)) plan[gl]={phoneScreenshots:shots('play',l),sevenInchScreenshots:shots('ipad',l),tenInchScreenshots:shots('ipad',l),icon:[ICON],featureGraphic:[FG]};
const e=(await ap.edits.insert({packageName:PKG})).data.id;
try{
  const listed=new Set(((await ap.edits.listings.list({packageName:PKG,editId:e})).data.listings||[]).map(x=>x.language));
  for(const gl of Object.keys(plan)) if(!listed.has(gl)) throw new Error('no listing '+gl);
  for(const [gl,t] of Object.entries(plan)) console.log('plan',gl,Object.entries(t).map(([k,v])=>k+':'+v.length).join(' '));
  if(!APPLY){ await ap.edits.delete({packageName:PKG,editId:e}); console.log('DRY'); process.exit(0); }
  const doLang=async gl=>{ for(const [T,fsx] of Object.entries(plan[gl])){ await ap.edits.images.deleteall({packageName:PKG,editId:e,language:gl,imageType:T});
    for(const f of fsx) await ap.edits.images.upload({packageName:PKG,editId:e,language:gl,imageType:T,media:{mimeType:'image/png',body:fs.createReadStream(f)}}); } console.log('uploaded',gl); };
  const gls=Object.keys(plan); for(let k=0;k<gls.length;k+=3) await Promise.all(gls.slice(k,k+3).map(doLang));
  let c; try{ c=await ap.edits.commit({packageName:PKG,editId:e,changesNotSentForReview:true}); console.log('commit (changesNotSentForReview=true)',c.data.id); }
  catch(x){ if(!/changesNotSentForReview/i.test(x.message)) throw x; console.log('changesNotSentForReview not allowed for this app state:',x.message.slice(0,200)); c=await ap.edits.commit({packageName:PKG,editId:e}); console.log('commit (plain; draft app, no release exists)',c.data.id); }
}catch(x){ try{await ap.edits.delete({packageName:PKG,editId:e});}catch{} console.error('HATA',x.message); process.exit(1); }
const v=(await ap.edits.insert({packageName:PKG})).data.id; let bad=0;
for(const [gl,t] of Object.entries(plan)){ const row=[]; for(const [T,fsx] of Object.entries(t)){ const im=(await ap.edits.images.list({packageName:PKG,editId:v,language:gl,imageType:T})).data.images||[];
  const ok=im.length===fsx.length&&im.every((x,i)=>x.sha1===sha1(fsx[i])); if(!ok) bad++; row.push(`${T}=${im.length}${ok?'':'✗'}`); } console.log('readback',gl,row.join(' ')); }
await ap.edits.delete({packageName:PKG,editId:v}); console.log('readback mismatches',bad);
