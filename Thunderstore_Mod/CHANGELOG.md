## [2.2.1] - 2026-05-12

### Fixed

- Fixed `MissingFieldException` in `StatsManager` caused by the latest game update where `dictionaryOfDictionaries` was changed to `internal` and its type was changed to `SortedDictionary`.
- Implemented reflection-based access for `dictionaryOfDictionaries` to ensure future compatibility.

## [2.2.0] - 2025-12-15

### Added

- Chance Upgrade Syncing: In the config you can change the chance that a player will get each upgrade that is shared.
- Chance Late Join Syncing: In the config you can change the chance that each upgrade level which gets synced for late joining player.
- Dynamic Upgrade Type Config Toggles: Upon loading the mod automatically detects all vanilla upgrades and all modded upgrades and add them to the config to be disabled or enabled by the host. This adds granular control of which mods get shared or synced.

### Fixed

- Fixed bug causing late join syncing to sync modded upgrades even when that config setting is off.
- Fixed bug causing crash with last rolled back update.

## [2.1.2] - 2025-12-02

- Rollback to version 2.0.0 due to critical issues in 2.1.0.

## [2.1.0] - 2025-12-02

### Added

- Configurable chance for upgrade sharing.
- Configurable chance for late join upgrade sync.
- Extra clarity in logging for exact player name.

### Fixed

- No longer attempts to sync late joining players in the menu lobby.

## [2.0.0] - 2025-11-29

### Added

- Host-Only Authority: The mod is now fully server-side. Only the host needs to install the mod for upgrades to be shared and synced with all clients (even vanilla clients).

- Smart Upgrade Syncing:

	- Implemented an intelligent system that distinguishes between Vanilla and Modded upgrades automatically.

	- Vanilla upgrades use native game commands (TesterUpgradeCommandRPC) to ensure immediate upgrade syncing and visual feedback.

	- Custom/Modded upgrades use a "Safe Sync" method (UpdateStatRPC) to sync values without crashing clients that do not have the custom mod installed.

- Dynamic Vanilla Discovery: The mod now uses reflection to automatically identify valid vanilla upgrades at runtime. This makes the mod highly compatible with future game updates without needing constant patches.

- Improved Late Join System:

	- Refactored late-join logic to hook into PlayerAvatar.Start, ensuring reliable syncing regardless of when a player joins.

	- Added specific safety checks to prevent sync logic from running in non-game scenes like the Main Menu or Splash Screen.

### Changed

- Rewrote Shared Upgrades Logic: Moved from a hardcoded list of components to a dynamic stat-monitoring system. This allows the mod to support almost any custom upgrade mod out of the box.

- Since the mod has now been completely rewritten from the ground up since the original [fork](https://github.com/W1ll-Gale/better.repo.mods/tree/mods.better-team-upgrades) the mod has now moved to its own standalone [Github repository](https://github.com/W1ll-Gale/BetterTeamUpgrades).

## [1.3.5] - 2025-11-11

### Fixed

- Added credits to `README.md`.

## [1.3.4] - 2025-11-11

### Fixed

- Fixed incorrect dll filename

## [1.3.3] - 2025-11-11

### Added

- Only the host is now in charge of syncing the upgrades for late joining players.

### Fixed

- Only attempts to sync upgrades for new players when actually in game.
- Small tweak to config file to make it more readable.

## [1.3.2] - 2025-11-09

### Fixed

- Small tweak to `README.md`

## [1.3.1] - 2025-11-07

### Fixed

- Small tweak to mod icon.

## [1.3.0] - 2025-11-07

### Added

- Implemented native late joining system that supports upgrade syncing like the currently broken `LateJoinSharedUpgradesByNastyPablo` mod.
- Added late join upgrade syncing to config so it can be enabled and disabled.

### Known Issues

- With late join upgrade syncing enabled it only updates upon moving to another level. However, the upgrades will correctly sync at that point and with it disabled the upgrades will be shared instantly.
(needs more looking into and testing - been a bit busy)

## [1.2.1] - 2025-11-07

### Fixed

- Fixed `CHANGELOG.md` file to fix spelling mistakes.

## [1.2.0] - 2025-11-07

### Added

- Player Crouch Rest Upgrade support.
- Player Tumble Wings Upgrade support.
- Player Tumble Climb Upgrade support.
- Death Head Battery Upgrade support.
- Better info logging for each upgrade and each player it syncs with to aid in debugging and consistency.
- Better `README.md` file for the mod front page.

### Fixed

- Fixed error causing original mod to not be able to find method to upgrade the stats of the players.
- Updated all DLLs to be current versions.

### Changed

- Upgrade dependencies: BepInEx v5.4.2304