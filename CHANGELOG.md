# Changelog

CoreOps uses semantic versioning while it is under active development.

## 0.7.1 — 2026-09-12

### Changed

- Moved Active Directory computer selection from the main panel into a dedicated modal picker
- Restored the compact main-window layout and its previous result/activity proportions
- Added a custom high-contrast selected-computer style, automatic loading, and Select all shown

## 0.7.0 — 2026-09-12

### Added

- Domain computers tab backed by Active Directory
- On-demand refresh of enabled domain computers
- Search by computer name or operating system
- Extended multi-selection with selected computers copied to the manual target list

### Changed

- Increased the main window height to accommodate domain selection without compressing results and activity areas

## 0.6.0 — 2026-09-12

### Added

- Browse button for selecting the macro-enabled inventory workbook
- Direct Open XML workbook updates without requiring Microsoft Excel
- Safe temporary-copy update and replacement process

### Changed

- Workbooks are marked for formula recalculation the next time they are opened in Excel
- Missing-file and locked-workbook errors no longer refer to a mapped drive

## 0.5.0 — 2026-09-12

### Added

- MIT license and public contribution, privacy, and security policies
- Code-signing policy and public release documentation
- Automated GitHub Actions build for pull requests, main, and version tags
- Pinned .NET SDK and explicit least-privilege application manifest
- Local persistence for the inventory workbook path and protected-computer list

### Changed

- Replaced organization-specific example computers and workbook path with neutral defaults
- Added consistent publisher, repository, copyright, license, and version metadata
- Excluded generated installer releases from source control

## 0.4.0 — 2026-09-11

First tracked pre-release.

### Included

- Windows desktop application built with C# and WPF
- Multi-computer WinRM connectivity checks
- Remote hardware and Windows inventory collection
- Updates existing rows in the macro-enabled inventory workbook
- Preserves VBA, formulas, formatting, and manually maintained workbook columns
- Searches both Client Systems and Servers by computer name
- Protected-computer enforcement and confirmed remote restarts
- Bounded parallel execution and cancellation
- JSON Lines audit logging
- CoreOps branding, application icon, charcoal interface, and breathing logo
- Self-contained single-file Windows deployment

### Still planned before 1.0

- Windows Update operations
- Dell Command Update scan and installation
- Group Policy refresh
- WPKG service control
- VNC configuration deployment
- Temporary-file and RMS log cleanup
- Settings persistence and broader operational testing
