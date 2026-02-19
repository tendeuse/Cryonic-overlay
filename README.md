# EVE Mission Overlay (WPF .NET 8)

This repo builds a standalone Windows overlay EXE that:
- Shows a lore-friendly transparent overlay
- Polls a Discord Cog API every 5 minutes for mission packs/missions
- Caches data in SQLite
- Ships with default lore mission packs (Caldari/ORE/CONCORD/EDENCOM/SOE)
- Lets the player switch faction focus on the fly

## Requirements
- Windows 10/11
- .NET 8 SDK (for building)
- Your Discord bot running the mission overlay Cog API

## Build locally
```powershell
cd OverlayMVP
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true
