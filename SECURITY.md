# Security policy

## Supported versions

CoreOps is under active prerelease development. Security fixes are provided only for the latest published version.

## Reporting a vulnerability

Please use GitHub's private vulnerability reporting feature for this repository. Do not include credentials, confidential inventory, internal hostnames, or personal information in a report.

Please allow a reasonable period for investigation and remediation before publicly disclosing an exploitable issue. Include the affected version, reproduction steps, expected impact, and any suggested mitigation. You should receive an acknowledgment within seven days.

## Operational security

CoreOps is intended only for systems the operator is authorized to administer. It runs commands as the current Windows user, does not collect credentials, and should be deployed using least-privilege accounts appropriate for the requested operation. Do not configure WinRM `TrustedHosts` as `*`.

Windows LAPS operations rely entirely on Microsoft LAPS and Active Directory authorization for the signed-in Windows identity. The interface does not store retrieved passwords and clears an unchanged copied password from the clipboard after 30 seconds or when the LAPS window closes. Domain-controller and Windows LAPS operational events remain the authoritative security audit.

Dell update installation requires explicit confirmation, runs under the signed-in operator's delegated remote permissions, and never requests an automatic reboot. BIOS and firmware updates remain inherently disruptive; scan first, verify power and BitLocker recovery processes, and schedule installation under the organization's change-control policy. Cancelling CoreOps cannot guarantee cancellation of a Dell update process already running remotely.
