// Compare two PNGs captured by OverlayMVP --screenshot.
//
// A byte compare is enough: RenderTargetBitmap is deterministic for an
// identical visual tree at an identical size, so any difference in the
// rendered output changes the file. It cannot say WHERE the difference is --
// when it fails, open both images and look.
import fs from "node:fs";

const [a, b] = process.argv.slice(2);
if (!a || !b) { console.error("usage: node tools/shot-diff.mjs <a.png> <b.png>"); process.exit(2); }
for (const f of [a, b]) if (!fs.existsSync(f)) { console.error(`missing: ${f}`); process.exit(2); }

const A = fs.readFileSync(a), B = fs.readFileSync(b);
if (A.length !== B.length) {
  console.error(`shot-diff: SIZE ${A.length} -> ${B.length} bytes`);
  process.exit(1);
}
let diff = 0;
for (let i = 0; i < A.length; i++) if (A[i] !== B[i]) diff++;
console.log(diff === 0 ? "shot-diff: identical" : `shot-diff: ${diff} differing bytes`);
process.exit(diff === 0 ? 0 : 1);
