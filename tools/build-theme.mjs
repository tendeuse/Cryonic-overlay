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

/**
 * Find the control panel's css directory.
 *
 * Searched rather than hard-coded, because the overlay is checked out at
 * varying depths -- a git worktree puts it three levels below the directory
 * the panel is a sibling of, so a fixed "../cryonic-panel" silently skipped
 * the CSS output and the skin quietly stopped being one source with two
 * outputs.
 *
 * Returns null when the panel is not on disk at all, which is legitimate:
 * someone may have only the overlay repo.
 */
function findPanelCss() {
  if (process.env.PANEL_CSS_DIR) return process.env.PANEL_CSS_DIR;
  let dir = path.resolve(".");
  for (let i = 0; i < 6; i++) {
    const candidate = path.join(dir, "cryonic-panel", "css");
    if (fs.existsSync(candidate)) return candidate;
    const parent = path.dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  return null;
}

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

    // Two forms. `from`/`to` is the common two-stop case; `stops` is the
    // general one, needed because a specular highlight is not a fade -- it
    // needs a tight band that arrives and leaves quickly, which two stops
    // spread across the whole surface cannot express.
    const stops = g.stops ?? [
      { colour: g.from, alpha: g.fromAlpha, offset: 0 },
      { colour: g.to,   alpha: g.toAlpha,   offset: 1 },
    ];
    const stopXml = stops.map(
      (s) => `        <GradientStop Offset="${s.offset}" Color="${stop(s.colour, s.alpha)}"/>`
    );

    if (g.kind === "radial") {
      // A RADIAL brush is what makes a highlight read as a REFLECTION rather
      // than paint: its falloff is a curve, and a curved edge is the thing the
      // eye uses to tell a glossy surface from a printed gradient.
      const c = g.center ?? "0.5,0";
      block.push(
        "",
        `    <RadialGradientBrush x:Key="${key}" Center="${c}" GradientOrigin="${g.origin ?? c}"` +
        ` RadiusX="${g.radiusX ?? 0.9}" RadiusY="${g.radiusY ?? 0.7}">`,
        ...stopXml,
        `    </RadialGradientBrush>`
      );
    } else {
      // angle 45 gives the diagonal a reflected light source actually makes.
      const [x2, y2] = g.angle === 0 ? [1, 0] : g.angle === 45 ? [1, 1] : [0, 1];
      block.push(
        "",
        `    <LinearGradientBrush x:Key="${key}" StartPoint="0,0" EndPoint="${x2},${y2}">`,
        ...stopXml,
        `    </LinearGradientBrush>`
      );
    }
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

// ── inversion ──────────────────────────────────────────────────────────────
//
// "The accent becomes the main and the main becomes the accent."
//
// Implemented as a HUE swap, not a value swap. Swapping the values outright
// would put a bright accent behind body text and a near-black on the emphasis,
// which is unreadable. Instead each colour KEEPS ITS LIGHTNESS — so contrast
// ratios survive — and only the hue and saturation trade places. The result
// reads as the same console rebuilt in the other colour.

function hexToHsl(hex) {
  const r = parseInt(hex.slice(0, 2), 16) / 255;
  const g = parseInt(hex.slice(2, 4), 16) / 255;
  const b = parseInt(hex.slice(4, 6), 16) / 255;
  const max = Math.max(r, g, b), min = Math.min(r, g, b);
  const l = (max + min) / 2;
  let h = 0, sat = 0;
  if (max !== min) {
    const d = max - min;
    sat = l > 0.5 ? d / (2 - max - min) : d / (max + min);
    if (max === r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6;
    else if (max === g) h = ((b - r) / d + 2) / 6;
    else h = ((r - g) / d + 4) / 6;
  }
  return [h, sat, l];
}

function hslToHex(h, s, l) {
  const f = (n) => {
    const k = (n + h * 12) % 12;
    const a = s * Math.min(l, 1 - l);
    const v = l - a * Math.max(-1, Math.min(k - 3, Math.min(9 - k, 1)));
    return Math.round(v * 255).toString(16).padStart(2, "0").toUpperCase();
  };
  return f(0) + f(8) + f(4);
}

/** Re-hue `hex` onto the hue and saturation of `donor`, keeping its lightness. */
function reHue(hex, donor) {
  const [, , l] = hexToHsl(hex);
  const [dh, ds] = hexToHsl(donor);
  return hslToHex(dh, ds, l);
}

/**
 * Surfaces take the accent's hue; the accent takes the surfaces'.
 *
 * Everything else is left alone. Red, amber and green are SEMANTIC — an alert
 * must look like an alert in every skin — and the glass and key families are
 * derived from the surfaces, so they follow automatically by being re-hued
 * with them.
 */
const INVERT_TO_ACCENT = [
  "Bg", "Panel", "Well", "Surface", "Onyx", "DeepInk", "DeepNavy", "GunMetal",
  "Charcoal", "Graphite", "Obsidian", "InkBlue", "Abyss", "TwilightBlue",
  "Border", "BorderStrong", "SteelBlue", "DuskBlue", "Indigo",
  "ComboBg", "ComboBorder", "ComboItemBg", "ComboHover", "ComboSelect", "ComboEdge",
  "KeyHi", "KeyLo", "KeyBorder", "KeyEdge",
  "GlassTop", "GlassBottom", "GlassBorder",
];
const INVERT_TO_SURFACE = ["Accent", "Gray", "AccentKeyHi", "AccentKeyLo", "AquaBlue"];

function invert(palette) {
  const c = { ...palette.colours };
  const accent  = c.Accent;
  const surface = c.Bg;
  if (!accent || !surface) throw new Error(`palette "${palette.name}" needs Accent and Bg to invert`);

  for (const k of INVERT_TO_ACCENT)  if (c[k]) c[k] = reHue(c[k], accent);
  for (const k of INVERT_TO_SURFACE) if (c[k]) c[k] = reHue(c[k], surface);

  return { ...palette, name: palette.name + " Inverted", colours: c };
}

const slug  = (s) => s.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
const pascal = (s) => s.replace(/[^A-Za-z0-9]+/g, " ").trim().split(/\s+/)
  .map((w) => w[0].toUpperCase() + w.slice(1)).join("");

function build(paletteFile, doInvert = false) {
  let palette = JSON.parse(fs.readFileSync(paletteFile, "utf8"));
  if (doInvert) palette = invert(palette);
  const name    = pascal(palette.name);

  const xamlPath = path.join(THEMES, `Tokens.${name}.xaml`);
  fs.writeFileSync(xamlPath, buildXaml(palette));
  console.log(`build: ${xamlPath}`);

  const panelCss = findPanelCss();
  if (panelCss) {
    const cssPath = path.join(panelCss, `skin-${slug(palette.name)}.css`);
    fs.writeFileSync(cssPath, buildCss(palette));
    console.log(`build: ${cssPath}`);
  } else {
    // Loud, not silent. A skipped CSS output means the panel and the overlay
    // have drifted apart, which is exactly what this generator exists to stop.
    console.log("build: panel css SKIPPED — cryonic-panel/css not found (set PANEL_CSS_DIR)");
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
else if (mode === "build" && arg) build(arg, process.argv.includes("--invert"));
else {
  console.error("usage: node tools/build-theme.mjs extract | build <palette.json> [--invert] | verify");
  process.exit(2);
}
