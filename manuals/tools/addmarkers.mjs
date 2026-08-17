// Adds the invisible ASCII page markers the two-pass TOC relies on, and strips Paged.js.
// Idempotent: existing markers are removed first, then re-added.
//
// Chapter headings (h1.chapter) do not reliably emit their tiny marker into the PDF text layer —
// the ::before block and heading styling swallow it — so a chapter's marker is placed at the start
// of the lead paragraph that immediately follows it. That paragraph is always on the same page as
// the heading, because h1.chapter carries break-before: page.
//
// Usage: node addmarkers.mjs <manual.html>

import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';

const file = path.resolve(process.argv[2]);
let html = await readFile(file, 'utf8');

// 1. Pagination belongs to Chromium now.
html = html
  .replace(/\s*<script>window\.PagedConfig[\s\S]*?<\/script>\s*/g, '\n')
  .replace(/\s*<script src="paged\.polyfill\.js"><\/script>\s*/g, '\n')
  .replace(/\s*\.toc a\.pg::after \{[^}]*\}\s*/g, '\n  ');

// 2. Clear any previous markers so the script can be re-run safely.
html = html.replace(/<span class="tocmark">#[a-z0-9-]+#<\/span>/gi, '');

// 3. Chapter marker -> start of the following lead paragraph.
let chapters = 0;
html = html.replace(
  /(<h1 class="chapter" id="([a-z0-9-]+)">[\s\S]*?<\/h1>\s*<p class="lead">)/gi,
  (all, head, id) => { chapters++; return `${head}<span class="tocmark">#${id}#</span>`; });

// 4. Section marker -> inline in the h2 (verified to extract correctly).
let sections = 0;
html = html.replace(/(<h2 id="([a-z0-9-]+)">)/gi,
  (all, open, id) => { sections++; return `${open}<span class="tocmark">#${id}#</span>`; });

await writeFile(file, html, 'utf8');
console.log(`chapter markers=${chapters} section markers=${sections}`);
console.log(`pagedjs removed=${!/paged\.polyfill/.test(html)}`);
