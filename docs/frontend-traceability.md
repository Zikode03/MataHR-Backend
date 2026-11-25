# Daily Workspace frontend integration boundary

The supported backend is `MataHR.Workforce.Host`, with layered ABP services and native Identity, Permission Management and Audit Logging. The frontend demo stores have not yet been replaced with API clients. Backend completion does not imply frontend integration.

| Module | Implemented backend workflow | Integration remaining |
|---|---|---|
| Authentication | Assigned roles, privacy gate, cookie/session revocation, lockout, password recovery, authenticator MFA | Frontend forms, MFA and recovery screens; production email configuration |
| Employees | Scoped records, employment history, independent contract review and verified signed-copy evidence | Frontend DTO mapping; signature provider integration |
| Documents | Protected upload/download, metadata-only listing, content hashes and attestation | Frontend API adapter; malware scanning and retention operations |
| Organisation | Branches/departments/positions, reporting-cycle validation, chart coordinates and connector controls | Persist frontend canvas commands; undo orchestration |
| Digital Register | Server-time clocking, schedules/locations, review, correction/overtime approval | Frontend API adapter; physical presence verification remains unverified |
| Leave | Versioned policies, allocations/ledger, frozen working dates, cross-year reservations, manager/HR approval and cancellation | Frontend API adapter; historical import reconciliation |
| Payroll & Benefits | Dated revisions, batched readiness, source fingerprints, independent approval, exports and released provider payslips | Frontend API adapter; external payroll provider/statutory calculation integration |
| Dashboard | Scoped headcount, attendance, leave and payroll-input totals | Replace demo selectors with period-filtered API queries |
| Administration | Native account/role management, persisted grants, audit metadata | Admin UI and production operations |

Verification runs against disposable SQL databases, with tenant/role boundaries, workflow concurrency, historical payroll and security lifecycle regressions. Production startup remains disabled; real-data migration and service activation are separate operations.
