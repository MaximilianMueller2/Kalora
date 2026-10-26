using Kalora.Application.Common;

namespace Kalora.Application.Staff;

/// <summary>
/// Working hours and absences of staff members. Owners and managers handle everyone; a staff member
/// reads their own hours and manages their own absences.
/// </summary>
public interface IStaffScheduleService
{
    Task<Result<StaffSchedule>> GetScheduleAsync(Guid staffId, CancellationToken cancellationToken = default);

    Task<Result<StaffSchedule>> ReplaceScheduleAsync(Guid staffId, SaveStaffScheduleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Current and upcoming absences.</summary>
    Task<Result<IReadOnlyList<AbsenceDto>>> ListAbsencesAsync(Guid staffId, CancellationToken cancellationToken = default);

    Task<Result<AbsenceCreated>> CreateAbsenceAsync(Guid staffId, CreateAbsenceRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteAbsenceAsync(Guid staffId, Guid absenceId, CancellationToken cancellationToken = default);
}
