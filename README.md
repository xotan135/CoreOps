# CoreOps

Current release: **0.4.0**

## Versioning

CoreOps follows semantic versioning:

- Increment the patch version for compatible fixes: 0.4.0 to 0.4.1.
- Increment the minor version for new functionality: 0.4.x to 0.5.0.
- Use 1.0.0 when the planned administration features are implemented and production-tested.
- Update the version properties in NetworkAdmin.App.csproj and add a changelog entry for every release.

A Windows WPF administration console written in C#. The app supports multiple targets, WinRM connectivity checks, inventory collection, protected-computer enforcement, audited restarts, cancellation, and bounded parallel execution.



Inventory collection updates existing computer rows in the `Client Systems` or `Servers` worksheet of `Your Excel File Location`. It matches the `Name` column case-insensitively, preserves macros and business-maintained columns, and does not silently add unknown computers. Microsoft Excel must be installed, the workbook must be writable, and the workbook should be closed before collection starts.

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

## Next operations

The service boundary in `Services/RemoteManagementService.cs` is ready for Windows Update, Dell Command Update, Group Policy refresh, service control, VNC configuration, and cleanup operations from the original script.
