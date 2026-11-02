// Borderline Empire — Play screenshots in the Coddy circuit style (icon + name header, pill chip, two-line headline,
// tilted glowing device, floating benefit chip); port of games/driftwood/tools/store/render.mjs (10-27). Deterministic local
// HTML/CSS -> PNG (Playwright + Sharp, no network). Game pixels stay intact: each device shows the whole raw frame from
// store/deck/capture_raw.sh playphone|playtab (assembled by store/circuit/assemble.py); nothing is painted into gameplay.
//   STORE_RAW=<raw dir> STORE_OUT=<out dir> [STORE_LOCALE=en-US] [STORE_KINDS=phone,tablet-7] [STORE_FEATURE=0] node store/circuit/render.mjs
// Out: <out>/<locale>/{phone,tablet-7,tablet-10}/NN-<id>.png (+ <locale>/feature.png unless STORE_FEATURE=0) + <out>/layout.json.
// Sizes: phone 1080x1920, 7" tablet 1200x1920, 10" tablet 1600x2560 (= the 7" layout at 4/3; portrait, RGB), feature 1024x500.
// Raw frames: <raw>/<locale>/phone/NN-<id>.png (1080x2340) and <raw>/<locale>/tablet/NN-<id>.png (1200x1920, both tablet sizes).
// Fonts (file URLs, sha256-pinned in fonts.json): Noto Sans / JP / KR from FONT_SRC (default /Volumes/BuildSSD/game-deps/font-src),
// Noto Sans SC 700/900 from store/deck/fonts. A hash mismatch stops the render.
// STORE_DECK_DEPS may point to a package.json with playwright + sharp (default: store-automation).
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
const here = path.dirname(fileURLToPath(import.meta.url));
const game = path.resolve(here, '../..'); // games/smuggler
const req = createRequire(process.env.STORE_DECK_DEPS || '/Volumes/BuildSSD/dev/CoddyApps/store-automation/package.json');
const { chromium } = req('playwright');
const sharp = req('sharp');
const RAW = process.env.STORE_RAW, OUT = process.env.STORE_OUT;
if (!RAW || !OUT) throw Error('STORE_RAW and STORE_OUT are required');
const copy = JSON.parse(fs.readFileSync(path.join(here, 'copy.json')));
const pins = JSON.parse(fs.readFileSync(path.join(here, 'fonts.json')));
const SHOTS = ['01-empire', '02-dispatch', '03-event', '04-fleet', '05-upgrades', '06-map', '07-contracts', '08-frost'];
const FEATURE_SHOT = '08-frost';
const BRAND = 'Borderline Empire';
const esc = x => String(x).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
const data = p => 'file://' + path.resolve(p);
const sha = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');

// header icon: the shipped Play icon (store/icons/icon_512.png)
const iconFile = path.join(game, 'store/icons/icon_512.png');
const iconStatus = 'shipped';
const icon = data(iconFile);

const FONT_SRC = process.env.FONT_SRC || '/Volumes/BuildSSD/game-deps/font-src';
const DECK_FONTS = path.join(game, 'store/deck/fonts');
// [family, file, dir, weight range]
const FAMILIES = [['GbLatin', 'NotoSans.ttf', FONT_SRC, '100 900'], ['GbJapanese', 'NotoSansJP.ttf', FONT_SRC, '100 900'],
  ['GbKorean', 'NotoSansKR.ttf', FONT_SRC, '100 900'], ['GbChinese', 'Noto-Sans-SC_wght-700.ttf', DECK_FONTS, '100 750'],
  ['GbChinese', 'Noto-Sans-SC_wght-900.ttf', DECK_FONTS, '751 900']];
const fontHashes = {};
for (const [, file, dir] of FAMILIES) {
  const h = sha(path.join(dir, file));
  if (h !== pins[file]) throw Error(`font ${file} does not match the pinned sha256 (store/circuit/fonts.json)`);
  fontHashes[file] = h;
}
const fontCSS = FAMILIES.map(([name, file, dir, w]) =>
  `@font-face{font-family:"${name}";src:url("file://${path.join(dir, file)}") format('truetype');font-weight:${w};font-display:block;}`).join('\n');
const LEAD = {'ja-JP': '"GbJapanese",', 'ko-KR': '"GbKorean",', 'zh-CN': '"GbChinese",'};

