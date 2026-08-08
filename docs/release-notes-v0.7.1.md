# Cryonic Overlay v0.7.1 — Beta

Quality-of-life work on the instance windows, plus a new hotkey.

## New

**`Ctrl + Shift + H` — hide everything.**
Hides the overlay, the Active Instances previews, and every popped-out
instance window in one press. Press again and everything returns where it was.

This is the one to use before a screenshot or when you go live. `Ctrl+Shift+O`
only fades the overlay panel, which leaves the live EVE previews on screen —
Windows draws those itself, so the overlay has to hide them explicitly. Now it
does.

## Improved

**Popped-out instance windows are easier to resize.** The grab band on the right
edge, bottom edge, and corner is now 14px instead of 8, so it is no longer a
pixel hunt.

**The ✕ and re-attach buttons work properly.** Both are bigger targets, and
closing a popped-out window now returns it to the overlay with its live preview
intact instead of an empty black box.

**Help now covers all four hotkeys**, in English and French.

## Hotkeys

| Key | Does |
| --- | --- |
| `Ctrl + Shift + O` | Show or hide the overlay panel |
| `Ctrl + Shift + C` | Toggle click-through |
| `Ctrl + Shift + I` | Report roaming gang |
| `Ctrl + Shift + H` | Hide everything, previews and pop-outs included |

## Install

Download `CryonicOverlay-v0.7.1-win-x64.exe` and run it. Nothing to install and
no .NET runtime needed — it is a single self-contained executable. Your
settings, linked characters and skins carry over; they live in your local
database, not in the program file.

If you are replacing an older build, close it first, then run the new file.

**Windows SmartScreen will warn you.** The build is not code-signed yet. Choose
*More info → Run anyway*, or verify the hash below first.

```
SHA-256: FA2CB371C4AEF23826C0E8F7D651AB4CD2AEE81F242BB3B1C4F71033036B7521
```

Check it in PowerShell:

```powershell
Get-FileHash .\CryonicOverlay-v0.7.1-win-x64.exe -Algorithm SHA256
```

## Notes for testers

This is a beta. If something breaks, the most useful report is what you were
doing, what you expected, and what happened instead — plus your overlay version
from the bottom-left corner.
