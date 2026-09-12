# CoreOps

Current release: **0.5.0**

## Versioning

CoreOps follows semantic versioning:

- Increment the patch version for compatible fixes: 0.4.0 to 0.4.1.
- Increment the minor version for new functionality: 0.4.x to 0.5.0.
- Use 1.0.0 when the planned administration features are implemented and production-tested.
- Update the version properties in NetworkAdmin.App.csproj and add a changelog entry for every release.

A Windows WPF administration console written in C#. The app supports multiple targets, WinRM connectivity checks, inventory collection, protected-computer enforcement, audited restarts, cancellation, and bounded parallel execution. CoreOps is intended only for computers the operator is authorized to administer.



Inventory collection updates existing computer rows in the `Client Systems` or `Servers` worksheet of the workbook selected by the operator. It matches the `Name` column case-insensitively, preserves macros and business-maintained columns, and does not silently add unknown computers. Microsoft Excel must be installed, the workbook must be writable, and the workbook should be closed before collection starts.

The inventory workbook path and protected-computer list are local preferences stored in `%LOCALAPPDATA%\CoreOps\settings.json`; they are not transmitted to the project maintainers.

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

Audit logs are JSON Lines files under `%LOCALAPPDATA%\NetworkAdmin\Logs`.

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

- Committers and reviewers: [CoreOps contributors](https://github.com/xotan135/networkadmin/graphs/contributors)
- Approvers: [repository owner](https://github.com/xotan135)

## License

CoreOps source code and original project artwork are released under the [MIT License](LICENSE). Contributions are accepted under the same license. Microsoft Excel, Windows, and PowerShell are separate products. Self-contained releases incorporate the separately licensed .NET runtime; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Next operations

The service boundary in `Services/RemoteManagementService.cs` is ready for Windows Update, Dell Command Update, Group Policy refresh, service control, VNC configuration, and cleanup operations from the original script.
