// TEMPORARY helper: stamp explicit width/height on every <img> in a manual.
// Paged.js measures images during pagination; without intrinsic dimensions it mis-measures tall
// screenshots and silently stops flowing content. The previous manuals carried explicit
// width/height attributes for the same reason.
//
// Display width targets (the CSS still caps height, this only gives Paged.js a size to reason
// about): landscape desktop captures -> 675px wide, portrait mobile captures -> 250px wide.
//
// Usage: node fiximg.mjs <manual.html>

import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';

const file = path.resolve(process.argv[2]);
const root = path.dirname(file);
let html = await readFile(file, 'utf8');

/** Reads intrinsic pixel size straight from the PNG IHDR chunk. */
async function pngSize(p) {
  const b = await readFile(p);
  return { w: b.readUInt32BE(16), h: b.readUInt32BE(20) };
}

const seen = new Set();
const tags = [...html.matchAll(/<img\s[^>]*src="([^"]+)"[^>]*>/g)];
let updated = 0;
const missing = [];

for (const [tag, src] of tags) {
  const abs = path.join(root, src);
  let size;
  try { size = await pngSize(abs); } catch { missing.push(src); continue; }

  const portrait = size.h > size.w;
  const targetW = portrait ? 250 : 675;
  const targetH = Math.round((size.h / size.w) * targetW);

  const cleaned = tag.replace(/\s(width|height)="[^"]*"/g, '');
  const replacement = cleaned.replace(/<img\s/, `<img width="${targetW}" height="${targetH}" `);
  if (replacement !== tag) { html = html.replace(tag, replacement); updated++; }
  seen.add(src);
}

await writeFile(file, html, 'utf8');
console.log(`images=${tags.length} stamped=${updated} unique=${seen.size}`);
if (missing.length) { console.log(`MISSING FILES (${missing.length}):`); [...new Set(missing)].forEach(m => console.log('  ' + m)); }