function circuit(i, w, h) {
  let seed = i * 31 + 101;
  const rnd = () => ((seed = seed * 16807 % 2147483647) / 2147483647);
  let marks = '';
  for (let n = 0; n < 18; n++) {
    const x = Math.round(rnd() * w), y = Math.round(rnd() * h), dx = Math.round((rnd() - .5) * w * .45), dy = Math.round((rnd() - .5) * h * .2);
    marks += `<path d="M${x},${y}v${dy}h${dx}v${dy / 2}"/><circle cx="${x + dx}" cy="${y + dy * 1.5}" r="8"/>`;
  }
  return `<svg class="circuit" viewBox="0 0 ${w} ${h}" preserveAspectRatio="none"><g fill="none" stroke="#e6dcff" stroke-width="3">${marks}</g></svg>`;
}

// one line icon per benefit: coins, compass, bolt, truck, building, map, clipboard, globe; feature = truck
const ICONS = [
  '<circle cx="18" cy="18" r="10"/><path d="M14 18h8M18 14v8"/><circle cx="31" cy="31" r="10"/><path d="M27 31h8"/>',
  '<circle cx="24" cy="24" r="18"/><path d="M31 17l-4 10-10 4 4-10z"/>',
  '<path d="M27 4L10 27h12l-3 17 18-24H25z"/>',
  '<path d="M4 12h24v20H4zM28 20h9l7 7v5H28z"/><circle cx="12" cy="36" r="4"/><circle cx="36" cy="36" r="4"/>',
  '<path d="M6 42h36M10 42V18l14-10 14 10v24"/><path d="M19 42V30h10v12M18 20h12"/>',
  '<path d="M4 10l12-4 16 6 12-4v30l-12 4-16-6-12 4z"/><path d="M16 6v30M32 12v30"/>',
  '<path d="M14 8h20v36H14z"/><path d="M19 4h10v8H19zM19 20h10M19 28h10M19 36h6"/>',
  '<circle cx="24" cy="24" r="18"/><path d="M6 24h36M24 6c-6 6-6 30 0 36M24 6c6 6 6 30 0 36"/>',
];
const FLAG = ICONS[3];

// layout per kind (CSS px at deviceScaleFactor 1; 10" tablet = the 7" layout at 4/3 scale)
const KINDS = {
  'phone': {w: 1080, h: 1920, scale: 1, raw: 'phone', dev: {top: 640, w: 560, radius: 60, bezel: 20}},
  'tablet-7': {w: 1200, h: 1920, scale: 1, raw: 'tablet', dev: {top: 640, w: 760, radius: 46, bezel: 22}, tilt: 0.6},
  'tablet-10': {w: 1200, h: 1920, scale: 4 / 3, raw: 'tablet', dev: {top: 640, w: 760, radius: 46, bezel: 22}, tilt: 0.6},
};
const RAW_SIZE = {phone: [1080, 2340], tablet: [1200, 1920]};

function slideHtml(locale, i, kind) {
  const words = copy[locale], slide = words.slides[i], K = KINDS[kind];
  const [rw, rh] = RAW_SIZE[K.raw];
  const sw = K.dev.w - 2 * K.dev.bezel, shh = Math.round(sw * rh / rw);
  const dh = shh + 2 * K.dev.bezel, tilt = (i % 2 ? 1 : -1) * (K.tilt || 1);
  const raw = data(path.join(RAW, locale, K.raw, SHOTS[i] + '.png'));
  const benefitTop = Math.min(K.h - 230, K.dev.top + dh - 150);
  return page(locale, K.w, K.h, `
  ${circuit(i, K.w, K.h)}
  <div class="brand"><img src="${icon}" alt=""><span>${esc(BRAND)}</span></div>
  <div class="page">${String(i + 1).padStart(2, '0')} / 08</div>
  <div class="tag" data-fit>${esc(slide[0])}</div>
  <h1 data-fit style="width:${K.w - 140}px">${esc(slide[1])}</h1>
  <div class="device" style="left:${(K.w - K.dev.w) / 2}px;top:${K.dev.top}px;width:${K.dev.w}px;height:${dh}px;border-radius:${K.dev.radius}px;
    transform:perspective(2600px) rotateY(${tilt * 9}deg) rotateX(3deg) rotateZ(${tilt * 2.2}deg)">
    <div class="screen" style="left:${K.dev.bezel}px;top:${K.dev.bezel}px;width:${sw}px;height:${shh}px;border-radius:${K.dev.radius - K.dev.bezel + 6}px"><img src="${raw}" alt=""></div></div>
  <div class="benefit" style="top:${benefitTop}px;max-width:${K.w - 260}px"><div class="badge">${badge(ICONS[i])}</div><span data-fit>${esc(slide[2])}</span></div>
  <div class="spark" style="left:${K.w - 150}px;top:${K.dev.top - 20}px">${spark()}</div>`);
}

