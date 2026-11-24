# MataHR Backend

First backend implementation for the six Daily Workspace modules. ASP.NET Core 10, EF Core SQLite, modular endpoints, tenant-scoped records and cookie authentication. **Development only; production startup deliberately disabled.** Public repository: never commit employee data, passwords or database files.

## Run locally
Install .NET 10 SDK. In a local shell, set ASPNETCORE_ENVIRONMENT=Development, Bootstrap__Email and Bootstrap__Password (at least 14 characters; use a unique development password). Run:
```sh
dotnet dev-certs https --trust
dotnet run --project src/MataHR.Api --urls https://localhost:7043
```
Database is created in the working directory. Bootstrap creates one tenant and Admin on an empty database. Obtain tenant ID locally from the Tenants table using a SQLite viewer; no public tenant enumeration. OpenAPI is served at /openapi/v1.json in Development. Use same-origin HTTPS reverse proxy for the frontend. All POST/PUT calls require X-MataHR-Request: 1; no cross-origin credential sharing configured. Authenticate with tenantId, email, password; acknowledge /api/auth/privacy before calling workspace routes. Cookie is HttpOnly, Secure, SameSite Strict. Sign out via POST /api/auth/logout.

## Roles
Admin provisions accounts and reads redacted audit metadata; no automatic access to employee salary/personnel data. HR manages staff, leave and payroll. Manager can read direct reports and review team attendance/leave. Employee reads own records, clocks and submits own leave. Multiple assigned roles supported. Authentication validates active account and current roles per request.

## Delivered endpoints
- /api/auth/login, logout, me, privacy
- /api/admin/accounts, audit
- /api/employees, employees/{id}/history, employees/{id}/contracts, /api/contracts
- /api/organisation/departments
- /api/attendance, attendance/locations, attendance/schedules, attendance/clock-in, attendance/clock-out, attendance/{id}/review
- /api/leave/policies, holidays, entitlements, balances/{employee}, requests, requests/{id}/review, requests/{id}/withdraw
- /api/payroll/salaries, benefits, runs, runs/{id}/transition, runs/{id}/export (approved JSON snapshot)
- /api/integrations, /api/dashboard

## Rules
Server clock in SAST, unverified presence, open-session uniqueness, approved leave conflict. Leave uses configured workdays and holidays, one calendar-year cycle, manual entitlement and carry-over, current-month simplified accrual, pending reservations and staged reviews. Payroll uses reviewed attendance, unpaid leave and salary/benefits snapshots; outstanding or changed inputs block review/approval; reviewer and approver must differ. No payroll tax or net-pay calculations.

## Not complete
Frontend is not wired to this API. DTO mapping from existing string demo IDs to GUIDs is needed. No ABP dependency (plain ASP.NET Core selected for this initial foundation). Missing contract lifecycle/signatures, document and payslip secure storage, full organisation positions/branches, attendance corrections/overtime endpoints, approved leave cancellation, versioned policies/schedules, full payroll export mapping and provider synchronization. No real GPS/QR/biometrics, account lifecycle/recovery/MFA, tenant onboarding, migration scripts, production database, deployment or compliance certification. Privacy notice content and operational safeguards require review. SQLite EnsureCreated is development-only. Security, concurrent transactions and all cross-role/tenant workflows require integration testing. Do not use real personnel data.

```sh
dotnet test tests/MataHR.Tests/MataHR.Tests.csproj
```

## Enterprise foundation branch
ABP modular foundation (10.7.1), domain rules and first structured organisation slice added. See [frontend traceability](docs/frontend-traceability.md). Existing API host remains the entry point. Use a fresh disposable SQLite database because schema migration is not implemented. This is not the complete ABP identity/permission migration.
