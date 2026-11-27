using Volo.Abp.Modularity;

namespace MataHR.Foundation;

// Keep shared workflow states available without loading domain entities or persistence.
public sealed class MataHrDomainSharedModule : AbpModule
{
}
