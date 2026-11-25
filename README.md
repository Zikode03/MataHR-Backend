# MataHR — ABP Daily Workspace backend

ASP.NET Core 10 and ABP Framework 10.7.1 with SQL Server, typed application services, domain workflows, ABP Identity, persisted permission grants, Audit Logging, repositories, tenant filters and transactional units of work.

The new API host is **src/MataHR.Workforce.Host**. It implements Dashboard, Employees, Organisation, Digital Register, Leave Management and Payroll & Benefits. The original `MataHR.Api` source is an archived reference, excluded from the supported solution. Both startup scripts target Workforce.Host. This code change does not replace an already running local process.

See the [backend guide](docs/daily-workspace-backend.md) for architecture, workflows, endpoint conventions and remaining production work.

## Local configuration

| Setting | Value |
|---|---|
| SQL host | localhost,14336 |
| Docker SQL internal port | 1433 |
| Database | MataHR |
| API | https://localhost:7043 |
| Health | /health |
| OpenAPI | /openapi/v1.json |

Keep the SQL password in the ignored `.env` as `MSSQL_SA_PASSWORD`. Use literal single quotes for dotenv values containing dollar signs. Do not commit credentials. Account passwords and the SQL password are different credentials.

```powershell
docker compose up -d
pwsh -File scripts/Start-Workforce.ps1
```

Normal startup requires the reviewed Workforce migrations to have been applied. First-time **development** migration is a deliberate opt-in:

```powershell
pwsh -File scripts/Start-Workforce.ps1 -ApplyDevelopmentMigrations
```

The migration adds `Mata*` tables and preserves legacy tables. It does not import legacy records. Review/import existing data before activating the new host. Your local database has not been migrated as part of the staged implementation.

In Visual Studio, open `MataHR.sln`. Select `MataHR.Workforce.Host` only when ready to activate it. Its launch profile uses .NET User Secrets for the connection string; never put the database password in launchSettings. The Development-only managed SQL networking option supports `localhost` on this machine and does not change SSMS.

## Verification

```powershell
pwsh -File scripts/Test-Workforce.ps1
```

Tests use GUID-named disposable databases on the local SQL instance. Cleanup verifies the test database prefix; it does not target `MataHR`. Tests cover tenant/role scope, real authentication and privacy, account lockout, optimistic concurrency, migration preservation, concurrent clock and leave requests, independent approvals, documents, contracts, payroll and private payslip release.

CI builds `MataHR.Workforce.Host` and runs `MataHR.Workforce.Tests` against an isolated SQL Server container. Restore migration tooling with `dotnet tool restore` if generating a future migration.

## API and permissions

All writes require `X-MataHR-Request: 1`. Login uses assigned accounts at `/api/auth/login`; accept the current notice at `/api/auth/privacy` before accessing workforce data. Roles are Admin, HR, Manager and Employee. Admin has no automatic personnel or payroll access; managers are scoped to direct reports; employees access their own records.

The frontend remains a separate demo until its stores and DTOs are connected to this API. Payroll implements reconciled inputs, benefits, overtime, approval, export and provider payslip release. Statutory tax calculations, payment instructions and live payroll-provider integrations are not implemented. Production startup remains disabled pending operational and security work described in the guide.

## Security and data history

Accounts use native ABP Identity users and roles, lockout, security stamps, password reset tokens and authenticator MFA with one-use recovery codes. Password reset delivery requires `Auth:RecoveryEnabled`, an HTTPS `Auth:PasswordResetUrl`, and configured ABP email delivery. No reset mail is sent by default. MFA enrollment and recovery endpoints are documented in the backend guide.

Compensation revisions and benefit enrollments are effective-dated on month boundaries. Payroll freezes source fingerprints and rejects changed sources before approval. Undated legacy pay records require reconciliation. Leave freezes its working dates; older leave without a matching calendar fails closed until reconciled. Migration preserves legacy account tables but does not silently import credentials into Identity.
