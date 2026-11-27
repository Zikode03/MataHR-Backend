using Volo.Abp.Uow;

namespace MataHR.Common;

// Use this for commands whose validation reads other rows that concurrent requests could also change.
[AttributeUsage(AttributeTargets.Method)]
public sealed class SerializableCommandAttribute : UnitOfWorkAttribute
{
    public SerializableCommandAttribute()
    {
        IsTransactional = true;
        IsolationLevel = System.Data.IsolationLevel.Serializable;
    }
}
