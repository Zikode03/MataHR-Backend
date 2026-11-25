using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.AspNetCore.Mvc;
using MataHR.Workforce;
using MataHR.Workforce.Contracts;

namespace MataHR.HttpApi;
[Authorize, Route("api/dashboard"), ApiController]
public class DashboardController(IDashboardAppService service) : AbpController
{
    [HttpGet("")]
    public Task<DashboardDto> Get(DateOnly from, DateOnly to) => service.GetAsync(from, to);
}
