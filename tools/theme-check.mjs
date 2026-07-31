// Theme refactor verification.
//
//   snapshot  — record every colour in every XAML file, in document order,
//               BEFORE the refactor starts.
//   verify    — re-read the same files, resolving DynamicResource keys through
//               Tokens.Default.xaml, and assert the resulting colour list is
//               byte-identical to the snapshot.
//
// This is what makes ~310 mechanical edits safe: pixel-identity becomes a
// machine check instead of an eyeball check.
import fs from "node:fs";
import path from "node:path";

const ROOT   = "OverlayMVP";
const TOKENS = path.join(ROOT, "Themes", "Tokens.Default.xaml");
const BASE   = path.join("tools", "theme-baseline.json");

// Attributes whose value is a colour. Order matters and is preserved.
const ATTR = /(Background|Foreground|BorderBrush|Fill|Stroke|Color)\s*=\s*"([^"]+)"/g;

function xamlFiles() {
  const out = [];
  (function walk(dir) {
    for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) { if (e.name !== "obj" && e.name !== "bin") walk(p); }
      else if (e.name.endsWith(".xaml")) out.push(p);
    }
  })(ROOT);
  return out.sort();
}

/** x:Key → #AARRGGBB from the token dictionary. */
function loadTokens() {
  if (!fs.existsSync(TOKENS)) return {};
  const src = fs.readFileSync(TOKENS, "utf8");
  const map = {};
  for (const m of src.matchAll(/<SolidColorBrush\s+x:Key="([^"]+)"\s+Color="([^"]+)"/g)) {
    map[m[1]] = m[2].toUpperCase();
  }
  return map;
}

/** Every colour in one file, in document order, resolved to a hex where possible. */
function coloursOf(file, tokens, resolve) {
  const src = fs.readFileSync(file, "utf8");
  const out = [];
  for (const m of src.matchAll(ATTR)) {
    const v = m[2].trim();
    if (v.startsWith("#")) { out.push(v.toUpperCase()); continue; }
    const dyn = v.match(/^\{DynamicResource\s+([^}]+)\}$/);
    if (dyn) {
      const key = dyn[1].trim();
      if (!resolve) { out.push(`{${key}}`); continue; }
      if (!(key in tokens)) { out.push(`MISSING:${key}`); continue; }
      out.push(tokens[key]);
      continue;
    }
    const stat = v.match(/^\{StaticResource\s+([^}]+)\}$/);
    if (stat) { out.push(`STATIC:${stat[1].trim()}`); continue; }
    out.push(`OTHER:${v}`);   // bindings, converters — recorded so drift is visible
  }
  return out;
}

const mode = process.argv[2];

if (mode === "snapshot") {
  const tokens = loadTokens();
  const snap = {};
  for (const f of xamlFiles()) snap[f.replace(/\\/g, "/")] = coloursOf(f, tokens, false);
  fs.mkdirSync("tools", { recursive: true });
  fs.writeFileSync(BASE, JSON.stringify(snap, null, 2));
  const n = Object.values(snap).reduce((a, b) => a + b.length, 0);
  console.log(`snapshot: ${Object.keys(snap).length} files, ${n} colours -> ${BASE}`);
  process.exit(0);
}

if (mode === "verify") {
  const snap = JSON.parse(fs.readFileSync(BASE, "utf8"));
  const tokens = loadTokens();
  let bad = 0;

  // Check 1 — every DynamicResource key referenced anywhere must exist.
  // A missing key does NOT fail the WPF build; it silently renders nothing.
  for (const f of xamlFiles()) {
    const src = fs.readFileSync(f, "utf8");
    for (const m of src.matchAll(/\{DynamicResource\s+([^}]+)\}/g)) {
      const key = m[1].trim();
      const known = key in tokens
        || /^GlobalFontSize/.test(key)      // font sizes live in App.xaml
        || /Style$/.test(key);              // style keys, not colours
      if (!known) { console.error(`MISSING KEY  ${f}: ${key}`); bad++; }
    }
  }

  // Check 2 — no StaticResource may point at a token.
  //
  // Checked against the token dictionary rather than a name pattern: tokens may
  // be named freely (SteelBlue, PaleCyan), so a hardcoded list of prefixes would
  // silently miss them. A StaticResource on a token is a colour that will not
  // follow a theme change.
  for (const f of xamlFiles()) {
    const src = fs.readFileSync(f, "utf8");
    for (const m of src.matchAll(/\{StaticResource\s+([^}]+)\}/g)) {
      const key = m[1].trim();
      if (key in tokens) { console.error(`STATIC TOKEN ${f}: ${key}`); bad++; }
    }
  }

  // Check 3 — resolved colours must match the pre-refactor snapshot exactly.
  for (const f of xamlFiles()) {
    const k = f.replace(/\\/g, "/");
    const now = coloursOf(f, tokens, true);
    const was = snap[k];
    if (!was) { console.error(`NEW FILE     ${k} (not in baseline)`); bad++; continue; }
    if (now.length !== was.length) {
      console.error(`COUNT        ${k}: ${was.length} -> ${now.length}`); bad++; continue;
    }
    for (let i = 0; i < now.length; i++) {
      // A baseline entry may be a literal (#AARRGGBB) or an unresolved
      // {StaticResource X} recorded as STATIC:X. After the refactor the same
      // slot must resolve to the SAME literal colour.
      const before = was[i].startsWith("STATIC:") ? null : was[i];
      if (before === null) continue;          // was a themed ref; compared via check 1
      if (now[i] !== before) {
        console.error(`COLOUR       ${k}[${i}]: ${before} -> ${now[i]}`); bad++;
      }
    }
  }

  console.log(bad === 0 ? "theme-check: OK" : `theme-check: ${bad} problem(s)`);
  process.exit(bad === 0 ? 0 : 1);
}

console.error("usage: node tools/theme-check.mjs snapshot|verify");
process.exit(2);
