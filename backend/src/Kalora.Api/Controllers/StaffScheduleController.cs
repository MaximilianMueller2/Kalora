using Kalora.Api.Http;
using Kalora.Application.Identity;
using Kalora.Application.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kalora.Api.Controllers;

/// <summary>Who may do what with which staff member is decided by the service (managers: all, staff: only themselves).</summary>
[Route("api/staff/{staffId:guid}")]
[Authorize(Policy = AuthPolicies.SalonMember)]
public sealed class StaffScheduleController(IStaffScheduleService schedule) : ApiControllerBase
{
    [HttpGet("working-hours")]
    public async Task<IActionResult> GetSchedule(Guid staffId, CancellationToken cancellationToken) =>
        ToActionResult(await schedule.GetScheduleAsync(staffId, cancellationToken));

    [HttpPut("working-hours")]
    public async Task<IActionResult> ReplaceSchedule(Guid staffId, SaveStaffScheduleRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await schedule.ReplaceScheduleAsync(staffId, request, cancellationToken));

    [HttpGet("absences")]
    public async Task<IActionResult> ListAbsences(Guid staffId, CancellationToken cancellationToken) =>
        ToActionResult(await schedule.ListAbsencesAsync(staffId, cancellationToken));

    [HttpPost("absences")]
    public async Task<IActionResult> CreateAbsence(Guid staffId, CreateAbsenceRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await schedule.CreateAbsenceAsync(staffId, request, cancellationToken));

    [HttpDelete("absences/{absenceId:guid}")]
    public async Task<IActionResult> DeleteAbsence(Guid staffId, Guid absenceId, CancellationToken cancellationToken) =>
        ToActionResult(await schedule.DeleteAbsenceAsync(staffId, absenceId, cancellationToken));
}
