# Changelog

CoreOps uses semantic versioning while it is under active development.

## 0.10.1 — 2026-09-12

### Fixed

- Kept empty Windows LAPS result grids dark instead of falling back to the Windows light theme.
- Added consistent dark hover, selected, pressed, and disabled states for Windows LAPS controls.

## 0.10.0 — 2026-09-12

### Added

- Dedicated Windows LAPS workspace accessible from the CoreOps header
- Password Operations and password-free Activity tabs
- Active Directory computer search and authorized current/password-history retrieval
- Masked password display with explicit reveal and copy controls
- Automatic 30-second clipboard clearing and clearing when the LAPS window closes
- Confirmed LAPS password-expiration/rotation requests

### Security

- Password values remain in process memory or the clipboard and are never written to logs, settings, reports, or workbooks
- LAPS and Active Directory continue to authorize every operation using the signed-in Windows identity
- The reference-only `laps-main` directory is excluded from the CoreOps repository

## 0.9.1 — 2026-09-12

### Changed

- Workbook Updated/Last Updated timestamps now use `MM.dd.yyyy HH:mm`.

## 0.9.0 — 2026-09-12

### Added

- Sortable Computer, Operating system, and OS version columns in the domain picker
- Search scopes for all fields, computer names, operating systems, or OS versions
- Exact operating-system/version filter populated from Active Directory results

### Changed

- Domain discovery now requests the Active Directory OperatingSystemVersion property
- Domain selection uses a sortable dark data grid while preserving extended multi-selection

## 0.8.0 — 2026-09-12

### Added

- Live in-memory HTML session report with no accumulated report files
- Specific remote failure states for missing names, offline computers, access denial, WinRM failures, and other command failures
- Explicit Not in workbook result for collected computers without a matching inventory row

### Changed

- Active Directory selections now use the short computer name rather than the fully qualified DNS name
- PowerShell CLIXML error output is decoded and cleaned before display or audit logging

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
