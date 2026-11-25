# MataHR Backend

First backend implementation for the six Daily Workspace modules. ASP.NET Core 10, EF Core SQL Server, modular endpoints, tenant-scoped records and cookie authentication. **Development only; production startup deliberately disabled.** Public repository: never commit employee data, passwords or database files.

## Run locally (Windows PowerShell, Docker Desktop and SSMS)
Install .NET 10 SDK. Copy .env.example to .env and set a strong unique development password. Start SQL Server:
```powershell
docker compose up -d
docker compose ps
```
In SSMS connect to localhost,1433 using SQL Server Authentication, login sa and the password in .env. Enable Trust server certificate for local development only. If port 1433 is occupied, set MSSQL_PORT in .env and use that port in SSMS and the connection string.
In PowerShell, set the connection string without committing secrets:
```powershell
$secret = Read-Host "SQL Server development password" -MaskInput
$env:ConnectionStrings__Default = "Server=localhost,1433;Database=MataHR;User Id=sa;Password=$secret;Encrypt=True;TrustServerCertificate=True"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Database__InitializeDevelopmentSchema = "true"
$env:Bootstrap__Email = Read-Host "Initial administrator email"
$env:Bootstrap__Password = Read-Host "Initial administrator password (14+ characters)" -MaskInput
dotnet dev-certs https --trust
dotnet run --project src/MataHR.Api --urls https://localhost:7043
```
The explicit development initializer creates an empty database schema. It does not upgrade existing schemas. Production startup remains disabled; reviewed EF migrations and a least-privileged application login must replace this bootstrap before deployment. Do not delete existing databases or Docker volumes. Bootstrap creates one tenant and Admin on an empty database. Obtain tenant ID from Tenants in SSMS. OpenAPI: /openapi/v1.json. Use same-origin HTTPS reverse proxy; POST/PUT require X-MataHR-Request: 1. Authenticate with tenantId, email, password and acknowledge /api/auth/privacy. Tests require MATAHR_TEST_SQL targeting a disposable SQL Server; CI supplies an isolated container.

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
Frontend is not wired to this API. DTO mapping from existing string demo IDs to GUIDs is needed. No ABP dependency (plain ASP.NET Core selected for this initial foundation). Missing contract lifecycle/signatures, document and payslip secure storage, full organisation positions/branches, attendance corrections/overtime endpoints, approved leave cancellation, versioned policies/schedules, full payroll export mapping and provider synchronization. No real GPS/QR/biometrics, account lifecycle/recovery/MFA, tenant onboarding, migration scripts, production database, deployment or compliance certification. Privacy notice content and operational safeguards require review. SQL Server EnsureCreated is development-only. Security, concurrent transactions and all cross-role/tenant workflows require integration testing. Do not use real personnel data.

```sh
dotnet test tests/MataHR.Tests/MataHR.Tests.csproj
```

## Enterprise foundation branch
ABP modular foundation (10.7.1), domain rules and first structured organisation slice added. See [frontend traceability](docs/frontend-traceability.md). Existing API host remains the entry point. Use a fresh disposable SQL Server database because schema migration is not implemented. This is not the complete ABP identity/permission migration.
