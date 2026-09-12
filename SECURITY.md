# Security policy

## Supported versions

CoreOps is under active prerelease development. Security fixes are provided only for the latest published version.

## Reporting a vulnerability

Please use GitHub's private vulnerability reporting feature for this repository. Do not include credentials, confidential inventory, internal hostnames, or personal information in a report.

Please allow a reasonable period for investigation and remediation before publicly disclosing an exploitable issue. Include the affected version, reproduction steps, expected impact, and any suggested mitigation. You should receive an acknowledgment within seven days.

## Operational security

CoreOps is intended only for systems the operator is authorized to administer. It runs commands as the current Windows user, does not collect credentials, and should be deployed using least-privilege accounts appropriate for the requested operation. Do not configure WinRM `TrustedHosts` as `*`.
