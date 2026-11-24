# Frontend traceability baseline
Frontend source revision: 71338c3381a24aebbbb1558c0c91f233890cc878.
Scope: six Daily Workspace modules and four role workspaces. Existing API is retained as compatibility host until migration is tested.

| Frontend model | Required workflow | Current backend |
|---|---|---|
| auth/previewSession | login, privacy, role-specific entry, logout | cookie login and privacy; manager/admin UI absent; recovery/MFA pending |
| corehr/model | draft/review/approved/awaiting signature/signed, versioning, renewal, termination | basic contract endpoint; domain transition rules added, not wired |
| corehr/model | documents, visibility, verification, expiry, requirements | pending secure storage |
| corehr/model | personal information, probation, effective employment changes | pending; basic employment history only |
| organisation/model | branches, departments, positions, assignments, reporting cycles | structured endpoints and composite tenant foreign keys added |
| organisation/model | chart coordinates, routes, undo, remove/reassign reports | pending |
| attendance/model | clock, schedule, locations, review, corrections, overtime | clock/review implemented; correction/overtime pending |
| hrworkforce/model | policies, accrual, carry, holidays, balances, approvals | partial API; history, policy versioning, integration pending |
| hrworkforce/model | payroll snapshot, overtime, benefits, payslips, mapping, export | partial API; overtime and secure files pending |
| HRDashboard / EmployeeDashboard | scoped analytics and period filters | basic dashboard only |
| administration/model | self-service, employee requests, audit | partial API; requests pending |

## Architecture migration
ABP 10.7.1 modules for domain shared, domain, application contracts, application, persistence and HTTP API are registered in the existing ASP.NET Core host. Organisation assignment is the first layered slice; remaining legacy endpoints still use direct EF access. ABP identity, permission management, multi-tenant data filters and audited repositories are not yet integrated. Custom tenant checks remain authoritative. No ABP commercial package required. Do not run on an existing SQLite file from the old schema; use a disposable development database until migrations exist.

## Acceptance gates
Build and test every commit. Add tenant-boundary and role-scope HTTP tests, migration tests, contract lifecycle and document tests, concurrent leave and clock tests, payroll reconciliation, API schema and frontend integration tests. Never expose live HR data before production controls and review.
