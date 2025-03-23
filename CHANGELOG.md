# Changelog

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