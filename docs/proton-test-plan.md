# Running Cryonic Overlay under Proton — first test

Goal: find out whether the existing Windows build is usable by Linux players
without porting anything. We are not trying to make it perfect, we are trying to
find out where it stops.

**The overlay must run inside the same Proton prefix as EVE.** A different prefix
is a different window namespace, so the overlay would find no EVE clients and the
log folder would be the wrong one. Everything below is about getting it into the
right prefix.

---

## 0. Before you start

You need:

- EVE Online installed through Steam and working under Proton
- The file `CryonicOverlay-v0.7.1-win-x64.exe`, saved somewhere simple such as
  `~/Downloads/`

Find EVE's Steam App ID — it is in the store page URL
(`store.steampowered.com/app/<ID>/`). It is almost certainly **8500**. Every
command below uses `8500`; change it if yours differs.

Install protontricks:

```bash
flatpak install flathub com.github.Matoking.protontricks
```

Or from your distro if it packages it (`sudo apt install protontricks`,
`sudo pacman -S protontricks`, …).

Confirm it can see EVE:

```bash
protontricks -l
```

If you installed the flatpak, every `protontricks…` command below becomes:

```bash
flatpak run --command=protontricks-launch com.github.Matoking.protontricks --appid 8500 <exe>
```

---

## Step 1 — Does it start at all?

This is the make-or-break. WPF is the one part nobody can predict from reading
the code. **Leave EVE closed for this step** — we only want to know whether the
window appears.

```bash
protontricks-launch --appid 8500 ~/Downloads/CryonicOverlay-v0.7.1-win-x64.exe
```

First launch is slow: it is a self-contained single-file build, so it unpacks
~70 MB into the prefix's temp folder before anything appears. Give it a minute.

**Three possible outcomes:**

- **A window appears** → excellent, go to Step 2.
- **Nothing appears, no error** → capture the output and send it (see Step 5).
- **It starts but the window is black, blank, or garbled** → WPF is failing to
  render through Direct3D. Try software rendering:

  ```bash
  protontricks 8500 regedit
  ```

  Navigate to `HKEY_CURRENT_USER\Software\Microsoft`, create a key
  `Avalon.Graphics`, and inside it a **DWORD** named `DisableHWAcceleration`
  set to `1`. Then run Step 1 again.

  If it is still broken, try adding the shader compiler:

  ```bash
  protontricks 8500 d3dcompiler_47
  ```

---

## Step 2 — Does it see your EVE clients?

1. Launch EVE from Steam as usual and log a character in.
2. **Leave EVE running**, then launch the overlay again with the Step 1 command.

Look at the **ACTIVE INSTANCES** section.

- A card with your character's window title → window detection works. This is
  the important one.
- Empty section → the overlay is in a different prefix than EVE, or
  `EnumWindows` is not seeing the game's window.

**The preview inside the card is expected to be an empty dark box.** Wine stubs
out the DWM thumbnail API the live previews are built on. I checked the code:
every one of those calls is guarded, so it should degrade to a blank box rather
than crash. If it *does* crash here, that is a genuine finding — tell me.

---

## Step 3 — Hotkeys and overlay behaviour

With EVE focused, try each one:

| Key | Should do |
| --- | --- |
| `Ctrl + Shift + O` | Overlay panel fades out, previews stay |
| `Ctrl + Shift + H` | Everything hides |
| `Ctrl + Shift + C` | Click-through toggles (footer switches Interactive ⇄ Click-through) |

Then check:

- Does the overlay stay **on top of EVE**, or does EVE cover it?
- With click-through on, do clicks reach the game underneath?
- Is EVE running **fullscreen** or **windowed**? Say which — fullscreen is much
  more likely to cover the overlay, and that difference matters a lot.

---

## Step 4 — Linking a character (optional, likely to fail)

Press the ⚙ Settings button and try **Link EVE character**. This opens your web
browser and runs a small local HTTP listener to catch the reply. Under Wine, the
browser hand-off often does not work.

If it fails, it is not a blocker for this test — the panels just stay empty. Note
what happened and move on.

---

## Step 5 — Send me the output

Run it once more with the output captured:

```bash
protontricks-launch --appid 8500 ~/Downloads/CryonicOverlay-v0.7.1-win-x64.exe \
  > ~/cryonic-proton.log 2>&1
```

Use the overlay for a moment, close it, then send me `~/cryonic-proton.log`.

Wine is noisy — `err:` and `fixme:` lines are normal and mostly harmless. The
interesting ones mention `dwmapi`, `d3d`, `mscoree`, `wpfgfx`, or any
**unhandled exception**.

---

## What to tell me

Even one line each is enough:

1. Did the window appear? (Step 1)
2. Did ACTIVE INSTANCES show your client? (Step 2)
3. Which hotkeys worked? (Step 3)
4. Did it stay above EVE — and was EVE fullscreen or windowed? (Step 3)
5. Anything that crashed outright.

Distro, GPU vendor, and whether you are on **X11 or Wayland** are worth adding.
Wayland is the one that could sink several of these on its own — check with:

```bash
echo $XDG_SESSION_TYPE
```

---

## My prediction, so we can see where I am wrong

- Steps 1–3 work, previews are blank boxes
- Always-on-top and click-through are the shaky parts, especially on Wayland or
  with EVE fullscreen
- Character linking fails

If Step 1 fails, that ends the Proton route and the web companion becomes the
real answer for Linux and Mac.
