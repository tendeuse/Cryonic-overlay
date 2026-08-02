// Skin generator. One palette in, two outputs: the overlay's token dictionary
// and the control panel's :root block.
//
//   extract          — derive skins/default.json FROM Tokens.Default.xaml
//   build <palette>  — write Themes/Tokens.<Name>.xaml + panel CSS
//   verify           — build default.json and assert byte-identity with
//                      Tokens.Default.xaml
//
// WHY THIS IS A TRANSFORMER, NOT AN EMITTER
//
// It reads Tokens.Default.xaml and substitutes colours line by line, keeping
// every comment, blank line and column of padding. Emitting the file from
// scratch would mean re-deriving all that formatting, and the byte-identity
// proof would then be a test of the formatter rather than of the colours.
// Here identity is structural: if a line has no colour, it cannot change.
//
// The consequence worth knowing: the DEFAULT file defines the shape of every
// skin. A skin cannot add or reorder the 84 tokens, only recolour them (it may
// append extra ones — see EXTRAS below). That is deliberate. A skin that could
// drop a token would render some control invisible with no error.
import fs from "node:fs";
import path from "node:path";

const ROOT      = "OverlayMVP";
const THEMES    = path.join(ROOT, "Themes");
const DEFAULT   = path.join(THEMES, "Tokens.Default.xaml");
const SKINS     = "skins";
const PANEL_CSS = path.join("..", "cryonic-panel", "css");

// Bases that have no token of their own but that alpha variants derive from.
// Gray22/Gray88 exist; "Gray" does not. Without this list the splitter cannot
// tell "Gray22" (family) from a standalone name that happens to end in hex.
const IMPLIED_BASES = ["Gray", "Emerald", "Indigo"];

const COLOUR_RE = /(<SolidColorBrush\s+x:Key="([A-Za-z0-9]+)"\s+Color=")(#[0-9A-Fa-f]{6,8})(")/;

// ── parsing ────────────────────────────────────────────────────────────────

/** Every (key, colour) in document order. */
function tokensOf(src) {
  const out = [];
  for (const line of src.split(/\r?\n/)) {
    const m = line.match(COLOUR_RE);
    if (m) out.push({ key: m[2], colour: normalise(m[3]) });
  }
  return out;
}

/** #RGB forms in this codebase are 6 or 8 digits; normalise to AARRGGBB. */
function normalise(hex) {
  const h = hex.slice(1).toUpperCase();
  return h.length === 6 ? "FF" + h : h;
}

/**
 * Split "Accent33" into { base: "Accent", alpha: "33" }, or null if the key is
 * a base in its own right. Requires the prefix to be a real key (or an implied
 * base) so a standalone name ending in hex digits is never mis-split.
 */
function splitFamily(key, keySet) {
  const m = key.match(/^([A-Za-z]+)([0-9A-F]{2})$/);
  if (!m) return null;
  const [, base, alpha] = m;
  if (keySet.has(base) || IMPLIED_BASES.includes(base)) return { base, alpha };
  return null;
}

// ── extract ────────────────────────────────────────────────────────────────

function extract() {
  const src    = fs.readFileSync(DEFAULT, "utf8");
  const tokens = tokensOf(src);
  const keySet = new Set(tokens.map((t) => t.key));

  const colours = {};
  for (const { key, colour } of tokens) {
    const fam = splitFamily(key, keySet);
    // An author writes RGB only. Alpha is structural -- it says how transparent
    // that surface is, which is layout, not palette. Skins inherit it.
    if (fam) colours[fam.base] ??= colour.slice(2);
    else     colours[key]        = colour.slice(2);
  }

  const palette = {
    name: "Default",
    description: "Today's overlay colours. The shape every other skin follows.",
    colours: Object.fromEntries(Object.keys(colours).sort().map((k) => [k, colours[k]])),
  };

  fs.mkdirSync(SKINS, { recursive: true });
  const out = path.join(SKINS, "default.json");
  fs.writeFileSync(out, JSON.stringify(palette, null, 2) + "\n");
  console.log(`extract: ${Object.keys(colours).length} colours -> ${out}`);
}

// ── build ──────────────────────────────────────────────────────────────────

