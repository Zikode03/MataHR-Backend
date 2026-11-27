using MataHR.Common;
using MataHR.Employees;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MataHR.Organisation;
public class ReportingLineManager(IRepository<Employee, Guid> workers, IRepository<Position, Guid> positions) : DomainService
{
    public async Task AssignAsync(Employee employee, Guid? position, Guid? manager, DateOnly today)
    {
        if (position.HasValue)
        {
            var job = await positions.FindAsync(position.Value);
            DomainGuard.Against(job == null || job.TenantId != employee.TenantId, "PositionNotFound", "Position does not exist in this organisation.");
        }

        // Follow every ancestor: checking only the immediate manager would miss longer reporting cycles.
        var seen = new HashSet<Guid>
        {
            employee.Id
        };
        var next = manager;
        while (next.HasValue)
        {
            DomainGuard.Against(!seen.Add(next.Value), "ReportingCycle", "Circular reporting relationship.");
            var parent = await workers.FindAsync(next.Value);
            DomainGuard.Against(parent == null || parent.TenantId != employee.TenantId, "ManagerNotFound", "Manager does not exist in this organisation.");
            DomainGuard.Against(!parent!.IsActive(today), "InactiveManager", "Reporting manager must be active.");
            next = parent.ManagerId;
        }

        employee.Assign(position, manager);
    }
}
