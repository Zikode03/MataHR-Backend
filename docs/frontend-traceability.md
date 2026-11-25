# Connected Daily Workspace traceability

The default React entry point mounts the live API workspace. Demo stores are not a fallback. The supported backend is `MataHR.Workforce.Host`, with ABP Identity, persisted Permission Management, Audit Logging and tenant-filtered repositories.

| Area | Connected frontend and server behavior | Remaining boundary |
|---|---|---|
| Authentication | Native login, MFA challenge completion, server privacy acknowledgement, session expiry and mandatory temporary-password changes | Production email delivery; authenticator enrollment management UI |
| Four roles | Admin account/grant/audit screens; HR workflows; manager direct-report review; employee personal services | New modules need their own APIs before appearing as live tasks |
| Employees | Paged search, creation/update, concurrency, history, documents and staged contracts | Electronic-signature provider; production scanning/retention |
| Organisation | HR chart dragging, reporting changes, route points and company-card position persisted; safe read-only directory/tree | Legacy branch address fields are not part of this server model |
| Digital register | Server-time punches, current-session lookup, schedule/location setup, GPS/code check, exceptions, correction/overtime decisions | Actual office boundary configuration; no continuous-presence or device-attestation claim |
| Leave | Current balances, evidence references, application/withdrawal/cancellation, manager/HR stages, allocation/policy/holiday setup | No half-day workflow is claimed |
| Payroll & benefits | Dated compensation/enrollment, readiness, independent run review, approved export, document verification, payslip attachment/release/download | Statutory net pay, bank/tax submissions and live provider adapter |
| Legacy data | Expiring preview, explicit company mapping, fingerprint check, transactional commit, idempotent preservation and temporary onboarding credentials | Employee/account scope only; other legacy module tables remain preserved |

The local reconciliation found no legacy employee rows and one legacy account whose email already has a native account. The original and current tenant IDs are explicitly mapped in ignored local configuration; existing native credentials and grants remain unchanged.

The regression suite covers tenant/role boundaries, concurrent workflows, historical payroll, native security, presence expiry/replay/accuracy, reviewed import and real browser persistence. Production startup remains disabled pending operational controls. Test data is created only in guarded disposable databases.
