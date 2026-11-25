using Volo.Abp.Uow;

namespace MataHR.Workforce;
[AttributeUsage(AttributeTargets.Method)]
public sealed class SerializableCommandAttribute : UnitOfWorkAttribute
{
    public SerializableCommandAttribute()
    {
        IsTransactional = true;
        IsolationLevel = System.Data.IsolationLevel.Serializable;
    }
}
