# Privacy policy

CoreOps does not contain telemetry, advertising, analytics, or an automatic update service. It does not transfer information to the CoreOps maintainers or to third-party services.

CoreOps connects only to computers explicitly named by the operator and only when the operator starts an action. It uses the current Windows user's credentials and Windows PowerShell remoting to check connectivity, collect system inventory, or request a restart.

Inventory may include computer name, hardware identifiers and specifications, Windows version, network addresses, recent local profile information, installed VNC version, and boot time. The operator chooses the Excel workbook where collected inventory is saved.

Audit records contain the time, current Windows user name, requested operation, target computer, result state, and result message. They are stored locally under `%LOCALAPPDATA%\NetworkAdmin\Logs`. Local preferences are stored under `%LOCALAPPDATA%\CoreOps\settings.json`.

CoreOps does not control how an organization stores, retains, or uses the workbook, audit records, or information obtained from managed computers. Operators are responsible for following their organization's privacy, access-control, and retention requirements.
