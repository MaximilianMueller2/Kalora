using Kalora.Application.Common;

namespace Kalora.Application.Staff;

/// <summary>Staff members (calendar columns) and the services they offer.</summary>
public interface IStaffService
{
    /// <summary>All staff of the salon, active ones first in display order.</summary>
    Task<Result<IReadOnlyList<StaffDto>>> ListAsync(CancellationToken cancellationToken = default);

    Task<Result<StaffDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<StaffDto>> CreateAsync(CreateStaffRequest request, CancellationToken cancellationToken = default);

    Task<Result<StaffDto>> UpdateAsync(Guid id, SaveStaffRequest request, CancellationToken cancellationToken = default);

    Task<Result<StaffSkills>> GetSkillsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<StaffSkills>> ReplaceSkillsAsync(Guid id, StaffSkills skills, CancellationToken cancellationToken = default);
}