function buildXaml(palette) {
  const src    = fs.readFileSync(DEFAULT, "utf8");
  const keySet = new Set(tokensOf(src).map((t) => t.key));
  const missing = [];
  // Round-trip the source's own line ending. Rewriting CRLF as LF renders
  // identically and diffs as "every line changed", which would bury a real
  // colour mistake in noise.
  const eol = src.includes("\r\n") ? "\r\n" : "\n";

  const lines = src.split(/\r?\n/).map((line) => {
    const m = line.match(COLOUR_RE);
    if (!m) return line;

    const key = m[2];
    const old = normalise(m[3]);
    const fam = splitFamily(key, keySet);
    const base = fam ? fam.base : key;
    // Alpha comes from the default file, never from the palette.
    const alpha = fam ? fam.alpha : old.slice(0, 2);

    const rgb = palette.colours[base];
    if (!rgb) { missing.push(base); return line; }

    // Two default tokens are written #RRGGBB with no alpha. Re-emitting them as
    // #FFRRGGBB renders identically but is not the same bytes, and byte-identity
    // is the whole proof -- so keep whichever notation the source used.
    const wroteAlpha = m[3].length > 7;
    const value = wroteAlpha || alpha !== "FF"
      ? `#${alpha}${rgb.toUpperCase()}`
      : `#${rgb.toUpperCase()}`;

    return line.replace(COLOUR_RE, `$1${value}$4`);
  });

  if (missing.length) {
    throw new Error(
      `palette "${palette.name}" is missing ${[...new Set(missing)].length} colour(s): ` +
      [...new Set(missing)].join(", ")
    );
  }

  // EXTRAS: colours a skin defines that the default has no token for -- the key
  // bevels, glass and hazard families the cockpit needs. Appended rather than
  // interleaved so the first 84 stay positionally identical to the default and
  // a diff between two skins shows only colour changes.
  const extras = Object.keys(palette.colours)
    .filter((k) => !keySet.has(k) && !IMPLIED_BASES.includes(k))
    .sort();

  const block = [];

  if (extras.length) {
    const width = Math.max(...extras.map((k) => k.length));
    block.push(
      "",
      "    <!-- Skin-specific tokens. No counterpart in the default theme: these",
      "         exist only for skins whose component layer draws things the",
      "         default one does not (key bevels, glass, hazard striping). -->",
      ...extras.map(
        (k) => `    <SolidColorBrush x:Key="${k}"${" ".repeat(width - k.length)} Color="#FF${palette.colours[k].toUpperCase()}"/>`
      )
    );
  }

  // GRADIENTS are composed, not authored. A gradient names colours the palette
  // already declares, so the "colours only" rule holds -- no new colour value
  // can enter through this door, and a recoloured skin's gradients recolour
  // with it automatically.
  //
  // They exist because a WPF GradientStop takes a Color, not a Brush, so it
  // cannot reference a SolidColorBrush token at all. Building the brush here is
  // the only way a skin gets real depth without hand-writing hex into XAML.
  //
  // The default palette declares none, which is what keeps Tokens.Default.xaml
  // byte-identical.
  for (const [key, g] of Object.entries(palette.gradients ?? {})) {
    // A stop may carry an alpha. That is not a new colour -- it is how opaque
    // this stop is, the same structural role alpha plays on a token -- and it
    // is what lets a skin describe a specular sheen that fades to nothing
    // rather than to a specific colour.
    const stop = (name, alpha) => {
      const rgb = palette.colours[name];
      if (!rgb) throw new Error(`gradient "${key}" references unknown colour "${name}"`);
      return `#${(alpha ?? "FF").toUpperCase()}${rgb.toUpperCase()}`;
    };
    // angle 90 = top-to-bottom, the only direction the cockpit reference uses.
    const [x2, y2] = g.angle === 0 ? [1, 0] : [0, 1];
    block.push(
      "",
      `    <LinearGradientBrush x:Key="${key}" StartPoint="0,0" EndPoint="${x2},${y2}">`,
      `        <GradientStop Offset="0" Color="${stop(g.from, g.fromAlpha)}"/>`,
      `        <GradientStop Offset="1" Color="${stop(g.to, g.toAlpha)}"/>`,
      `    </LinearGradientBrush>`
    );
  }

  if (block.length) {
    const close = lines.lastIndexOf("</ResourceDictionary>");
    lines.splice(close, 0, ...block);
  }

  return lines.join(eol);
}

/**
 * The panel's :root block. Its 16 custom properties are named independently of
 * the overlay's tokens, so the correspondence is written down here rather than
 * left to coincidence -- six already match byte-for-byte, the rest are a
 * judgement call recorded once.
 */
