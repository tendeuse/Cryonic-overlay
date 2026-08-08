# Cryonic Overlay v0.7.2 — Beta

Small fix on top of v0.7.1.

## Fixed

**The Discord link in the sponsor banner was dead.** The invite baked into
v0.7.1 and earlier was a default 7-day link, so it had expired long before
anyone clicked "Contact tendeuse on Discord". It is now a permanent invite that
will not lapse.

Everything else is identical to v0.7.1 — if you already have that and do not
need the Discord link, there is nothing here you are missing.

## Everything from v0.7.1

- `Ctrl + Shift + H` hides everything: the overlay, the Active Instances
  previews, and every popped-out instance window
- Popped-out instance windows resize from a much wider grab band
- The ✕ and re-attach buttons work, and closing a pop-out returns it to the
  overlay with its live preview intact
- Help covers all four hotkeys in English and French

| Key | Does |
| --- | --- |
| `Ctrl + Shift + O` | Hide the overlay panel — instance previews stay visible |
| `Ctrl + Shift + C` | Toggle click-through |
| `Ctrl + Shift + I` | Report roaming gang |
| `Ctrl + Shift + H` | Hide everything — previews and pop-outs included |

## Install

Download `CryonicOverlay-v0.7.2-win-x64.exe` and run it. Single self-contained
file, no .NET runtime needed. Settings, linked characters and skins carry over —
they live in your local database, not in the program file.

Close the old overlay before running the new one.

**Windows SmartScreen will warn you** — the build is not code-signed. Choose
*More info → Run anyway*, or check the hash first.

```
SHA-256: F8D792130AB4A0322BC125B07C74B8F908F39D8743ACB2015ABA19E37C0F711B
```

```powershell
Get-FileHash .\CryonicOverlay-v0.7.2-win-x64.exe -Algorithm SHA256
```

## Linux — experimental, testing only

**Not supported. There is no Linux build and no Mac build.** This section exists
because a tester offered to try it, and the results decide whether Linux is worth
pursuing at all.

Nothing extra to download — same `.exe`, run inside EVE's Proton prefix:

```bash
protontricks-launch --appid 8500 ./CryonicOverlay-v0.7.2-win-x64.exe
```

It must be the **same** prefix as EVE, or the overlay sees no clients.

Expect the Active Instances previews to be **empty dark boxes** — Wine does not
implement the Windows API they are built on. The rest may or may not work; that
is what is being measured.

Full steps, fallbacks, and what to report:
<https://github.com/tendeuse/Cryonic-overlay/blob/main/docs/proton-test-plan.md>

## Notes for testers

This is a beta. If something breaks, the most useful report is what you were
doing, what you expected, and what happened instead — plus your overlay version
from the bottom-left corner.
