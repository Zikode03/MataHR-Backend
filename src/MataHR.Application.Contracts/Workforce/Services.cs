using Volo.Abp.Application.Services;

namespace MataHR.Workforce.Contracts;
public interface IAccountsAppService : IApplicationService
{
    Task<List<EmployeeLinkDto>> GetEmployeeLinksAsync();
    Task<List<RoleGrantDto>> GetRoleGrantsAsync(string role);
    Task SetRolePermissionAsync(RolePermissionInput input);
    Task<AccountDto> UpdateAsync(Guid id, AccountUpdateInput input);
    Task<Page<AccountDto>> GetListAsync(PageQuery input);
    Task<AccountDto> CreateAsync(AccountInput input);
    Task<AccountDto> SetActiveAsync(Guid id, ActiveAccountInput input);
    Task<Page<AuditDto>> GetAuditAsync(PageQuery input);
}

public interface IAttendanceAppService : IApplicationService
{
    Task<LocationDto> ConfigureBoundaryAsync(Guid id, SiteBoundaryInput input);
    Task<PresenceCodeDto> GeneratePresenceCodeAsync(Guid id);
    Task<SessionDto?> GetCurrentAsync();
    Task<List<LocationDto>> GetLocationsAsync();
    Task<LocationDto> CreateLocationAsync(LocationInput input);
    Task<List<ScheduleDto>> GetSchedulesAsync();
    Task<ScheduleDto> SetScheduleAsync(ScheduleInput input);
    Task<Page<SessionDto>> GetListAsync(DateOnly from, DateOnly to, PageQuery input);
    Task<SessionDto> ClockInAsync(ClockInput input);
    Task<SessionDto> ClockOutAsync();
    Task<SessionDto> ReviewAsync(Guid id, DecisionInput input);
    Task<List<TimeRequestDto>> GetRequestsAsync(DateOnly from, DateOnly to);
    Task<TimeRequestDto> SubmitRequestAsync(TimeRequestInput input);
    Task<TimeRequestDto> ReviewRequestAsync(Guid id, DecisionInput input);
}

public interface IDashboardAppService : IApplicationService
{
    Task<DashboardDto> GetAsync(DateOnly from, DateOnly to);
}

public interface IDocumentsAppService : IApplicationService
{
    Task<List<DocumentDto>> GetListAsync(Guid employeeId);
    Task<DocumentDto> UploadAsync(DocumentInput input);
    Task<DocumentDto> VerifyAsync(Guid id, DocumentVerificationInput input);
    Task<DocumentContent> DownloadAsync(Guid id);
}

public interface IEmployeesAppService : IApplicationService
{
    Task<Page<EmployeeDto>> GetListAsync(PageQuery input);
    Task<EmployeeDto> GetAsync(Guid id);
    Task<EmployeeDto> CreateAsync(EmployeeInput input);
    Task<EmployeeDto> UpdateAsync(Guid id, EmployeeInput input);
    Task<List<EmployeeHistoryDto>> GetHistoryAsync(Guid id);
    Task<List<ContractDto>> GetContractsAsync(Guid id);
    Task<ContractDto> CreateContractAsync(ContractInput input);
    Task<ContractDto> TransitionContractAsync(Guid id, ContractDecision input);
}

public interface ILeaveAppService : IApplicationService
{
    Task<List<PolicyDto>> GetPoliciesAsync();
    Task<PolicyDto> CreatePolicyAsync(PolicyInput input);
    Task<List<HolidayInput>> GetHolidaysAsync(int year);
    Task AddHolidayAsync(HolidayInput input);
    Task SetAllocationAsync(AllocationInput input);
    Task<List<BalanceDto>> GetBalancesAsync(Guid employeeId, int year);
    Task<Page<LeaveDto>> GetListAsync(PageQuery input);
    Task<LeaveDto> SubmitAsync(LeaveInput input, Guid? employeeId = null);
    Task<LeaveDto> ReviewAsync(Guid id, DecisionInput input);
    Task<LeaveDto> WithdrawAsync(Guid id, string concurrencyStamp);
    Task<LeaveDto> RequestCancellationAsync(Guid id, string concurrencyStamp);
}

public interface IOrganisationAppService : IApplicationService
{
    Task SaveCanvasAsync(ChartPointDto input);
    Task<StructureDto> GetAsync();
    Task<BranchDto> CreateBranchAsync(BranchInput input);
    Task<DepartmentDto> CreateDepartmentAsync(DepartmentInput input);
    Task<PositionDto> CreatePositionAsync(PositionInput input);
    Task<EmployeeDto> AssignAsync(AssignmentInput input);
    Task SaveLayoutAsync(List<LayoutDto> input);
}

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
