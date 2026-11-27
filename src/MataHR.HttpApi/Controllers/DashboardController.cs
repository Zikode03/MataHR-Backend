using MataHR.Dashboard.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.AspNetCore.Mvc;

namespace MataHR.HttpApi;

// Dashboard figures come from the application service so callers get totals scoped to their access.
[Authorize, Route("api/dashboard"), ApiController]
public class DashboardController(IDashboardAppService service) : AbpController
{
    [HttpGet("")]
    public Task<DashboardDto> Get(DateOnly from, DateOnly to) => service.GetAsync(from, to);
}
