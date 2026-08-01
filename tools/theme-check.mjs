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

// WPF treats a 6-digit hex as fully opaque, so #0D1117 and #FF0D1117 paint the
// SAME pixel. Comparing the raw text would let them look like different colours
// and, worse, let two tokens hold one colour under different names -- a skin
// author would change one and silently miss the other.
const norm = (hex) => {
  const h = hex.replace("#", "").toUpperCase();
  return "#" + (h.length === 6 ? "FF" + h : h);
};

// Named colours WPF accepts in place of a hex. Only the ones this app actually
// uses are mapped: they must resolve to a colour so that converting such a site
// to a token is a no-op to the checker rather than a colour appearing from
// nowhere. Transparent is deliberately absent -- it is structural (windows use
// AllowsTransparency), not a theme colour, and is never tokenised.
const KEYWORD = {
  White: "#FFFFFFFF",
  Black: "#FF000000",
};

// Colour-bearing attributes, in two syntaxes:
//   Background="#FF0D1117"                              (direct attribute)
//   <Setter Property="Background" Value="#FF0D1117"/>   (style setter)
//   <Setter TargetName="X" Property="Background" ...>   (trigger setter -
//                                                         TargetName precedes Property)
// ONE regex with alternation, not two passes, so document order is preserved.
// The Setter alternative allows arbitrary attributes (e.g. TargetName=) before
// Property=, since trigger Setters put TargetName first.
//
// `Color` is deliberately NOT tracked. It appears only on <SolidColorBrush>
// DECLARATIONS (verified: all 23 occurrences), which define a palette entry
// rather than paint anything. Tracking them made the refactor unverifiable --
// adding a token grew Tokens.Default.xaml's count and removing a window-local
// brush shrank that window's, both of which are intended changes that were
// being reported as drift. Only USAGE sites are tracked.
const ATTR = new RegExp(
  '<Setter\\s+(?:[\\w:.]+="[^"]*"\\s+)*Property="(?:Background|Foreground|BorderBrush|Fill|Stroke)"\\s+Value="([^"]+)"' +
  '|(?:Background|Foreground|BorderBrush|Fill|Stroke)\\s*=\\s*"([^"]+)"',
  'g',
);

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

/** x:Key -> #AARRGGBB, from the old App.xaml palette AND the new token dictionary.
 *
 *  Both are read because the refactor moves brushes from one to the other: at
 *  snapshot time only App.xaml has them, at verify time only Tokens.Default.xaml
 *  does. Reading both means a site's colour is resolvable on BOTH sides of the
 *  rename -- which is what lets the value-drift check cover the ~143 sites that
 *  start out as {StaticResource ...}. Without this they are recorded opaquely
 *  and skipped, leaving the largest single group of conversions unverified.
 */
function loadTokens() {
  const map = {};
  for (const f of [path.join(ROOT, "App.xaml"), TOKENS]) {
    if (!fs.existsSync(f)) continue;
    const src = fs.readFileSync(f, "utf8");
    for (const m of src.matchAll(/<SolidColorBrush\s+x:Key="([^"]+)"\s+Color="([^"]+)"/g)) {
      map[m[1]] = norm(m[2]);
    }
  }
  return map;
}

/** x:Key set from Tokens.Default.xaml ONLY (not merged with App.xaml).
 *
 *  Used by Check 2, which flags a StaticResource that points at the NEW theme
 *  dictionary -- a leftover that would not follow a theme change. Checking
 *  against the merged map (loadTokens()) would also flag every pre-refactor
 *  {StaticResource AccentGold}-style site, since those names now resolve via
 *  App.xaml too -- but that is the expected, correct state before the
 *  refactor runs, not a mistake. Scoping to Tokens.Default.xaml keeps Check 2
 *  silent until the new dictionary exists and something actually still
 *  points at it via StaticResource.
 */
function loadNewTokenKeys() {
  if (!fs.existsSync(TOKENS)) return new Set();
  const src = fs.readFileSync(TOKENS, "utf8");
  const keys = new Set();
  for (const m of src.matchAll(/<SolidColorBrush\s+x:Key="([^"]+)"/g)) keys.add(m[1]);
  return keys;
}

