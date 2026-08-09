# Cryonic Overlay v0.7.3 — Beta

The French release. Plus the overlay now plays by CCP's current API rules.

## Français

**The whole app is translated now, not just three lines of it.**

The language button was only ever wired to three strings, so switching to French
changed two headings and left everything else in English. Every window is now
translated — around 250 strings across 18 windows.

**Your language choice is remembered.** It used to reset to English every single
launch, so French pilots re-pressed the button every time they opened the
overlay.

**One tooltip was in French for English users** — the settings gear said
*Paramètres* to everyone. Fixed, along with the five beside it.

Every French term was checked against the EVE client itself, so the wording
matches what you see in game: *Relations*, *Points de loyauté*, *État-major de
la Mordu's Legion*. EVE jargon that French players say in English stays in
English — a gate camp is a **gate camp**, not a *camp de gate*.

## Under the hood — ESI

The overlay was still using API conventions CCP have moved on from. Now current:

- **Rate limiting is handled.** CCP introduced it in late 2025 and the overlay
  had no idea. It now backs off properly when told to, across every request it
  makes rather than one at a time.
- **It identifies itself properly.** The main API client sent no User-Agent at
  all; the others announced stale versions.
- **Versionless routes with a pinned compatibility date**, the mechanism CCP
  replaced `/latest/` with.

Nothing you will see, but it is the difference between a well-behaved
third-party tool and one that gets throttled.

## Hotkeys

| Key | Does |
| --- | --- |
| `Ctrl + Shift + O` | Hide the overlay panel — instance previews stay visible |
| `Ctrl + Shift + C` | Toggle click-through |
| `Ctrl + Shift + I` | Report roaming gang |
| `Ctrl + Shift + H` | Hide everything — previews and pop-outs included |

## Install

Download `CryonicOverlay-v0.7.3-win-x64.exe` and run it. Single self-contained
file, no .NET runtime needed. Settings, linked characters and skins carry over —
they live in your local database, not the program file.

Close the old overlay before running the new one.

**Windows SmartScreen will warn you** — the build is not code-signed. Choose
*More info → Run anyway*, or check the hash first.

```
SHA-256: 77C4D3CC5222770E94AE8A5002E7C5691D73DF033E5F5794C0C3B97D72839607
```

```powershell
Get-FileHash .\CryonicOverlay-v0.7.3-win-x64.exe -Algorithm SHA256
```

## Linux — experimental, testing only

**Not supported. There is no Linux build and no Mac build.** Same `.exe`, run
inside EVE's Proton prefix:

```bash
protontricks-launch --appid 8500 ./CryonicOverlay-v0.7.3-win-x64.exe
```

It must be the **same** prefix as EVE, or the overlay sees no clients. Expect the
Active Instances previews to be empty boxes — Wine does not implement the
Windows API they are built on.

Full steps and what to report:
<https://github.com/tendeuse/Cryonic-overlay/blob/main/docs/proton-test-plan.md>

## Notes for testers

If the French reads wrong anywhere, that is worth reporting — the translation was
checked against the client but not yet used in anger. Same for anything else:
what you were doing, what you expected, what happened instead, plus your version
from the bottom-left corner.
