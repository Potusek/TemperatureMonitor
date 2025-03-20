# Changelog

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