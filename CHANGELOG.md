# Changelog

## 0.14.0 — 2026-09-18

### Added

- Remote Windows service status, start, stop, and restart operations
- Temporary-file cleanup preview and confirmed cleanup for Windows Temp and local profile Temp folders
- Selectable minimum file age, per-computer results, audit entries, cancellation, and in-memory maintenance session reports
- Dedicated resizable Remote Maintenance window with remembered placement and adjustable activity-log height

## 0.13.1 — 2026-09-18

### Added

- Inventory updates now support standard `.xlsx` workbooks in addition to macro-enabled `.xlsm` workbooks

## 0.13.0 — 2026-09-18

### Added

- Remote Group Policy refresh with parallel execution, cancellation, auditing, and session reporting
- Resizable Protected Computers editor with remembered window placement

### Changed

- Moved protected-computer management out of the main workspace into a dedicated window


CoreOps uses semantic versioning while it is under active development.

## 0.12.1 — 2026-09-18

### Changed

- Changed workflow to more reliable workflow.
### Fixed

- Dell session reports now use Dell-specific in-memory transcript text instead of the LAPS password disclaimer.

## 0.12.0 — 2026-09-18

### Added

- In-memory Dell Updates and Windows LAPS session-report windows; LAPS reports never contain password values.
- Draggable, remembered internal pane sizing in the Dell Updates and Windows LAPS workspaces.

### Changed

- Dell installation no longer requires a prior scan, but presents a prominent warning that scanning first is strongly recommended.


## 0.11.12 — 2026-09-18

### Changed

- Dell scans now capture DCU's console transcript directly in memory and display it in the selected-computer panel.
- Removed XML report creation, filesystem parsing, ownership changes, and ACL manipulation from Dell scans.


## 0.11.2 — 2026-09-16

### Added

- Draggable horizontal resizing between the target-computer and protected-computer panes.
- Draggable vertical resizing between the main input panel and activity log.
- Persistent main-pane proportions across application sessions.

## 0.11.1 — 2026-09-16

### Added

- Per-window memory for size, position, and maximized state across CoreOps sessions.

### Fixed

- Saved window positions are validated against connected monitor work areas before restoration, preventing inaccessible off-screen windows after display or remote-session changes.

## 0.11.0 — 2026-09-16

### Added

- Dedicated Dell Command Update workspace with remote detection, scanning, confirmed installation, audit entries, and reboot-required reporting.
- Domain-computer selection within the Dell updates workspace.

### Changed

- Main, LAPS, domain-picker, session-report, and Dell-update windows now constrain themselves to the current monitor's usable work area with DPI-aware sizing.
- Reduced secondary-window minimum dimensions for smaller displays and remote sessions.

## 0.10.2 — 2026-09-16

### Changed

- The main window now launches maximized and supports smaller displays without placing title-bar controls off screen.
- Removed the redundant inline results table; complete results remain available in Session report.
- Reduced header, form, spacing, and activity-log sizing to give operations more usable space on lower-resolution screens.

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
- VNC configuration deployment
- Settings interface and release hardening
