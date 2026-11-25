using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.AspNetCore.Mvc;
using MataHR.Workforce;
using MataHR.Workforce.Contracts;

namespace MataHR.HttpApi;
[Authorize, Route("api/payroll"), ApiController]
public class PayrollController(IPayrollAppService service) : AbpController
{
    [Authorize(WorkforcePermissions.PayrollManage),HttpGet("documents/{id:guid}/download")]
    public Task<DocumentContent> ProviderDocument(Guid id)=>service.DownloadProviderDocumentAsync(id);
    [HttpGet("documents")]
    public Task<List<DocumentDto>> ProviderDocuments() => service.GetProviderDocumentsAsync();
    [HttpGet("salaries")]
    public Task<List<CompensationDto>> Salaries() => service.GetSalariesAsync();
    [HttpPut("salaries")]
    public Task Salary(CompensationInput input) => service.SetSalaryAsync(input);
    [HttpGet("benefits")]
    public Task<List<BenefitDto>> Benefits() => service.GetBenefitsAsync();
    [HttpPost("benefits")]
    public Task<BenefitDto> Benefit(BenefitInput input) => service.CreateBenefitAsync(input);
    [HttpGet("readiness")]
    public Task<PayrollReadiness> Readiness(string period) => service.GetReadinessAsync(period);
    [HttpGet("runs")]
    public Task<List<RunDto>> Runs() => service.GetRunsAsync();
    [HttpPost("runs")]
    public Task<RunDto> Prepare(RunInput input) => service.PrepareAsync(input);
    [HttpPost("runs/{id:guid}/transition")]
    public Task<RunDto> Transition(Guid id, DecisionInput input) => service.TransitionAsync(id, input);
    [HttpGet("runs/{id:guid}/export")]
    public Task<ExportDto> Export(Guid id) => service.ExportAsync(id);
    [HttpPost("runs/{id:guid}/payslips")]
    public Task<PayslipDto> Attach(Guid id, PayslipInput input) => service.AttachPayslipAsync(id, input);
    [HttpPost("payslips/{id:guid}/release")]
    public Task<PayslipDto> Release(Guid id) => service.ReleasePayslipAsync(id);
    [HttpGet("payslips")]
    public Task<List<PayslipDto>> Payslips() => service.GetPayslipsAsync();
    [HttpGet("payslips/{id:guid}/download")]
    public Task<DocumentContent> Download(Guid id) => service.DownloadPayslipAsync(id);
}
