using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MataHR.Common;
public abstract class WorkforceRow : AuditedAggregateRoot<Guid>, IWorkforceEntity
{
    public Guid? TenantId { get; set; }

    protected WorkforceRow() : base(Guid.NewGuid())
    {
    }
}

public interface IWorkforceEntity : Volo.Abp.Domain.Entities.IEntity<Guid>, IMultiTenant, Volo.Abp.Domain.Entities.IHasConcurrencyStamp
{
    new Guid? TenantId { get; set; }
}

public abstract class WorkforceEntity : Volo.Abp.Domain.Entities.Auditing.AuditedEntity<Guid>, IWorkforceEntity
{
    public Guid? TenantId { get; set; }
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");

    protected WorkforceEntity() : base(Guid.NewGuid())
    {
    }
}
