using MataHR.Employees;
using MataHR.Auditing;
using MataHR.Organisation;
using MataHR.Common;
using MataHR.Imports.Contracts;
using MataHR.Permissions;
using Volo.Abp.Authorization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace MataHR.Imports;

// Preview first, then verify the reviewed source at commit time so an administrator cannot import a different set of records by accident.
public class LegacyImportAppService(IRepository<Employee, Guid> workers, IRepository<WorkforceAudit, Guid> audits, ILegacySourceReader source, IRepository<LegacyImportBatch, Guid> batches, IRepository<IdentityUser, Guid> accounts, IdentityUserManager users, IRepository<Branch, Guid> offices, IRepository<Department, Guid> teams, IRepository<Position, Guid> jobs, ReportingLineManager reporting) : WorkforceService(workers, audits), ILegacyImportAppService
{
    async Task CheckKind(string kind)
    {
        Require(kind is "Employees" or "Accounts", "Unknown import kind.");
        await AuthorizationService.CheckAsync(kind == "Employees" ? WorkforcePermissions.PeopleWrite : WorkforcePermissions.Accounts);
        if (kind == "Employees")
            await AuthorizationService.CheckAsync(WorkforcePermissions.OrganisationWrite);
    }

    static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    static string[] Roles(LegacyAccount account) => account.Roles.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
    public virtual async Task<LegacyPreviewDto> PreviewAsync(LegacyPreviewInput input)
    {
        await CheckKind(input.Kind);
        var data = await source.ReadAsync(TenantId, input.Kind);
        var rows = await Review(input.Kind, data);
        var batch = await batches.InsertAsync(new LegacyImportBatch(TenantId, input.Kind, Hash(data), Actor, Now));
        await Audit("LegacyImport.ReviewPrepared", batch.Id);
        return new(batch.Id, batch.Kind, batch.ExpiresAt, batch.Fingerprint, rows, rows.All(x => x.Status != "Conflict"), false, data.SourceTenantId, TenantId);
    }

