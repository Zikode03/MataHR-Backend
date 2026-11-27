using MataHR.Common.Contracts;
using Volo.Abp.Application.Services;
namespace MataHR.Attendance.Contracts;

// Expose attendance requests and results without exposing the stored presence evidence or repository types.
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
