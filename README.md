# CoreOps

Current release: **0.11.2**

## Versioning

CoreOps follows semantic versioning:

- Increment the patch version for compatible fixes: 0.4.0 to 0.4.1.
- Increment the minor version for new functionality: 0.4.x to 0.5.0.
- Use 1.0.0 when the planned administration features are implemented and production-tested.
- Update the version properties in NetworkAdmin.App.csproj and add a changelog entry for every release.

A Windows WPF administration console written in C#. The app supports manual or Active Directory target selection, WinRM connectivity checks, inventory collection, protected-computer enforcement, audited restarts, live session reporting, cancellation, and bounded parallel execution. CoreOps is intended only for computers the operator is authorized to administer.



Inventory collection updates existing computer rows in the `Client Systems` or `Servers` worksheet of the `.xlsm` workbook selected by the operator. It matches the `Name` column case-insensitively, preserves macros and business-maintained columns, and does not silently add unknown computers. Microsoft Excel is not required. The workbook must be writable and should not be open by another user during an update.

The inventory workbook path and protected-computer list are local preferences stored in `%LOCALAPPDATA%\CoreOps\settings.json`; they are not transmitted to the project maintainers.

CoreOps windows are resizable and remember their last normal size, position, and maximized state in `%LOCALAPPDATA%\CoreOps\window-placement.json`. Saved positions are constrained to the currently available monitor work area when restored, so disconnecting a monitor or changing remote-session resolution does not leave a window inaccessible.

The main console also has draggable pane dividers. The vertical divider changes the relative width of the target-computer and protected-computer lists; the horizontal divider changes the space allocated to inputs and the activity log. These pane sizes are retained in the local CoreOps settings.

## Run

### VS Code

1. Open this repository folder in VS Code.
2. Install the recommended **C# Dev Kit** extension when prompted.
3. Press `F5` and select **Network Admin (WPF)** if VS Code asks for a configuration.

You can also press `Ctrl+Shift+B` to build, or run the **run Network Admin** task from **Terminal → Run Task**.

### Command line

```powershell
dotnet run --project .\NetworkAdmin.App\NetworkAdmin.App.csproj
```

The app runs remote commands as the current Windows user. Run it from an account authorized for the target computers. Target computers must have WinRM/PowerShell remoting configured. In an Active Directory domain, use computer names and Kerberos; do not set `TrustedHosts` to `*`.

The domain-computer picker loads enabled computer objects from Active Directory on demand. It requires domain connectivity, permission to read computer objects, and the Active Directory PowerShell module. The module is normally available on domain controllers and can be installed as part of RSAT on an administration workstation. Search can be scoped to computer names, operating systems, or OS versions; the list can be filtered to an exact OS/version and sorted by clicking a column heading. Selected short computer names are copied into the manual target list before an operation runs.

Audit logs are JSON Lines files under `%LOCALAPPDATA%\NetworkAdmin\Logs`.

The Session report button opens a live HTML report inside CoreOps. The report reflects the current session and is kept in memory only; CoreOps does not accumulate HTML report files. Remote failures are grouped into useful states such as Not found, Offline/unreachable, Access denied, WinRM unavailable, and Command failed. Inventory collected from a computer that has no matching workbook row is shown as Not in workbook.

## Windows LAPS

The Windows LAPS workspace opens from the module navigation in the CoreOps header. It searches Active Directory, retrieves the authorized current password and up to three history entries, copies a selected password with an automatic 30-second clipboard timeout, and requests password rotation. The Activity tab records password-free operational events only.

LAPS operations require the Microsoft Windows LAPS PowerShell module, the Active Directory PowerShell module, domain connectivity, and delegated permissions. CoreOps supplies no alternate credentials and cannot bypass Active Directory authorization. Plaintext passwords are kept only in process memory and the Windows clipboard; they are never written to CoreOps logs, reports, or settings.

## Dell Command Update

The Dell updates workspace detects Dell systems and Dell Command Update remotely, scans for applicable updates, and installs updates only after explicit confirmation. CoreOps disables automatic reboot and reports when a restart is required. Installation can include Dell drivers, firmware, and BIOS updates. The remote computer must have Dell Command Update installed, internet or configured catalog access, WinRM connectivity, and sufficient operator permissions.

CoreOps invokes Dell's installed `dcu-cli.exe`; it does not bundle or redistribute Dell Command Update. Scan and installation outcomes are included in the CoreOps audit log. Cancelling CoreOps stops waiting for new work, but a DCU process that has already started remotely may continue to completion.

## Build

```powershell
dotnet build .\NetworkAdmin.slnx
dotnet publish .\NetworkAdmin.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The repository pins the required .NET SDK in `global.json`. Tagged releases are built from a clean checkout by GitHub Actions. Compiled executables belong in GitHub Releases and must not be committed to the repository.

## Privacy and security

CoreOps does not include telemetry or collect credentials. See [PRIVACY.md](PRIVACY.md) for the information processed during requested operations and [SECURITY.md](SECURITY.md) for vulnerability reporting and supported versions.

## Code signing policy

Release artifacts must be built by the repository's automated workflow from committed source. Changes from contributors without direct repository access require maintainer review, and each production signing request requires maintainer approval. Signing credentials and private keys must never be stored in the repository.

CoreOps is preparing to apply to SignPath Foundation. Planned provider statement (not yet active): **Free code signing provided by SignPath.io, certificate by SignPath Foundation.** Until acceptance and integration are complete, release artifacts are unsigned and are labeled accordingly.

- Committers and reviewers: [CoreOps contributors](https://github.com/xotan135/CoreOps/graphs/contributors)
- Approvers: [repository owner](https://github.com/xotan135)

## License

CoreOps source code and original project artwork are released under the [MIT License](LICENSE). Contributions are accepted under the same license. Microsoft Excel, Windows, and PowerShell are separate products. Self-contained releases incorporate the separately licensed .NET runtime; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Next operations

The service boundary in `Services/RemoteManagementService.cs` is ready for Windows Update, Group Policy refresh, service control, VNC configuration, and cleanup operations from the original script.
