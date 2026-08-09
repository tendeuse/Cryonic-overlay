# Running Cryonic Overlay under Proton — test plan v2 (v0.7.3, Wayland)

Goal: find out whether the Windows build is usable by Linux players without
porting anything. We are not trying to make it perfect, we are trying to find
out exactly where it stops.

**Round 1 (Pop!_OS / Wayland / GE-Proton 10-34, Aug 2026) got as far as: the app
runs, ESI works, the overlay does not appear over EVE.** This round exists to
find out *why* — there are two completely different causes and they need
different fixes.

---

## Read this first: what actually counts as evidence

The round-1 report listed character linking, location detection and "EVE data"
as passes and concluded the failure was overlay-specific. That conclusion does
not follow yet, because **all three of those go over the network, not through
EVE's window.**

Cryonic talks to EVE in two entirely separate ways:

| Path | Used for | Needs |
| --- | --- | --- |
| **ESI over HTTPS** | character link, location, skills, standings, orders | just internet |
| **Win32 window handles** | ACTIVE INSTANCES, anchoring, always-on-top | *same Proton prefix as EVE* |

The ESI path working tells us nothing about the window path. So the single most
important line in this whole document is Step 2, and it is one word: does
ACTIVE INSTANCES show a card, yes or no.

Related: Steam's own overlay and MangoHud work by injecting into the game
process. Cryonic is a separate top-level window. "Other overlays work" is not
evidence that this one can — it is a different mechanism.

---

## 0. Setup

You need EVE working under Proton, and `CryonicOverlay-v0.7.3-win-x64.exe` in
`~/Downloads/`.

EVE's Steam App ID is **8500**. Every command below uses it.

```bash
flatpak install flathub com.github.Matoking.protontricks
```

Confirm it can see EVE:

```bash
flatpak run --command=protontricks com.github.Matoking.protontricks -l
```

**If EVE is not listed** — likely with Flatpak Steam plus a library on a second
drive — grant access to the library path and try again:

```bash
flatpak override --user --filesystem=/path/to/your/SteamLibrary com.github.Matoking.protontricks
```

Record your session type; it changes what is even possible:

```bash
echo $XDG_SESSION_TYPE
```

---

## Step 1 — Launch it in EVE's prefix

**This is not optional and it is what round 1 skipped.** A normal Proton launch
creates its *own* prefix. A different prefix is a different Win32 desktop: the
overlay cannot see EVE's window, cannot anchor to it, and cannot stack against
it. It will still run and still do everything over ESI, which is exactly why
round 1 looked partly successful.

Start EVE from Steam first and log a character in. Leave it running. Then:

```bash
flatpak run --command=protontricks-launch com.github.Matoking.protontricks --appid 8500 ~/Downloads/CryonicOverlay-v0.7.3-win-x64.exe
```

First launch is slow — it is a self-contained single-file build and unpacks
~70 MB into the prefix before anything appears. Give it a minute.

> Needs ~200 MB free on the drive holding the prefix. If it exits immediately
> with an I/O or decompression error, check free space first — that exact error
> turned out to be a full disk on Windows, not a bug.

If the window is black, blank or garbled, WPF is failing through Direct3D:

```bash
flatpak run --command=protontricks com.github.Matoking.protontricks 8500 regedit
```

Under `HKEY_CURRENT_USER\Software\Microsoft`, create key `Avalon.Graphics`, and
inside it a DWORD `DisableHWAcceleration` = `1`. Relaunch. Still broken, try
`protontricks 8500 d3dcompiler_47`.

---

## Step 2 — THE decisive check

Look at **ACTIVE INSTANCES**.

- **A card with your character's window title** → window detection works, you
  are in the right prefix. Everything after this is a genuine Wayland stacking
  problem. Go to Step 3.
- **Empty** → still the wrong prefix, or `EnumWindows` cannot see the game.
  Steps 3–4 are meaningless in this state; skip to Step 5 and send the log.