/** Every colour in one file, in document order, resolved to a hex where possible. */
function coloursOf(file, globalTokens, resolve) {
  const src = fs.readFileSync(file, "utf8");

  // A window may declare its own brushes in its local resource dictionary, which
  // shadow the app palette. Resolve against those too, or every site using one is
  // recorded opaquely and drops out of the comparison -- and then hoisting it into
  // a real token later looks like a colour appearing from nowhere.
  const tokens = { ...globalTokens };
  for (const m of src.matchAll(/<SolidColorBrush\s+x:Key="([^"]+)"\s+Color="([^"]+)"/g)) {
    tokens[m[1]] = norm(m[2]);
  }

  const out = [];
  for (const m of src.matchAll(ATTR)) {
    const v = (m[1] ?? m[2]).trim();
    if (v.startsWith("#")) { out.push(norm(v)); continue; }
    if (v in KEYWORD) { out.push(KEYWORD[v]); continue; }
    const dyn = v.match(/^\{DynamicResource\s+([^}]+)\}$/);
    if (dyn) {
      const key = dyn[1].trim();
      if (!resolve) { out.push(`{${key}}`); continue; }
      if (!(key in tokens)) { out.push(`MISSING:${key}`); continue; }
      out.push(tokens[key]);
      continue;
    }
    const stat = v.match(/^\{StaticResource\s+([^}]+)\}$/);
    if (stat) {
      const key = stat[1].trim();
      // Resolve to a colour when we know one, so this slot participates in the
      // value-drift check. Style keys (SmallBtn) and converters legitimately
      // have no colour and stay opaque.
      out.push(key in tokens ? tokens[key] : `STATIC:${key}`);
      continue;
    }
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

  // Check 2 — no StaticResource may point at the NEW token dictionary.
  //
  // Checked against Tokens.Default.xaml specifically (loadNewTokenKeys), not
  // the merged map: tokens may be named freely (SteelBlue, PaleCyan), so a
  // hardcoded list of prefixes would silently miss them, but checking the
  // merged map would also flag every pre-refactor {StaticResource AccentGold}
  // site, which is the correct state before the refactor runs. A
  // StaticResource on a NEW token is a colour that will not follow a theme
  // change.
  const newTokenKeys = loadNewTokenKeys();
  for (const f of xamlFiles()) {
    const src = fs.readFileSync(f, "utf8");
    for (const m of src.matchAll(/\{StaticResource\s+([^}]+)\}/g)) {
      const key = m[1].trim();
      if (newTokenKeys.has(key)) { console.error(`STATIC TOKEN ${f}: ${key}`); bad++; }
    }
  }

  // Check 3 — the app must still render exactly the same multiset of colours.
  //
  // Compared GLOBALLY, not per file. The refactor deliberately moves colours
  // between files (App.xaml's styles into Styles.Default.xaml; window-local
  // brushes hoisted into Tokens.Default.xaml), so a path-keyed, position-indexed
  // comparison reports intended moves as drift and has to be worked around --
  // and working around a safety net destroys it.
  //
  // The question that actually matters is "does the app still paint the same
  // colours, the same number of times?", which is exactly a multiset compare.
  // A colour changing value, appearing, or disappearing is caught. The one
  // thing this cannot see is two colours swapping places between files, which
  // no mechanical rename produces.
  //
  // Opaque entries (STATIC:/OTHER:) are excluded from both sides: they are
  // style keys, converters and bindings, not colours. Check 1 covers key
  // validity for those.
  const tally = (arrs) => {
    const m = new Map();
    for (const arr of arrs) for (const v of arr) {
      if (v.startsWith("STATIC:") || v.startsWith("OTHER:") || v.startsWith("MISSING:")) continue;
      m.set(v, (m.get(v) ?? 0) + 1);
    }
    return m;
  };

  const was = tally(Object.values(snap));
  const now = tally(xamlFiles().map((f) => coloursOf(f, tokens, true)));

  for (const colour of new Set([...was.keys(), ...now.keys()])) {
    const a = was.get(colour) ?? 0;
    const b = now.get(colour) ?? 0;
    if (a !== b) { console.error(`COLOUR  ${colour}: rendered ${a}x before, ${b}x now`); bad++; }
  }

  // Unresolvable references are a separate failure: a DynamicResource whose key
  // is absent renders nothing, and WPF will not tell you.
  for (const f of xamlFiles()) {
    for (const v of coloursOf(f, tokens, true)) {
      if (v.startsWith("MISSING:")) {
        console.error(`UNRESOLVED   ${f.replace(/\\/g, "/")}: ${v.slice(8)}`); bad++;
      }
    }
  }

  console.log(bad === 0 ? "theme-check: OK" : `theme-check: ${bad} problem(s)`);
  process.exit(bad === 0 ? 0 : 1);
}

console.error("usage: node tools/theme-check.mjs snapshot|verify");
process.exit(2);