function featureHtml(locale) {
  const words = copy[locale];
  const raw = data(path.join(RAW, locale, 'phone', FEATURE_SHOT + '.png'));
  return page(locale, 1024, 500, `
  ${circuit(9, 1024, 500)}
  <div class="brand"><img src="${icon}" alt=""><span>${esc(BRAND)}</span></div>
  <div class="tag" data-fit>${esc(words.category)}</div>
  <h1 data-fit>${esc(words.feature[0])}</h1>
  <div class="device" style="left:742px;top:20px;width:214px;height:${Math.round(198 * 2340 / 1080) + 16}px;border-radius:30px;
    transform:perspective(1400px) rotateY(-10deg) rotateX(3deg) rotateZ(4deg)">
    <div class="screen" style="left:8px;top:8px;width:198px;height:${Math.round(198 * 2340 / 1080)}px;border-radius:24px"><img src="${raw}" alt=""></div></div>
  <div class="benefit"><div class="badge">${badge(FLAG)}</div><span data-fit>${esc(words.feature[1])}</span></div>`, true);
}

const badge = p => `<svg viewBox="0 0 48 48" width="64%" height="64%" fill="none" stroke="currentColor" stroke-width="3.6" stroke-linecap="round" stroke-linejoin="round">${p}</svg>`;
const spark = () => '<svg width="58" height="58" viewBox="0 0 70 70" fill="none" stroke="currentColor" stroke-width="4"><path d="M35 5Q39 31 65 35Q39 39 35 65Q31 39 5 35Q31 31 35 5Z"/></svg>';

function page(locale, w, h, body, feature = false) {
  return `<!doctype html><html lang="${locale}"><meta charset="utf-8"><style>
  ${fontCSS}
  *{box-sizing:border-box} html,body{margin:0;width:${w}px;height:${h}px;overflow:hidden}
  body{color:#ffffff;font-family:${LEAD[locale] || ''}"GbLatin","GbJapanese","GbKorean","GbChinese";background:#140c33}
  .art{position:relative;width:${w}px;height:${h}px;overflow:hidden;
    background:radial-gradient(ellipse at 74% 58%,#8f6bff66 0,transparent 55%),radial-gradient(ellipse at 10% 100%,#ffc93c40,transparent 46%),
      linear-gradient(168deg,#120a2e 0%,#2a1a63 44%,#4b2fa6 82%,#6a44c8 120%)}
  .circuit{position:absolute;inset:0;width:100%;height:100%;opacity:.12}
  .brand{position:absolute;left:72px;top:62px;display:flex;align-items:center;gap:24px;z-index:4}
  .brand img{width:104px;height:104px;border-radius:24px;box-shadow:0 0 0 2px #fff3,0 12px 40px #0008}
  .brand span{font-size:50px;font-weight:800;letter-spacing:-1px;font-family:"GbLatin"}
  .page{position:absolute;right:76px;top:100px;color:#e2d8ff;font-size:26px;letter-spacing:5px;font-family:"GbLatin";z-index:4}
  .tag{position:absolute;top:214px;left:74px;padding:15px 30px;border-radius:99px;background:linear-gradient(100deg,#ffc93c,#ffffff);color:#1d1146;font-size:31px;font-weight:800;max-width:820px;white-space:nowrap;overflow:hidden;z-index:4}
  h1{position:absolute;top:322px;left:70px;height:270px;margin:0;font-size:104px;line-height:1.08;letter-spacing:-3px;font-weight:800;white-space:pre;overflow:hidden;z-index:4;text-shadow:0 4px 30px #0a0618aa}
  :lang(zh) h1{letter-spacing:0;line-height:1.18}
  :lang(ja) h1,:lang(ko) h1{letter-spacing:-1px;line-height:1.18}
  .device{position:absolute;background:linear-gradient(150deg,#f1ebff,#3a2580 16%,#140c33 74%,#ffc93c);box-shadow:0 0 0 3px #efe6ff70,0 0 40px #9b7bffa0,0 0 130px #ffc93c40,24px 40px 8px #070414aa,30px 60px 90px #000a;z-index:2}
  .screen{position:absolute;overflow:hidden;background:#000}
  .screen img{display:block;width:100%;height:100%}
  .device:after{content:'';position:absolute;inset:8px;border-radius:inherit;box-shadow:inset 0 2px 1px #fff6;pointer-events:none}
  .benefit{position:absolute;left:66px;display:flex;align-items:center;gap:20px;padding:20px 28px 20px 18px;min-height:116px;border:2px solid #efe6ff55;border-radius:32px;background:linear-gradient(115deg,#6a44c8f2,#22145af4);box-shadow:0 18px 60px #0008;z-index:5}
  .badge{display:flex;width:76px;height:76px;flex-shrink:0;align-items:center;justify-content:center;border-radius:22px;color:#1d1146;background:linear-gradient(130deg,#ffc93c,#ffffff)}
  .benefit span{min-width:0;max-height:124px;font-size:36px;font-weight:700;line-height:1.5;text-wrap:balance;overflow:hidden}
  .spark{position:absolute;color:#ffc93c;opacity:.8;z-index:3}
  .feature .brand{left:40px;top:30px;gap:16px}.feature .brand img{width:62px;height:62px;border-radius:15px}.feature .brand span{font-size:32px}
  .feature .tag{top:112px;left:42px;font-size:18px;padding:9px 18px;max-width:560px}
  .feature h1{top:178px;left:40px;width:640px;height:190px;font-size:62px;line-height:1.1;letter-spacing:-1.5px}
  .feature .benefit{left:42px;top:378px;max-width:640px;min-height:78px;padding:12px 18px 12px 12px;gap:14px;border-radius:20px;border-width:1px}
  .feature .badge{width:50px;height:50px;border-radius:14px}.feature .benefit span{font-size:24px;max-height:76px}
  .feature .device:after{inset:4px}
  </style><body><main class="art ${feature ? 'feature' : ''}">${body}</main></body></html>`;
}