const PANEL_MAP = {
  "--bg":          "Bg",
  "--bg-panel":    "Panel",
  "--bg-sunk":     "Well",
  "--border":      "Border",
  "--border-soft": "ComboItemBg",
  "--accent":      "Accent",
  "--gold":        "Amber",
  "--red":         "Red",
  "--green":       "Green",
  "--text":        "Text",
  "--muted":       "TextDim",
};
// Alpha-composited properties: CSS rgba() rather than a flat hex.
const PANEL_ALPHA = {
  "--accent-dim": ["Accent", 0.14],
  "--red-dim":    ["Red",    0.14],
};

function buildCss(palette) {
  const hex = (name) => {
    const v = palette.colours[name];
    if (!v) throw new Error(`palette "${palette.name}" has no colour "${name}" (needed by the panel)`);
    return "#" + v.toUpperCase();
  };
  const rgba = (name, a) => {
    const v = hex(name).slice(1);
    const [r, g, b] = [0, 2, 4].map((i) => parseInt(v.slice(i, i + 2), 16));
    return `rgba(${r},${g},${b},${a})`;
  };

  const rows = [
    ...Object.entries(PANEL_MAP).map(([k, t]) => [k, hex(t)]),
    ...Object.entries(PANEL_ALPHA).map(([k, [t, a]]) => [k, rgba(t, a)]),
  ];
  const width = Math.max(...rows.map(([k]) => k.length));

  return [
    `/* Generated by tools/build-theme.mjs from skins/${slug(palette.name)}.json -- do not edit.`,
    `   Skin: ${palette.name}`,
    ``,
    `   Only colours are generated. Fonts and the --tick HUD metric are not`,
    `   palette data and stay in panel.css. */`,
    `:root {`,
    ...rows.map(([k, v]) => `  ${k}:${" ".repeat(width - k.length)} ${v};`),
    `}`,
    ``,
  ].join("\n");
}

const slug  = (s) => s.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
const pascal = (s) => s.replace(/[^A-Za-z0-9]+/g, " ").trim().split(/\s+/)
  .map((w) => w[0].toUpperCase() + w.slice(1)).join("");

function build(paletteFile) {
  const palette = JSON.parse(fs.readFileSync(paletteFile, "utf8"));
  const name    = pascal(palette.name);

  const xamlPath = path.join(THEMES, `Tokens.${name}.xaml`);
  fs.writeFileSync(xamlPath, buildXaml(palette));
  console.log(`build: ${xamlPath}`);

  if (fs.existsSync(PANEL_CSS)) {
    const cssPath = path.join(PANEL_CSS, `skin-${slug(palette.name)}.css`);
    fs.writeFileSync(cssPath, buildCss(palette));
    console.log(`build: ${cssPath}`);
  } else {
    console.log(`build: panel css skipped (${PANEL_CSS} not found)`);
  }
}

// ── verify ─────────────────────────────────────────────────────────────────
//
// The proof the whole generator rests on. If rebuilding the default palette
// reproduces Tokens.Default.xaml exactly, then the transformation is colour-
// preserving, and every other skin differs from the default by colour alone.

function verify() {
  const paletteFile = path.join(SKINS, "default.json");
  if (!fs.existsSync(paletteFile)) {
    console.error(`verify: ${paletteFile} missing -- run "extract" first`);
    process.exit(2);
  }
  const palette = JSON.parse(fs.readFileSync(paletteFile, "utf8"));
  const rebuilt = buildXaml(palette);
  const actual  = fs.readFileSync(DEFAULT, "utf8");

  if (rebuilt === actual) { console.log("build-theme: default reproduced byte-for-byte"); return; }

  const a = actual.split(/\r?\n/), b = rebuilt.split(/\r?\n/);
  for (let i = 0; i < Math.max(a.length, b.length); i++) {
    if (a[i] !== b[i]) {
      console.error(`build-theme: MISMATCH at line ${i + 1}`);
      console.error(`  expected: ${a[i]}`);
      console.error(`  rebuilt : ${b[i]}`);
      process.exit(1);
    }
  }
  console.error("build-theme: MISMATCH (line count differs)");
  process.exit(1);
}

// ── cli ────────────────────────────────────────────────────────────────────

const [mode, arg] = process.argv.slice(2);
if (mode === "extract")      extract();
else if (mode === "verify")  verify();
else if (mode === "build" && arg) build(arg);
else {
  console.error("usage: node tools/build-theme.mjs extract | build <palette.json> | verify");
  process.exit(2);
}
