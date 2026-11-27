using Volo.Abp.Application.Services;
namespace MataHR.Dashboard.Contracts;
public interface IDashboardAppService : IApplicationService
{
    Task<DashboardDto> GetAsync(DateOnly from, DateOnly to);
}
