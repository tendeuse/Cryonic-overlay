// Skin completeness check.
//
// A skin is a PAIR of dictionaries and ThemeManager swaps both wholesale. That
// makes two silent failure modes possible, neither of which WPF reports:
//
//   1. A style the default defines and the skin does not. The control loses its
//      appearance entirely -- no exception, no warning, it just renders bare.
//   2. A token a skin's styles reference but its palette does not define. A
//      DynamicResource that resolves to nothing paints nothing, so the control
//      is invisible rather than wrong-coloured.
//
// Both look like "the skin is broken" long after the change that caused them.
// This turns them into a build-time error.
import fs from "node:fs";
import path from "node:path";

const THEMES  = path.join("OverlayMVP", "Themes");
const BASE    = "Styles.Default.xaml";

const read = (f) => fs.readFileSync(path.join(THEMES, f), "utf8");
const all  = (src, re) => [...src.matchAll(re)].map((m) => m[1]);

/** Keyed styles and resources: x:Key="Foo". */
const keysOf = (src) => new Set(all(src, /x:Key="([^"]+)"/g));

/** Implicit styles: <Style TargetType="Button"> with no key. */
const targetsOf = (src) =>
  new Set(
    [...src.matchAll(/<Style\b([^>]*)>/g)]
      .filter((m) => !/x:Key=/.test(m[1]))
      .map((m) => m[1].match(/TargetType="(?:\{x:Type\s+)?([^"}\s]+)/)?.[1])
      .filter(Boolean)
  );

const refsOf = (src) => new Set(all(src, /\{DynamicResource\s+([^}]+)\}/g).map((s) => s.trim()));

// Not colours: font sizes live in App.xaml, and a *Style suffix is a style key.
const isColourRef = (k) => !/^GlobalFontSize/.test(k) && !/Style$/.test(k);

/**
 * XAML forbids "--" inside a comment, and the compiler reports it as an opaque
 * MC3000 with a line number and no hint about which of the many dashes on that
 * line is illegal. It broke this build five times in one sitting, always while
 * writing prose, so it is checked here rather than left to discipline.
 */
function commentDashes(file, src) {
  const bad = [];
  for (const m of src.matchAll(/<!--([\s\S]*?)-->/g)) {
    if (!m[1].includes("--")) continue;
    const line = src.slice(0, m.index).split(/\r?\n/).length;
    bad.push(`${file}:${line}: "--" inside an XML comment (use an em dash or a semicolon)`);
  }
  // An unterminated comment: "-->" replaced by something else, which is the
  // over-correction that follows from fixing the above carelessly.
  const opens = (src.match(/<!--/g) ?? []).length;
  const closes = (src.match(/-->/g) ?? []).length;
  if (opens !== closes) bad.push(`${file}: ${opens} comment opens but ${closes} closes`);
  return bad;
}

function main() {
  const skins = fs.readdirSync(THEMES)
    .filter((f) => /^Styles\..+\.xaml$/.test(f) && f !== BASE);

  const baseSrc  = read(BASE);
  const baseKeys = keysOf(baseSrc);
  const baseTgts = targetsOf(baseSrc);

  let bad = 0;

  for (const f of fs.readdirSync(THEMES).filter((n) => n.endsWith(".xaml"))) {
    for (const problem of commentDashes(f, read(f))) { console.error(`XAML  ${problem}`); bad++; }
  }

  for (const styleFile of skins) {
    const src  = read(styleFile);
    const keys = keysOf(src);
    const tgts = targetsOf(src);

    for (const k of baseKeys) {
      if (!keys.has(k)) { console.error(`MISSING KEY     ${styleFile}: ${k} (defined in ${BASE})`); bad++; }
    }
    for (const t of baseTgts) {
      if (!tgts.has(t)) { console.error(`MISSING STYLE   ${styleFile}: implicit style for ${t}`); bad++; }
    }

    // Which palettes pair with this component layer. "Styles.Cockpit.xaml"
    // is used by every skin whose Styles is "Cockpit", so its references must
    // resolve against ALL of them -- checking only one would let a token that
    // exists in Caldari Navy but not in Hangar Deck through.
    const layer  = styleFile.replace(/^Styles\.|\.xaml$/g, "");
    const tokens = fs.readdirSync(THEMES).filter((f) => /^Tokens\..+\.xaml$/.test(f));
    const paired = layer === "Default"
      ? tokens.filter((f) => f === "Tokens.Default.xaml")
      : tokens.filter((f) => f !== "Tokens.Default.xaml");

    if (!paired.length) {
      console.error(`NO PALETTE      ${styleFile}: no token dictionary pairs with it`);
      bad++;
      continue;
    }

    for (const tokenFile of paired) {
      const have = keysOf(read(tokenFile));
      for (const ref of refsOf(src)) {
        if (isColourRef(ref) && !have.has(ref)) {
          console.error(`UNRESOLVED      ${styleFile} -> ${tokenFile}: ${ref}`);
          bad++;
        }
      }
    }
  }

  console.log(bad === 0 ? `skin-check: OK (${skins.length} skin layer(s))` : `skin-check: ${bad} problem(s)`);
  process.exit(bad === 0 ? 0 : 1);
}

main();
