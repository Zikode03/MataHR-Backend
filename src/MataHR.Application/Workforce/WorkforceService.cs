using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using MataHR.Workforce.Contracts;

namespace MataHR.Workforce;
[Authorize]
public abstract class WorkforceService : ApplicationService
{
    protected Volo.Abp.Users.ICurrentUser User => CurrentUser;
    protected Volo.Abp.MultiTenancy.ICurrentTenant Tenant => CurrentTenant;
    protected ICurrentPrincipalAccessor Principal => LazyServiceProvider.LazyGetRequiredService<ICurrentPrincipalAccessor>();
    protected Volo.Abp.Linq.IAsyncQueryableExecuter Queries => AsyncExecuter;
    protected IUnitOfWorkManager Units => UnitOfWorkManager;

    protected readonly IRepository<Worker, Guid> workers;
    protected readonly IRepository<WorkforceAudit, Guid> audits;
    protected WorkforceService(IRepository<Worker, Guid> workers, IRepository<WorkforceAudit, Guid> audits)
    {
        this.workers = workers;
        this.audits = audits;
    }

    protected Guid TenantId => Tenant.Id ?? throw new AbpAuthorizationException("Tenant required.");
    protected Guid Actor => User.Id ?? throw new AbpAuthorizationException();
    protected Guid? Own => Guid.TryParse(Principal.Principal?.FindFirst("employee")?.Value, out var id) ? id : null;
    protected bool Hr => User.IsInRole("HR");

    protected static void Require(bool ok, string message)
    {
        if (!ok)
            throw new UserFriendlyException(message);
    }

    protected static void Allow(bool ok)
    {
        if (!ok)
            throw new AbpAuthorizationException();
    }

    protected async Task<T> Get<T>(IRepository<T, Guid> repo, Guid id)
        where T : class, IWorkforceEntity
    {
        var row = await repo.FindAsync(id);
        return row is not null && row.TenantId == TenantId ? row : throw new EntityNotFoundException(typeof(T), id);
    }

    protected async Task<Worker> Worker(Guid id) => await Get(workers, id);
    protected async Task View(Guid id, bool confidential = false)
    {
        var w = await Worker(id);
        Allow(Hr || Own == id || (!confidential && User.IsInRole("Manager") && Own != null && w.ManagerId == Own));
    }

    protected async Task Review(Guid id)
    {
        var w = await Worker(id);
        Allow(Own != id && (Hr || (User.IsInRole("Manager") && Own != null && w.ManagerId == Own)));
    }

    protected async Task<IQueryable<Worker>> Visible()
    {
        var q = await workers.GetQueryableAsync();
        return q.Where(x => x.TenantId == TenantId && (Hr || x.Id == Own || (User.IsInRole("Manager") && Own != null && x.ManagerId == Own)));
    }

    protected T New<T>(T row)
        where T : class, IWorkforceEntity
    {
        row.TenantId = TenantId;
        return row;
    }

    protected Task Audit(string action, Guid target, string note = "") => audits.InsertAsync(New(new WorkforceAudit { ActorId = Actor, Action = action, TargetId = target, Note = note }));
    protected static void Stamp(Volo.Abp.Domain.Entities.IHasConcurrencyStamp row, string? stamp)
    {
        Require(!string.IsNullOrWhiteSpace(stamp), "Concurrency stamp required. Refresh the record.");
        if (row.ConcurrencyStamp != stamp)
            throw new Volo.Abp.Data.AbpDbConcurrencyException("This record changed. Refresh and retry.");
    }

    protected DateTimeOffset Now => LazyServiceProvider.LazyGetRequiredService<TimeProvider>().GetUtcNow();
    protected DateOnly Today => WorkforceRules.WorkDate(Now);

    protected static bool Active(Worker w, DateOnly day) => w.Started <= day && (w.Exited == null || day < w.Exited);
    protected static EmployeeDto Dto(Worker w) => new(w.Id, w.Number, w.Name, w.Email, w.Started, w.Exited, w.ContractType, w.ManagerId, w.PositionId, w.ConcurrencyStamp);
    protected static void PageBounds(PageQuery q) => Require(q.SkipCount >= 0 && q.MaxResultCount is >= 1 and <= 200, "Page size must be between 1 and 200.");
    protected async Task Flush() => await Units.Current!.SaveChangesAsync();
    protected async Task<T> InSerializableTransaction<T>(Func<Task<T>> command)
    {
        using var unit = Units.Begin(requiresNew: true, isTransactional: true, isolationLevel: System.Data.IsolationLevel.Serializable);
        var result = await command();
        await unit.CompleteAsync();
        return result;
    }

    protected async Task InSerializableTransaction(Func<Task> command)
    {
        using var unit = Units.Begin(requiresNew: true, isTransactional: true, isolationLevel: System.Data.IsolationLevel.Serializable);
        await command();
        await unit.CompleteAsync();
    }
}