    async Task<List<LegacyReviewRow>> Review(string kind, LegacySource data)
    {
        var nativeWorkers = await workers.GetListAsync();
        var result = new List<LegacyReviewRow>();
        if (kind == "Employees")
            foreach (var employee in data.Employees)
            {
                var existing = nativeWorkers.FirstOrDefault(x => x.Id == employee.Id);
                string? conflict = null;
                if (existing != null)
                {
                    result.Add(new(employee.Id, employee.Number + " · " + employee.Name, existing.GetProperty<string?>("LegacySourceFingerprint") == Hash(employee) ? "Already imported" : "Conflict", "Existing native employee records will not be overwritten."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(employee.Number) || employee.Number.Length > 64 || string.IsNullOrWhiteSpace(employee.Name) || employee.Name.Length > 160 || employee.Email.Length > 254 || employee.Department.Length > 160 || employee.Position.Length > 160 || employee.Started == default || employee.Exited < employee.Started || string.IsNullOrWhiteSpace(employee.ContractType) || employee.ContractType.Length > 64)
                    conflict = "Invalid employee fields require correction in the source.";
                if (nativeWorkers.Any(x => x.Number.Equals(employee.Number, StringComparison.OrdinalIgnoreCase)) || data.Employees.Count(x => x.Number.Equals(employee.Number, StringComparison.OrdinalIgnoreCase)) > 1)
                    conflict = "Employee number conflicts with another record.";
                if (employee.ManagerId.HasValue && !data.Employees.Any(x => x.Id == employee.ManagerId) && !nativeWorkers.Any(x => x.Id == employee.ManagerId))
                    conflict = "Reporting manager is missing. Import the manager first.";
                var seen = new HashSet<Guid>
                {
                    employee.Id
                };
                var manager = employee.ManagerId;
                var managerRow = data.Employees.FirstOrDefault(x => x.Id == manager);
                var nativeManager = nativeWorkers.FirstOrDefault(x => x.Id == manager);
                if (manager.HasValue && ((managerRow != null && (managerRow.Started > Today || managerRow.Exited <= Today)) || (managerRow == null && nativeManager != null && !nativeManager.IsActive(Today))))
                    conflict = "Reporting manager is inactive. Reconcile historical reporting lines before importing.";
                while (manager.HasValue)
                {
                    if (!seen.Add(manager.Value))
                    {
                        conflict = "Circular reporting relationship.";
                        break;
                    }

                    var ancestor = nativeWorkers.FirstOrDefault(x => x.Id == manager.Value);
                    manager = ancestor != null ? ancestor.ManagerId : data.Employees.FirstOrDefault(x => x.Id == manager.Value)?.ManagerId;
                }

                result.Add(new(employee.Id, employee.Number + " · " + employee.Name, conflict == null ? "Ready" : "Conflict", conflict ?? $"Start: {employee.Started:yyyy-MM-dd}; end: {employee.Exited?.ToString("yyyy-MM-dd") ?? "ongoing"}; department: {employee.Department}; position: {employee.Position}; manager: {employee.ManagerId?.ToString() ?? "top level"}."));
            }
        else
        {
            var native = await accounts.GetListAsync();
            foreach (var account in data.Accounts)
            {
                var existing = native.FirstOrDefault(x => x.Email.Equals(account.Email, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    result.Add(new(account.Id, account.Email, "Already exists", "Existing native account keeps its password, employee link and roles."));
                    continue;
                }

                var roles = Roles(account);
                string? conflict = null;
                if (!System.Net.Mail.MailAddress.TryCreate(account.Email, out _) || roles.Length == 0 || roles.Any(x => !new[] { "Admin", "HR", "Manager", "Employee" }.Contains(x)))
                    conflict = "Invalid email or unrecognised role. Correct the source first.";
                if (data.Accounts.Count(x => x.Email.Equals(account.Email, StringComparison.OrdinalIgnoreCase)) > 1)
                    conflict = "Duplicate source email.";
                if (account.EmployeeId.HasValue && !nativeWorkers.Any(x => x.Id == account.EmployeeId) || roles.Any(x => x is "Employee" or "Manager") && !account.EmployeeId.HasValue)
                    conflict = "Employee link is unresolved. HR must import employees first.";
                result.Add(new(account.Id, account.Email, conflict == null ? "Ready" : "Conflict", conflict ?? $"Create {(account.Active ? "active" : "inactive")} account; roles: {string.Join(", ", roles)}; employee: {nativeWorkers.FirstOrDefault(x => x.Id == account.EmployeeId)?.Name ?? "not linked"}. Issue a temporary password; change required at first sign-in."));
            }
        }

        return result;
    }

    public virtual async Task<LegacyCommitDto> CommitAsync(Guid id, LegacyCommitInput input) => await InSerializableTransaction<LegacyCommitDto>(async () =>
    {
        var batch = await Get(batches, id);
        await CheckKind(batch.Kind);
        Require(input.Confirmed && input.Fingerprint == batch.Fingerprint, "Confirm the exact reviewed source snapshot.");
        batch.Commit(Actor, Now);
        var data = await source.ReadAsync(TenantId, batch.Kind);
        Require(Hash(data) == batch.Fingerprint, "Legacy data changed. Prepare a new import review.");
        var review = await Review(batch.Kind, data);
        Require(review.All(x => x.Status != "Conflict"), "Resolve the import conflicts first.");
        var credentials = new List<TemporaryCredential>();
        var ready = review.Where(x => x.Status == "Ready").Select(x => x.SourceId).ToHashSet();
        if (batch.Kind == "Employees")
        {
            foreach (var employee in data.Employees.Where(x => ready.Contains(x.Id)))
            {
                var row = MataHR.Employees.Employee.ImportLegacy(employee.Id, TenantId, employee.Number, employee.Name, employee.Email, employee.Started, employee.Exited, employee.ContractType);
                row.SetProperty("LegacySourceFingerprint", Hash(employee));
                row.SetProperty("LegacyDepartment", employee.Department);
                row.SetProperty("LegacyPosition", employee.Position);
                await workers.InsertAsync(row);
            }

            await Flush();
            foreach (var employee in data.Employees.Where(x => ready.Contains(x.Id)))
            {
                Guid? position = null;
                if (!string.IsNullOrWhiteSpace(employee.Position))
                {
                    var office = await offices.FindAsync(x => x.Name == "Imported legacy organisation") ?? await offices.InsertAsync(New(new Branch("Imported legacy organisation")));
                    var department = string.IsNullOrWhiteSpace(employee.Department) ? "Legacy" : employee.Department;
                    var team = await teams.FindAsync(x => x.Name == department && x.BranchId == office.Id) ?? await teams.InsertAsync(New(new Department(office.Id, department)));
                    var job = await jobs.FindAsync(x => x.Name == employee.Position && x.DepartmentId == team.Id) ?? await jobs.InsertAsync(New(new Position(team.Id, employee.Position)));
                    position = job.Id;
                }

                await Flush();
                var row = await GetEmployee(employee.Id);
                await reporting.AssignAsync(row, position, employee.ManagerId, Today);
                await workers.UpdateAsync(row);
            }
        }
        else
            foreach (var account in data.Accounts.Where(x => ready.Contains(x.Id)))
            {
                var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "aA1!";
                var row = new IdentityUser(Guid.NewGuid(), account.Email.Trim(), account.Email.Trim(), TenantId);
                row.SetIsActive(account.Active);
                row.SetProperty("EmployeeId", account.EmployeeId);
                row.SetProperty("RequirePasswordChange", true);
                row.SetProperty("LegacySourceId", account.Id.ToString());
                var created = await users.CreateAsync(row, password);
                Require(created.Succeeded, "Account import failed: " + string.Join(",", created.Errors.Select(x => x.Code)));
                var assigned = await users.SetRolesAsync(row, Roles(account));
                Require(assigned.Succeeded, "Role assignment failed. The import was rolled back.");
                credentials.Add(new(account.Email, password));
            }

        await batches.UpdateAsync(batch);
        await Audit("LegacyImport.Committed", batch.Id, batch.Kind + "; imported=" + ready.Count + "; skipped=" + (review.Count - ready.Count));
        await Flush();
        return new(ready.Count, review.Count - ready.Count, credentials);
    });
}