Answer this one explicitly in your report even if it seems obvious.

**The preview inside the card is expected to be an empty dark box.** Wine stubs
out the DWM thumbnail API. Every one of those calls is guarded, so it should
degrade to a blank box. If it *crashes* here, that is a real finding.

---

## Step 3 — Stacking, tested per display mode

Only meaningful if Step 2 showed a card.

Round 1 did not record whether EVE was fullscreen. It matters more than anything
else here, so test all three. In EVE: Esc → Display & Graphics → Display Mode.

| EVE display mode | Overlay visible over EVE? | Click-through reaches game? |
| --- | --- | --- |
| Windowed | | |
| Borderless / windowed-fullscreen | | |
| Fullscreen | | |

Expect a gradient rather than pass/fail: windowed most likely to work, exclusive
fullscreen least. **If it works in any one of the three, say so** — that alone
decides whether Linux support is documentable-with-caveats or genuinely dead.

Then the hotkeys, with EVE focused:

| Key | Should do | Worked? |
| --- | --- | --- |
| `Ctrl + Shift + O` | Overlay panel fades, previews stay | |
| `Ctrl + Shift + H` | Everything hides | |
| `Ctrl + Shift + C` | Click-through toggles (footer shows Interactive ⇄ Click-through) | |

Global hotkeys go through `RegisterHotKey`, which is per-prefix — they may work
only while the overlay itself has focus, which would make them useless in
practice. Note which case you see.

**Do not run EVE under gamescope for this test.** Gamescope nests the game in
its own compositor; a separate window cannot be composited over it, and that
would mask whatever the real result is.

---

## Step 4 — Confirm character linking still works

Round 1 established this works, which contradicted my prediction. Just confirm
it survives in the correct prefix — the browser hand-off uses a local HTTP
listener, and prefix changes can affect it.

---

## Step 5 — Send the log

```bash
flatpak run --command=protontricks-launch com.github.Matoking.protontricks --appid 8500 ~/Downloads/CryonicOverlay-v0.7.3-win-x64.exe > ~/cryonic-proton.log 2>&1
```

Use it briefly, close it, send `~/cryonic-proton.log`.

Wine is noisy — `err:` and `fixme:` are mostly harmless. The interesting ones
mention `dwmapi`, `d3d`, `wpfgfx`, `winex11`, `SetWindowPos`, or any unhandled
exception.

---

## Report template

```
Session type (XDG_SESSION_TYPE):
Launched via protontricks-launch:      yes / no
Step 2 — ACTIVE INSTANCES card:        yes / no      <-- the important one
Step 3 — visible over EVE, windowed:   yes / no
Step 3 — visible over EVE, borderless: yes / no
Step 3 — visible over EVE, fullscreen: yes / no
Click-through reached the game:        yes / no / n-a
Hotkeys working (which, and only-when-focused?):
Character linking:                     yes / no
Anything that crashed outright:
```

---

## Predictions for round 2, so we can see where I am wrong

Round 1 scored me: I predicted character linking would fail. It did not.

- Step 2 shows a card once launched in the right prefix — this is the failure I
  expect round 1 actually hit
- Even then, exclusive fullscreen covers the overlay on Wayland and I do not
  expect to fix that from inside a WPF app
- Windowed or borderless is where it has a real chance
- Click-through is unlikely to work; it relies on `WS_EX_TRANSPARENT` hit-testing
  that XWayland handles differently

Known weak points in our own code, independent of the above: `Topmost` is set
once in XAML and never re-asserted, and the anchoring call deliberately passes
`SWP_NOZORDER` so it never raises z-order. On Windows that is fine. If Step 3
shows the overlay dropping *behind* EVE after a focus change rather than never
appearing at all, that distinction is the thing to fix, and it is fixable.

If Step 2 is empty even via protontricks-launch, the Proton route is finished
and the web companion becomes the real answer for Linux and Mac.