const TMP_HTML = path.join(fs.mkdtempSync(path.join(process.env.TMPDIR || '/tmp', 'smugstore-')), 'slide.html');
const browser = await chromium.launch({headless: true, args: ['--disable-gpu', '--mute-audio', '--allow-file-access-from-files']});
const report = [];
try {
  for (const locale of Object.keys(copy)) {
    if (process.env.STORE_LOCALE && !process.env.STORE_LOCALE.split(',').includes(locale)) continue;
    const jobs = [];
    const kinds = process.env.STORE_KINDS ? process.env.STORE_KINDS.split(',') : Object.keys(KINDS);
    for (const kind of kinds) for (let i = 0; i < SHOTS.length; i++) jobs.push({kind, i});
    if (process.env.STORE_FEATURE !== '0') jobs.push({kind: 'feature', i: SHOTS.indexOf(FEATURE_SHOT)});
    for (const job of jobs) {
      const feature = job.kind === 'feature';
      const K = feature ? {w: 1024, h: 500, scale: 1} : KINDS[job.kind];
      const ctx = await browser.newContext({viewport: {width: K.w, height: K.h}, deviceScaleFactor: K.scale});
      const page_ = await ctx.newPage();
      fs.writeFileSync(TMP_HTML, feature ? featureHtml(locale) : slideHtml(locale, job.i, job.kind));
      await page_.goto('file://' + TMP_HTML);
      await page_.evaluate(async (names) => { await Promise.all(names.flatMap(n => [document.fonts.load(`700 20px "${n}"`), document.fonts.load(`800 20px "${n}"`)])); await document.fonts.ready; await Promise.all([...document.images].map(x => x.decode())); }, [...new Set(FAMILIES.map(f => f[0]))]);
      const fonts_loaded = await page_.evaluate(() => document.fonts.status === 'loaded' && [...document.fonts].every(f => f.status === 'loaded'));
      const checks = await page_.evaluate((min) => {
        const result = [];
        const visualLines = (e) => {
          const range = document.createRange(); const walker = document.createTreeWalker(e, NodeFilter.SHOW_TEXT);
          const tops = new Map();
          for (let node = walker.nextNode(); node; node = walker.nextNode()) for (let k = 0; k < node.length; k++) {
            const ch = node.data[k]; if (/\s/.test(ch)) continue;
            range.setStart(node, k); range.setEnd(node, k + 1);
            const rect = range.getClientRects()[0]; if (!rect) continue;
            const key = Math.round((rect.top + rect.bottom) / 2 / 14); tops.set(key, (tops.get(key) || '') + ch);
          }
          return [...tops.values()];
        };
        for (const e of document.querySelectorAll('[data-fit]')) {
          let n = parseFloat(getComputedStyle(e).fontSize);
          if (e.tagName === 'H1') {
            const authored = e.textContent.split('\n').length;
            while ((visualLines(e).length > authored || e.scrollWidth > e.clientWidth + 1) && n > 18) e.style.fontSize = `${--n}px`;
          }
          while ((e.scrollWidth > e.clientWidth + 1 || e.scrollHeight > e.clientHeight + 1) && n > 14) e.style.fontSize = `${--n}px`;
          result.push({element: e.tagName, cls: e.className, text: e.textContent, font: n, lines: visualLines(e),
            fit: e.scrollWidth <= e.clientWidth + 1 && e.scrollHeight <= e.clientHeight + 1});
        }
        // every text block fully on the canvas
        for (const e of document.querySelectorAll('.brand,.tag,h1,.benefit,.page')) {
          const r = e.getBoundingClientRect();
          result.push({element: 'bounds ' + (e.className || e.tagName), bounds: [r.left, r.top, r.right, r.bottom],
            fit: r.left >= 0 && r.top >= 0 && r.right <= innerWidth && r.bottom <= innerHeight});
        }
        // text never on the device frame/screen, except the benefit chip that floats over its lower edge by design;
        // headline / tag / brand must clear the device's bounding box
        const box = s => { const e = document.querySelector(s); if (!e) return null; const r = e.getBoundingClientRect(); return [r.left, r.top, r.right, r.bottom, s]; };
        const dev = box('.device');
        const ink = (s) => { const e = document.querySelector(s); if (!e) return null; const range = document.createRange(); range.selectNodeContents(e); const r = range.getBoundingClientRect(); return [r.left, r.top, r.right, r.bottom, s + ' ink']; };
        const blocks = ['.brand', '.page', '.tag', '.benefit'].map(box).concat([ink('h1')]).filter(Boolean);
        for (let a = 0; a < blocks.length; a++) for (let b = a + 1; b < blocks.length; b++) {
          const [l1, t1, r1, b1, n1] = blocks[a], [l2, t2, r2, b2, n2] = blocks[b];
          result.push({element: `gap ${n1}|${n2}`, fit: !(l1 < r2 && l2 < r1 && t1 < b2 && t2 < b1)});
        }
        for (const bl of blocks) {
          if (!dev || bl[4] === '.benefit') continue;
          const [l1, t1, r1, b1, n1] = bl, [l2, t2, r2, b2] = dev;
          result.push({element: `clear ${n1}|device`, fit: !(l1 < r2 && l2 < r1 && t1 < b2 && t2 < b1)});
        }
        // the benefit chip may cover at most the device's bottom 22 %
        const ben = box('.benefit');
        if (dev && ben) result.push({element: 'benefit over device', fit: ben[1] >= dev[1] + (dev[3] - dev[1]) * 0.6});
        return result;
      });
      const bad = checks.filter(x => !x.fit);
      if (!fonts_loaded || bad.length) throw Error(`Layout ${locale} ${job.kind} ${job.i}: fonts ${fonts_loaded} ${JSON.stringify(bad)}`);
      const rel = feature ? path.join(locale, 'feature.png') : path.join(locale, job.kind, SHOTS[job.i] + '.png');
      const out = path.join(OUT, rel);
      fs.mkdirSync(path.dirname(out), {recursive: true});
      const png = await page_.screenshot({type: 'png', animations: 'disabled'});
      await sharp(png).removeAlpha().png({compressionLevel: 9, adaptiveFiltering: true}).toFile(out);
      const meta = await sharp(out).metadata();
      report.push({locale, kind: job.kind, shot: SHOTS[job.i], file: rel, size: [meta.width, meta.height], fonts_loaded,
        header_icon: path.relative(game, iconFile), header_icon_status: iconStatus, brand: BRAND, checks});
      await ctx.close();
    }
    console.log('RENDER', locale, jobs.length);
  }
} finally { await browser.close(); }
// merge into layout.json (a partial run replaces only its locales)
const lp = path.join(OUT, 'layout.json');
const done = new Set(report.map(r => r.locale));
const old = fs.existsSync(lp) ? JSON.parse(fs.readFileSync(lp)).records.filter(r => !done.has(r.locale)) : [];
fs.writeFileSync(lp, JSON.stringify({fonts: fontHashes, header_icon_sha256: sha(iconFile), records: old.concat(report)}) + '\n');
console.log('STORE_RENDER_PASS', report.length);
