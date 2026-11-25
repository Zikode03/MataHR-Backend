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
| API explorer | /swagger |
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

The migration adds `Mata*` tables and preserves legacy tables. It does not import legacy records. Review/import existing data before activating the new host. Migration and import are explicit operations; a source preview never silently rewrites native accounts.

In Visual Studio, open `MataHR.sln`. Select `MataHR.Workforce.Host` only when ready to activate it. Its launch profile uses .NET User Secrets for the connection string; never put the database password in launchSettings. The Development-only managed SQL networking option supports `localhost` on this machine and does not change SSMS.

## Verification

```powershell
pwsh -File scripts/Test-Workforce.ps1
```

Tests use GUID-named disposable databases on the local SQL instance. Cleanup verifies the test database prefix; it does not target `MataHR`. Tests cover tenant/role scope, real authentication and privacy, account lockout, optimistic concurrency, migration preservation, concurrent clock and leave requests, independent approvals, documents, contracts, payroll and private payslip release.

CI builds `MataHR.Workforce.Host` and runs `MataHR.Workforce.Tests` against an isolated SQL Server container. Restore migration tooling with `dotnet tool restore` if generating a future migration.

## API and permissions

All writes require `X-MataHR-Request: 1`. Login uses assigned accounts at `/api/auth/login`; accept the current notice at `/api/auth/privacy` before accessing workforce data. Roles are Admin, HR, Manager and Employee. Admin has no automatic personnel or payroll access; managers are scoped to direct reports; employees access their own records.

The companion frontend now connects Daily Workspace screens to this API with cookie authentication and server-assigned role capabilities. Payroll implements reconciled inputs, benefits, overtime, approval, export and provider payslip release. Statutory tax calculations, payment instructions and live payroll-provider integrations are not implemented. Production startup remains disabled pending operational and security work described in the guide.

## Security and data history

Accounts use native ABP Identity users and roles, lockout, security stamps, password reset tokens and authenticator MFA with one-use recovery codes. Password reset delivery requires `Auth:RecoveryEnabled`, an HTTPS `Auth:PasswordResetUrl`, and configured ABP email delivery. No reset mail is sent by default. MFA enrollment and recovery endpoints are documented in the backend guide.

Compensation revisions and benefit enrollments are effective-dated on month boundaries. Payroll freezes source fingerprints and rejects changed sources before approval. Undated legacy pay records require reconciliation. Leave freezes its working dates; older leave without a matching calendar fails closed until reconciled. Migration preserves legacy account tables but does not silently import credentials into Identity.

## Browse and try the API locally

Open `https://localhost:7043/swagger` while the Development host is running. Visual Studio opens this explorer by default. Expand an endpoint and choose **Try it out**. The explorer supplies the required request header and uses same-origin session cookies. Use `/api/auth/login` with your configured tenant/account, then acknowledge the privacy notice through `/api/auth/privacy`; role permissions still govern each request. No credentials are embedded in the explorer. Swagger UI and the OpenAPI specification are available only in Development and Testing.

## Connected frontend, presence and legacy import

The companion MataHR frontend starts on `http://localhost:5173`; its Vite development proxy sends `/api` to this host. It now uses native sessions, privacy acknowledgement, assigned role workspaces and live Daily Workspace operations. `/api/auth/me` returns server grants, notice state and temporary-password requirements. `/api/auth/config` exposes only the configured local organisation ID.

Attendance uses a short-lived hashed worksite code and a conservative GPS-boundary check at clock-in. The server checks code expiry, employee replay, observed-time freshness, finite coordinates and reported accuracy. Precise submitted employee coordinates are not saved; measured distance/accuracy and the result are. Remote/exception attendance remains unverified. GPS is a reported device signal, not continuous-presence attestation. Real office boundaries, including Mpumalanga, await actual site details.

`POST /api/imports/preview` and `POST /api/imports/{id}/commit` implement an expiring, explicit reviewed import for employees/accounts. Source reads join the current ABP transaction, use parameterised tenant filters and fixed table names. Existing native records are preserved. `LegacyImport:SourceTenantId` maps to `LegacyImport:TargetTenantId` only when the current destination matches; otherwise reads remain within the current tenant. Set `MATAHR_LEGACY_SOURCE_TENANT_ID` in ignored local configuration when reconciling an original tenant ID with the current account store. The destination remains `MATAHR_TENANT_ID`.

Run `scripts/Test-ConnectedFrontend.ps1 -FrontendPath <frontend-path>` to verify the real browser/API/database flow using a separate disposable test database and loopback ports 7143/5174. Test fixtures never seed fictional employees into the real MataHR database.
