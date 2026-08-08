# Cryonic Overlay v0.7.2 — Beta

**Fixes both features from v0.7.1, which did not work.** If you are on v0.7.1,
update — the two things it advertised were broken in it.

## Fixed

**Detached instance windows can be resized again.** v0.7.1 set out to make the
grab band bigger and instead removed resizing altogether: the wider band
swallowed the corner grip that was doing the actual work, and the mechanism
meant to replace it had never been switched on. Both are fixed, so the right
edge, bottom edge, and corner now all resize — with the wider, easier-to-hit
band that was intended.

**`Ctrl + Shift + H` now hides the Active Instances previews.** Those live EVE
previews are drawn by Windows itself rather than by the overlay, so making the
overlay transparent left them on screen. They are now properly hidden, and stay
hidden — the refresh that runs every few seconds no longer turns them back on.

## Reminder — what the hotkeys do

| Key | Does |
| --- | --- |
| `Ctrl + Shift + O` | Show or hide the overlay panel |
| `Ctrl + Shift + C` | Toggle click-through |
| `Ctrl + Shift + I` | Report roaming gang |
| `Ctrl + Shift + H` | Hide everything, instance previews and pop-outs included |

## Install

Download `CryonicOverlay-v0.7.2-win-x64.exe` and run it. Single self-contained
file, no .NET runtime needed. Your settings, linked characters and skins carry
over — they live in your local database, not in the program file.

Close the old overlay before running the new one.

**Windows SmartScreen will warn you** — the build is not code-signed. Choose
*More info → Run anyway*, or check the hash first.

```
SHA-256: 0AC741B618FAACBC91468978459D384CAA421013E07AECAC8E09DA74C23B71E8
```

```powershell
Get-FileHash .\CryonicOverlay-v0.7.2-win-x64.exe -Algorithm SHA256
```
