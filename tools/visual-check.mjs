// Render every skin and compare each against its own frozen baseline.
//
// One command, because two are one too many: while only the default had a
// baseline it was easy to change the cockpit layer, see the default still
// pass, and believe nothing had moved. Every skin now has to answer for
// itself.
//
//   node tools/visual-check.mjs           verify all skins
//   node tools/visual-check.mjs --accept  re-record baselines (see below)
//
// --accept EXISTS FOR NEW SKINS AND INTENDED REDESIGNS ONLY. A baseline is
// evidence; re-recording it to make a failing check pass destroys the thing
// that makes the check worth running. If a diff surprises you, fix the XAML.
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";

const EXE      = path.join("OverlayMVP", "bin", "Debug", "net8.0-windows", "OverlayMVP.exe");
const BASE_DIR = path.join("tools", "visual-baseline");
const SCRATCH  = path.join("tools", ".shot-scratch");

// id -> baseline filename. The default keeps its original name so its history
// (and the guarantee the structure refactor rests on) is unbroken.
const FACTIONS = ["Caldari", "Gallente", "Amarr", "Minmatar"];
const CONSOLES = ["Navy", "Hangar", "Console"];

// The default keeps its original filename so its history — and the guarantee
// the structure refactor rests on — is unbroken.
const SKINS = [
  { id: null, baseline: "MainWindow.png" },
  ...FACTIONS.flatMap((f) => CONSOLES.map((c) => ({
    id: `${f}${c}`, baseline: `MainWindow.${f}${c}.png`,
  }))),
];

const accept = process.argv.includes("--accept");

if (!fs.existsSync(EXE)) {
  console.error(`visual-check: ${EXE} not found — build first`);
  process.exit(2);
}
fs.mkdirSync(SCRATCH, { recursive: true });

let bad = 0;

for (const skin of SKINS) {
  const label = skin.id ?? "Default";
  const shot  = path.resolve(SCRATCH, `${label}.png`);
  const base  = path.join(BASE_DIR, skin.baseline);

  const args = ["--screenshot", shot];
  // --skin only takes effect alongside --screenshot, by design: honouring it
  // in a normal run would make a paid skin a command-line flag away.
  if (skin.id) args.push("--skin", skin.id);

  try {
    execFileSync(EXE, args, { stdio: ["ignore", "pipe", "pipe"] });
  } catch (err) {
    console.error(`FAIL  ${label}: capture failed — ${err.message}`);
    bad++;
    continue;
  }

  if (!fs.existsSync(shot)) {
    console.error(`FAIL  ${label}: no image was written`);
    bad++;
    continue;
  }

  if (accept || !fs.existsSync(base)) {
    const isNew = !fs.existsSync(base);

    // Only rewrite what actually differs. Touching an unchanged baseline is
    // pure noise in a diff, and noise is where a real change hides.
    if (!isNew && fs.readFileSync(base).equals(fs.readFileSync(shot))) {
      console.log(`same  ${label} (baseline already current)`);
      continue;
    }

    // THE DEFAULT SKIN IS NOT A SKIN. Its baseline is the guarantee that the
    // free app never changed appearance, which every skin's diff is measured
    // against. If it moved, something leaked out of a skin and into the
    // default -- accepting that would destroy the reference silently, which
    // has already been caught happening once.
    if (!isNew && skin.id === null) {
      console.error(`REFUSED Default baseline changed. That is a REGRESSION, not a redesign.`);
      console.error(`        Find what leaked into the default theme; do not accept this.`);
      console.error(`        Capture kept at ${shot}`);
      bad++;
      continue;
    }

    fs.copyFileSync(shot, base);
    console.log(`${isNew ? "NEW  " : "ACCEPT"} ${label} -> ${base}`);
    continue;
  }

  const a = fs.readFileSync(base), b = fs.readFileSync(shot);
  if (a.equals(b)) {
    console.log(`ok    ${label}`);
  } else {
    console.error(`FAIL  ${label}: differs from ${base} (${a.length} vs ${b.length} bytes)`);
    console.error(`      capture kept at ${shot} for inspection`);
    bad++;
  }
}

fs.rmSync(SCRATCH, { recursive: true, force: true, maxRetries: 2 });
console.log(bad === 0 ? `visual-check: OK (${SKINS.length} skins)` : `visual-check: ${bad} problem(s)`);
process.exit(bad === 0 ? 0 : 1);
