# Changelog

## [0.9.9] - 2025-10-15
### Fixed
- Fixed null reference exception when measurement location chunk is not loaded
- Added validation to check if measurement position is accessible before taking temperature readings

### Added
- Intelligent measurement failure handling with consecutive failure counter
- Admin notifications when sensor location is unreachable (after 5 failed attempts)
- Enhanced `/tempsensor` command with measurement status display:
  - Current status (Active ✓ / Warning ⚠)
  - Time since last successful measurement
  - Count of consecutive failed attempts
- Automatic retry mechanism for temperature measurements

### Improved
- Better error logging for debugging measurement issues
- More informative feedback when sensor location is not loaded

## [0.9.8] - 2025-03-24
### Changed
- Temperature history tree view now defaults to collapsed state for all years/months except current ones
- Only the current year and current month are expanded by default in the temperature history window
- Improved user experience by reducing visual clutter in temperature history display

### Fixed
- Changed all server notification messages from Polish to English for better international compatibility
- Server logs are now displayed in English regardless of client language settings

## [0.9.7] - 2025-03-23
### Fixed
- Improved server-side performance by optimizing temperature measurement interval
- Enhanced tick listener management to ensure proper resource cleanup
- Fixed potential issues with logging timestamps
- Improved error handling for temperature measurements

### Added
- Better diagnostics for spawn point measurement location
- Detailed logging of game time during temperature recordings

## [0.9.6] - 2025-03-23
### Added
- Greenhouse mode that shows temperatures adjusted by +5°C
- Tooltip explanation for greenhouse mode
- Persistence of greenhouse mode setting

## [0.9.5] - 2025-03-23
### Added
- Font size adjustment controls in temperature history window
- Client-side persistence of font size preference
- Visual percentage indicator of current font scale

### Improved
- Better placement of font size controls in the interface
- Limited maximum font size to prevent UI elements from being cut off
- Added translation support for font size interface elements
- Enhanced readability for users with different screens and visual preferences

## [0.9.4] - 2025-03-21
### Security
- Added admin-only restrictions for temperature sensor location changes
- Regular players can now view but not modify sensor location
- Improved feedback messages related to sensor permissions
- Added informative notes about required permissions in command responses

## [0.9.3] - 2025-03-21
### Improved
- Enhanced ImGui interface with better layout and styling
- Fixed issues with displaying Polish characters by using short month names
- Improved temperature value alignment in the temperature history view
- Added alternating row colors for better readability in daily temperature data
- Fixed visual bugs when expanding/collapsing year and month sections
- Updated translation files to include short month names
- Optimized column widths and element spacing for more consistent display

## [0.9.2]- 2025-03-20
### Fixed
- Replace chat-based output with proper GUI window using ImGui
- Implement hierarchical tree view for temperature data (years, months, days)
- Display min/max temperatures at each hierarchy level
- Fix data serialization and loading to prevent data loss
- Improve chronological sorting of months and days
- Add column headers for better readability
- Update translations for GUI elements
- Fix bugs in temperature data saving and loading logic

## [0.9.1] - 2025-03-19
### Fixed
- Fixed client-server communication for multiplayer environments
- Server temperature data now properly displays on client side
- Improved error handling for network communication
- Temperature measurements now occur every 15 minutes of game time (increased from hourly)

## [0.9.0] - 2025-03-19
### Added
- Initial beta release
- Automatic temperature monitoring in the game world
- Temperature history accessible via Alt+T hotkey
- Configurable temperature sensor location with chat commands
- Support for both English and Polish languages
- Network communication for multiplayer support

### Known Issues
- GUI dialog is not yet implemented - temperature data is displayed in chat window