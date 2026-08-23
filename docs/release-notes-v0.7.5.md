# Cryonic Overlay v0.7.5 — Beta

Resizing from both sides, and one fewer permission on the login screen.

## One less permission

The overlay was asking for **`esi-characters.read_corporation_roles.v1`** — your
corporation roles — on CCP's login page. Nothing in the app has ever used it. It
was requested and never called.

It is gone. When you link a character you will now see seven permissions instead
of eight, and every one of them is used by a feature you can see.

If you linked a character on an older version, your existing token still carries
the old permission until you re-link. You can clear it any time from
[CCP's third-party authorisations page](https://community.eveonline.com/support/third-party-applications/).

Every remaining permission is now listed and justified in the
[README](https://github.com/tendeuse/Cryonic-overlay#permissions-it-asks-for-and-why),
including the wallet one — the overlay only ever reads your **balance**, never
your transaction journal, and the README names the exact method so you can check
rather than take our word for it.

## Resize from either side

The overlay could only be resized by dragging its **right** edge, bottom edge, or
the ◢ grip. Grabbing the left edge did nothing, so a window parked against the
right of your screen could only be widened in the direction it had no room to go.

The left edge and the bottom-left corner now work too. Dragging the left edge
keeps the **right edge where it is**, so it grows leftward instead of shoving
itself off-screen.

The top edge is still fixed.

## Builds are public now

Releases are built by GitHub Actions from this public source, not uploaded from a
developer's PC. The run that produced this binary — and its SHA-256 — is visible
under [Actions](https://github.com/tendeuse/Cryonic-overlay/actions).

The build is still unsigned, so SmartScreen will still warn you. That part needs
a code-signing certificate the project cannot yet afford. The hash and the public
build log are what is on offer instead.

## Hotkeys

| Key | Does |
| --- | --- |
| `Ctrl + Shift + O` | Hide the overlay panel — instance previews stay visible |
| `Ctrl + Shift + C` | Toggle click-through |
| `Ctrl + Shift + I` | Report roaming gang |
| `Ctrl + Shift + H` | Hide everything — previews and pop-outs included |

## Install

Download `CryonicOverlay-v0.7.5-win-x64.exe` and run it. Single self-contained
file, no .NET runtime needed. Settings, linked characters and skins carry over.

Close the old overlay before running the new one.

```
SHA-256: A84CFF2111C58AF428D1E0076E290534DABE4732D010D814DFA57F87DAA59AFE
```

```powershell
Get-FileHash .\CryonicOverlay-v0.7.5-win-x64.exe -Algorithm SHA256
```

> Needs a few hundred MB free on the drive it runs from — it unpacks ~70 MB
> before the window appears, and on a full disk it fails with a decompression
> error rather than anything helpful.

## Linux

Unchanged from v0.7.4: works with EVE in **windowed** mode, not borderless or
fullscreen, and anything needing the game's window handle does not work while
Steam runs EVE in a container. Full table in the
[README](https://github.com/tendeuse/Cryonic-overlay#linux-and-mac).

## Notes for testers

All four resize handles were measured on the actual release build before it went
out. Still worth a real-hands check — a measured drag and a mouse in your hand are
not quite the same thing.

Otherwise the usual: what you were doing, what you expected, what happened
instead, plus your version from the bottom-left corner.
