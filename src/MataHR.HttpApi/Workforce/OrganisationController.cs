using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.AspNetCore.Mvc;
using MataHR.Workforce;
using MataHR.Workforce.Contracts;

namespace MataHR.HttpApi;
[Authorize, Route("api/organisation"), ApiController]
public class OrganisationController(IOrganisationAppService service) : AbpController
{
    [HttpGet("structure")]
    public Task<StructureDto> Get() => service.GetAsync();
    [HttpPost("branches")]
    public Task<BranchDto> Branch(BranchInput input) => service.CreateBranchAsync(input);
    [HttpPost("departments")]
    public Task<DepartmentDto> Department(DepartmentInput input) => service.CreateDepartmentAsync(input);
    [HttpPost("positions")]
    public Task<PositionDto> Position(PositionInput input) => service.CreatePositionAsync(input);
    [HttpPut("assignments")]
    public Task<EmployeeDto> Assign(AssignmentInput input) => service.AssignAsync(input);
    [HttpPut("canvas")]
    public Task Canvas(ChartPointDto input) => service.SaveCanvasAsync(input);
    [HttpPut("layout")]
    public Task Layout(List<LayoutDto> input) => service.SaveLayoutAsync(input);
}
