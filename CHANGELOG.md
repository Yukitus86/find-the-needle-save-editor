# Changelog

## 1.1.0

### New

- **Fair play**: every save marks the run (`debug_used`) and the PC (`debug_tainted` in `profile.cfg`) like the game does for mods/debug menu, so edited saves never count for the leaderboards; a notice in the sidebar explains it
- **Radar 3D view**: the pile as terrain with all buildings and the player; drag to orbit, wheel to zoom
- **X-ray mode**: see-through hay with a red beacon above every hidden needle
- Zoom and pan in the radar top view
- **Find key**: if a game update changes the save password, the editor can find the new one in the running game and remembers it
- Spending analysis in the Missions tab (by kind, biggest items)

### Changed

- The "debug flag" switch is gone from the Money tab (the editor sets it on every save)
- Dark Windows title bar
- Money, Gear, Buildings and Missions redesigned: value tiles with readable numbers (click to edit), switches, chips and bars instead of tables
- Tech tab is now a tree per area (base tech → upgrades), with unlock buttons, search and bulk actions
- Hotbar shown as keys 1–9, tools as clickable tiles
- English is the default language; German via the flag, remembered

## 1.0.0

- First release: money, needles, radar top view with teleport, tech, gear, buildings, missions, career, backups, raw data editor
