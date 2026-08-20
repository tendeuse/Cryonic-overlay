# Cryonic Overlay v0.7.4 — Beta

A small one. Window resizing works from both sides now, and the Linux picture is
clearer after two rounds of real testing.

## Resize from either side

The overlay could only be resized by dragging its **right** edge, bottom edge, or
the ◢ grip in the corner. Grabbing the left edge did nothing, so a window parked
against the right of your screen could only be widened in the direction it had no
room to go.

The left edge and the bottom-left corner now work the same way.

Dragging the left edge keeps the **right edge where it is** — which is the point,
since the overlay anchors to the right side of your EVE window. It grows to the
left instead of shoving itself off-screen.

The top edge is still fixed. Say so if you want it.

## Hotkeys

| Key | Does |
| --- | --- |
| `Ctrl + Shift + O` | Hide the overlay panel — instance previews stay visible |
| `Ctrl + Shift + C` | Toggle click-through |
| `Ctrl + Shift + I` | Report roaming gang |
| `Ctrl + Shift + H` | Hide everything — previews and pop-outs included |

## Install

Download `CryonicOverlay-v0.7.4-win-x64.exe` and run it. Single self-contained
file, no .NET runtime needed. Settings, linked characters and skins carry over —
they live in your local database, not the program file.

Close the old overlay before running the new one.

**Windows SmartScreen will warn you** — the build is not code-signed. Choose
*More info → Run anyway*, or check the hash first.

```
SHA-256: EBC0215E3E1079486327600C4D0B781E5B939F68FA65C14907F2C6CE54B81274
```

```powershell
Get-FileHash .\CryonicOverlay-v0.7.4-win-x64.exe -Algorithm SHA256
```

> Needs a few hundred MB free on the drive it runs from. It is a self-extracting
> single file and unpacks ~70 MB before the window appears. On a full disk it
> fails with a decompression error rather than anything helpful.

## Linux — what actually works

**Still not supported, and there is no Linux or Mac build.** But two rounds of
testing on Pop!_OS / Wayland / GE-Proton produced a clearer answer than "it does
not work", so here it is.

Run the same `.exe` inside EVE's Proton prefix:

```bash
protontricks-launch --appid 8500 ./CryonicOverlay-v0.7.4-win-x64.exe
```

| | Result |
| --- | --- |
| App runs, no crashes | ✅ |
| Character linking, location, standings, all ESI data | ✅ |
| Drawing above EVE — **windowed** | ✅ |
| Drawing above EVE — borderless or fullscreen | ❌ |
| Active Instances, previews, window anchoring | ❌ |
| Global hotkeys while the **game** has focus | ❌ (work when the overlay is focused) |

So the usable Linux setup today is **EVE in windowed mode**, treating the overlay
as a normal window beside the game. Everything driven by CCP's API works;
everything that needs to see the game's actual window does not.

That last group fails because Steam runs EVE inside a container, so the overlay
ends up in a separate Wine session even when the prefix is correct — a different
window namespace. It is not a bug we can fix from inside a Windows app.

Full test procedure:
<https://github.com/tendeuse/Cryonic-overlay/blob/main/docs/proton-test-plan.md>

## Notes for testers

All four resize handles were measured on the actual release build before it went
out — left edge, right edge, bottom-left corner, and the ◢ grip's bottom edge.
Still worth a real-hands check, since a measured drag and a mouse in your hand
are not quite the same thing.

Otherwise the usual: what you were doing, what you expected, what happened
instead, plus your version from the bottom-left corner.
