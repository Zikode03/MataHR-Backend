using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.AspNetCore.Mvc;
using MataHR.Workforce;
using MataHR.Workforce.Contracts;
namespace MataHR.HttpApi;

[Authorize, Route("api/attendance"), ApiController]
public class AttendanceController(IAttendanceAppService service) : AbpController
{
    [HttpGet("locations")] public Task<List<LocationDto>> Locations() => service.GetLocationsAsync();
    [HttpPost("locations")] public Task<LocationDto> Location(LocationInput input) => service.CreateLocationAsync(input);
    [HttpGet("schedules")] public Task<List<ScheduleDto>> Schedules() => service.GetSchedulesAsync();
    [HttpPut("schedules")] public Task<ScheduleDto> Schedule(ScheduleInput input) => service.SetScheduleAsync(input);
    [HttpGet("")] public Task<Page<SessionDto>> Get(DateOnly from, DateOnly to, [FromQuery] PageQuery input) => service.GetListAsync(from, to, input);
    [HttpPost("clock-in")] public Task<SessionDto> ClockIn(ClockInput input) => service.ClockInAsync(input);
    [HttpPost("clock-out")] public Task<SessionDto> ClockOut() => service.ClockOutAsync();
    [HttpPost("{id:guid}/review")] public Task<SessionDto> Review(Guid id, DecisionInput input) => service.ReviewAsync(id, input);
    [HttpGet("requests")] public Task<List<TimeRequestDto>> Requests(DateOnly from, DateOnly to) => service.GetRequestsAsync(from, to);
    [HttpPost("requests")] public Task<TimeRequestDto> SubmitTimeRequest(TimeRequestInput input) => service.SubmitRequestAsync(input);
    [HttpPost("requests/{id:guid}/review")] public Task<TimeRequestDto> ReviewRequest(Guid id, DecisionInput input) => service.ReviewRequestAsync(id, input);
}
