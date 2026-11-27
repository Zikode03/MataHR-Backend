using MataHR.Common.Contracts;
using MataHR.Documents.Contracts;
using Volo.Abp.Application.Services;
namespace MataHR.Payroll.Contracts;
public interface IPayrollAppService : IApplicationService
{
    Task<DocumentContent> DownloadProviderDocumentAsync(Guid id);
    Task<List<DocumentDto>> GetProviderDocumentsAsync();
    Task<List<CompensationDto>> GetSalariesAsync();
    Task SetSalaryAsync(CompensationInput input);
    Task<List<BenefitDto>> GetBenefitsAsync();
    Task<BenefitDto> CreateBenefitAsync(BenefitInput input);
    Task<PayrollReadiness> GetReadinessAsync(string period);
    Task<List<RunDto>> GetRunsAsync();
    Task<RunDto> PrepareAsync(RunInput input);
    Task<RunDto> TransitionAsync(Guid id, DecisionInput input);
    Task<ExportDto> ExportAsync(Guid id);
    Task<PayslipDto> AttachPayslipAsync(Guid id, PayslipInput input);
    Task<PayslipDto> ReleasePayslipAsync(Guid id);
    Task<List<PayslipDto>> GetPayslipsAsync();
    Task<DocumentContent> DownloadPayslipAsync(Guid id);
}
