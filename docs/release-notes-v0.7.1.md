# Cryonic Overlay v0.7.1 — Beta

Quality-of-life update for the instance windows, plus a new hotkey.

## New

**`Ctrl + Shift + H` — hide everything.**
Hides the overlay *and* every detached instance window in one press. Press it
again to bring them all back, exactly where they were.

This is the one to use before a screenshot or when you go live. `Ctrl+Shift+O`
only fades the main overlay — the detached instance windows show a live preview
of the game composited by Windows itself, outside the overlay's control, so
fading cannot touch them. They are now genuinely hidden.

## Improved

**Instance windows are easier to resize.** The grab band on the right edge,
bottom edge, and corner went from 8 to 14 pixels, so you no longer have to hunt
for the exact pixel.

**Help now covers all four hotkeys**, in English and French.

## Hotkeys

| Key | Does |
| --- | --- |
| `Ctrl + Shift + O` | Show or hide the overlay |
| `Ctrl + Shift + C` | Toggle click-through |
| `Ctrl + Shift + I` | Report roaming gang |
| `Ctrl + Shift + H` | Hide everything, instance windows included |

## Install

Download `CryonicOverlay-v0.7.1-win-x64.exe` and run it. Nothing to install and
no .NET runtime needed — it is a single self-contained executable. Your
settings, linked characters and skins carry over; they live in your local
database, not in the program file.

If you are replacing v0.7.0, close the old overlay first, then run the new file.

**Windows SmartScreen will warn you.** The build is not code-signed yet. Choose
*More info → Run anyway*, or verify the hash below first.

```
SHA-256: BC72809C88C362439F5F35A8E98AFBDA3B7DA0003CD148F27B523B43F43090DF
```

Check it in PowerShell:

```powershell
Get-FileHash .\CryonicOverlay-v0.7.1-win-x64.exe -Algorithm SHA256
```

## Notes for testers

This is a beta. If something breaks, the most useful report is what you were
doing, what you expected, and what happened instead — plus your overlay version
from the bottom-left corner.
