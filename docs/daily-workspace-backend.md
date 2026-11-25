# Daily Workspace ABP backend

The new entry point is `src/MataHR.Workforce.Host`. The original `MataHR.Api` is retained as a legacy implementation for comparison and rollback. It is not the ABP workflow host. Existing frontend demo stores are not connected automatically.

## Architecture

- Domain.Shared: shared rules and existing foundation types.
- Domain/Workforce: tenant-owned audited aggregate roots and calendar/pay-period domain rules.
- Application.Contracts/Workforce: typed DTOs, bounded list queries and application-service interfaces.
- Application/Workforce: ABP application services, role permission definitions/value provider and transactional workflows. No HTTP context, EF DbContext or file-system paths in application services.
- EntityFrameworkCore/Workforce: ABP SQL Server context, default repositories, composite tenant foreign keys, concurrency stamps, filtered open-session uniqueness and EF migrations.
- HttpApi/Workforce: thin authenticated controllers. Services enforce permissions independently of controllers.
- Workforce.Host: cookie transport, claim-bound tenant context, request-header CSRF defense, privacy gate, login rate limiting, local bootstrap, migrations and sanitized concurrency status codes.

The role grants deliberately separate system administration from confidential HR/payroll operations. Managers see direct reports and cannot approve their own requests. Employees see their own records, requests and released payslips. HR sees tenant personnel/payroll; Admin manages accounts and reads redacted audit metadata. All four roles may use their own clocking and leave functions if linked to an employee.

ABP supplies repositories, multi-tenant EF data filters, audited entity metadata, permission checking, DI/interception and units of work. Authentication currently uses a dedicated cookie account store with Microsoft's password hasher, account deactivation validation, failed-login lockout and assigned roles. It is not ABP Identity/Account or OpenIddict; account recovery, MFA and federated identity are not implemented.

## Workflow coverage

| Module | Server workflow |
|---|---|
| Dashboard | Date-bounded headcount, new joiners, attendance, leave and review queues; payroll input totals visible only to HR |
| Employees | Tenant-scoped paged records, optimistic concurrency, history, contract review/approval/signature-evidence stages |
| Organisation | Branches, departments, positions, employee assignments, reporting-cycle validation, canvas coordinates and connector control points |
| Digital Register | Server-time clock-in/out in SAST, active locations/schedules, one open session, manager/HR review, correction and overtime approval |
| Leave | Versioned policies, holidays, allocations, accrual/balances, working-day counting, pending reservations, manager/HR stages, withdrawal and reviewed cancellation |
| Payroll & Benefits | Compensation and benefits, readiness issues, approved hours/overtime and unpaid leave inputs, immutable snapshots, independent approval, approved export, private verified provider payslips and explicit release |

Presence evidence stays `Unverified`; a digital clock action is not proof of physical presence. No biometric, GPS or network verification is claimed.

Contract signing records verified evidence; it is not an electronic-signature provider integration. HR document verification is independent of upload. Documents are restricted to PDF/PNG/JPEG with content signatures, a 5 MB limit, content hashes and protected SQL storage. Storage is not a public URL. Payslips cannot bypass release via the general document download route. Antivirus scanning, retention schedules, backups and database/storage encryption must be operationally configured before real personnel data is used.

## Payroll product decision

SimplePay describes leave balances, requests, released payslips and approval access in [self-service](https://www.simplepay.co.za/help/self-service-admin/) and recommends review before [finalisation](https://www.simplepay.co.za/help/self-service-admin/requests/actioning-employee-requests/). Sage separates payslips, tax calculations and employee access in its [South African payroll products](https://www.sage.com/en-za/payroll-software/).

MataHR therefore prepares and reconciles payroll inputs, locks approved snapshots, separates review from approval, and releases verified provider payslips. Overtime rates are configurable; no multiplier is represented as statutory advice. The export is a generic JSON input snapshot, not a Sage/SimplePay/PaySpace adapter, bank file, SARS submission, tax certificate, calculated net pay or payment instruction. Provider adapters and statutory calculations require a separate integration and reconciliation implementation.

## Run locally

Keep the API stopped while changing its startup project. Start Docker SQL with `docker compose up -d`. Use the existing ignored `.env` for the actual SQL password; quote dollar signs literally in dotenv values. Standard host port: `14336`; database: `MataHR`.

First review `src/MataHR.EntityFrameworkCore/Workforce/Migrations`, then apply the additive development migration explicitly:

```powershell
pwsh -File scripts/Start-Workforce.ps1 -ApplyDevelopmentMigrations
```

Subsequent starts use `pwsh -File scripts/Start-Workforce.ps1`. The host is `https://localhost:7043`, health is `/health`, and OpenAPI is `/openapi/v1.json`. Visual Studio: open `MataHR.sln`, select `MataHR.Workforce.Host` as startup project, and use its launch profile. It shares the local UserSecretsId already configured for the database connection. No credentials belong in launchSettings or appsettings.

The managed-networking switch is Development-only and applies to SqlClient, not SSMS. See [Microsoft's documentation](https://learn.microsoft.com/en-us/sql/connect/ado-net/appcontext-switches).

Bootstrap seeds only an empty Workforce account store. Configure `Bootstrap:TenantId`, `Bootstrap:Email` and a 14+ character `Bootstrap:Password` through local secrets/environment; account passwords are separate from the SQL `sa` password. Do not use an Admin account for confidential HR operations; provision an HR role explicitly.

## API contract

All writes require `X-MataHR-Request: 1`. Login is `POST /api/auth/login` with `{tenantId,email,password}`. A signed cookie carries the assigned account/role and employee identifiers. The tenant is read from validated claims, never from a request header. Every request revalidates active account, roles and employee linkage. Before workforce routes open, `POST /api/auth/privacy` persists the current notice version. `/api/auth/me` returns role permissions; logout removes the session cookie.

Lists use `{totalCount,items}` and `skipCount`/`maxResultCount` (1–200). Reads and writes use GUIDs, not frontend demo IDs. Updates and review transitions require the latest `concurrencyStamp`. Unknown or foreign-tenant IDs return 404; out-of-scope roles return 403; stale or conflicting concurrent writes return 409. Validation is 400. Errors do not return credentials or SQL diagnostics. Explicit audit records store actor, action, target, timestamp and review notes; the Admin audit DTO excludes notes. Automatic parameter auditing is disabled so credential, salary and document payloads are not captured.

Use `/api/employees`, `/api/contracts`, `/api/documents`, `/api/organisation`, `/api/attendance`, `/api/leave`, `/api/payroll`, `/api/dashboard` and `/api/admin`. The generated OpenAPI document defines each typed payload and route. Payroll snapshots are immutable after approval; source changes in approved periods are blocked for attendance/leave and require a later separately reviewed adjustment workflow.

## Migration and verification

New tables use the `Mata` prefix. The initial migration does not drop or modify the legacy tables. It does not import legacy rows into the new model. Existing HR data requires a reviewed import/reconciliation before changing the active host. Never delete a volume to fix schema mismatch. Production startup stays disabled pending deployment/security review, secrets, least-privilege SQL access, recovery/MFA, storage safeguards and payroll integration.

`pwsh -File scripts/Test-Workforce.ps1` uses a unique `MataHR_Workforce_Test_<guid>` database and cleans up only that verified prefix. It tests HTTP authentication/privacy, tenant/role scope, stale updates, documents/contracts, concurrent clock and leave requests, staged reviews, payroll approval, working-day rules and migration preservation. It does not target `MataHR` for destructive cleanup. CI runs the new host and tests against a disposable SQL instance.
