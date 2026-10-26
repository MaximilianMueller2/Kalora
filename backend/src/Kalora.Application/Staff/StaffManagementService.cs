using FluentValidation;
using Kalora.Application.Catalog;
using Kalora.Application.Common;
using Kalora.Application.Identity;
using Kalora.Domain.Salons;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Application.Staff;

internal sealed class StaffManagementService(
    IAppDbContext db,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<CreateStaffRequest> createValidator,
    IValidator<SaveStaffRequest> saveValidator,
    IValidator<StaffSkills> skillsValidator) : IStaffService
{
    public async Task<Result<IReadOnlyList<StaffDto>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var members = await db.StaffMembers.OrderByDescending(s => s.IsActive).ThenBy(s => s.SortOrder).ThenBy(s => s.DisplayName).ToListAsync(cancellationToken);
        return await ToDtosAsync(members, cancellationToken);
    }

    public async Task<Result<StaffDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var staff = await db.StaffMembers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (staff is null)
        {
            return Error.NotFound("staff.not_found");
        }

        return (await ToDtosAsync([staff], cancellationToken))[0];
    }

    public async Task<Result<StaffDto>> CreateAsync(CreateStaffRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsManager)
        {
            return Error.Forbidden();
        }

        var validation = createValidator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        Guid? userId = null;
        if (request.LinkToCurrentUser)
        {
            userId = currentUser.UserId;
            if (userId is null || await db.StaffMembers.AnyAsync(s => s.UserId == userId, cancellationToken))
            {
                return Error.Conflict("staff.already_linked");
            }
        }

        var nextOrder = await db.StaffMembers.Select(s => (int?)s.SortOrder).MaxAsync(cancellationToken) + 1 ?? 0;
        var staff = new StaffMember
        {
            DisplayName = request.DisplayName.Trim(),
            Color = request.Color.ToUpperInvariant(),
            IsBookableOnline = request.IsBookableOnline,
            AutoConfirm = request.AutoConfirm,
            UserId = userId,
            SortOrder = nextOrder,
        };
        db.StaffMembers.Add(staff);
        await db.SaveChangesAsync(cancellationToken);
        return (await ToDtosAsync([staff], cancellationToken))[0];
    }

    public async Task<Result<StaffDto>> UpdateAsync(Guid id, SaveStaffRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsManager)
        {
            return Error.Forbidden();
        }

        var validation = saveValidator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var staff = await db.StaffMembers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (staff is null)
        {
            return Error.NotFound("staff.not_found");
        }

        staff.DisplayName = request.DisplayName.Trim();
        staff.Color = request.Color.ToUpperInvariant();
        staff.IsBookableOnline = request.IsBookableOnline;
        staff.AutoConfirm = request.AutoConfirm;
        staff.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return (await ToDtosAsync([staff], cancellationToken))[0];
    }

    public async Task<Result<StaffSkills>> GetSkillsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await db.StaffMembers.AnyAsync(s => s.Id == id, cancellationToken))
        {
            return Error.NotFound("staff.not_found");
        }

        return await LoadSkillsAsync(id, cancellationToken);
    }

    public async Task<Result<StaffSkills>> ReplaceSkillsAsync(Guid id, StaffSkills skills, CancellationToken cancellationToken = default)
    {
        if (!IsManager)
        {
            return Error.Forbidden();
        }

        var validation = skillsValidator.Validate(skills);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        if (!await db.StaffMembers.AnyAsync(s => s.Id == id, cancellationToken))
        {
            return Error.NotFound("staff.not_found");
        }

        var wanted = skills.Skills.ToDictionary(s => s.ServiceId);
        var knownIds = await db.Services.Where(s => wanted.Keys.Contains(s.Id)).Select(s => s.Id).ToListAsync(cancellationToken);
        if (knownIds.Count != wanted.Count)
        {
            return Error.NotFound("service.not_found");
        }

        // Update, add and remove row by row: deleting and re-adding the same pair would hit the unique index.
        var existing = await db.StaffServices.Where(s => s.StaffId == id).ToListAsync(cancellationToken);
        db.StaffServices.RemoveRange(existing.Where(e => !wanted.ContainsKey(e.ServiceId)));
        foreach (var row in existing.Where(e => wanted.ContainsKey(e.ServiceId)))
        {
            row.DurationOverrideMinutes = wanted[row.ServiceId].DurationOverrideMinutes;
        }

        var present = existing.Select(e => e.ServiceId).ToHashSet();
        db.StaffServices.AddRange(wanted.Values.Where(s => !present.Contains(s.ServiceId)).Select(s => new StaffService
        {
            StaffId = id,
            ServiceId = s.ServiceId,
            DurationOverrideMinutes = s.DurationOverrideMinutes,
        }));
        await db.SaveChangesAsync(cancellationToken);
        return await LoadSkillsAsync(id, cancellationToken);
    }

    private bool IsManager => currentUser.SalonRole is SalonRole.Owner or SalonRole.Manager;

    private async Task<StaffSkills> LoadSkillsAsync(Guid id, CancellationToken cancellationToken)
    {
        var rows = await db.StaffServices.Where(s => s.StaffId == id).ToListAsync(cancellationToken);
        return new StaffSkills(rows.OrderBy(r => r.ServiceId).Select(r => new StaffSkill(r.ServiceId, r.DurationOverrideMinutes)).ToList());
    }

    private async Task<List<StaffDto>> ToDtosAsync(List<StaffMember> members, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var ids = members.Select(m => m.Id).ToList();
        var pending = (await db.StaffInvitations
            .Where(i => ids.Contains(i.StaffId) && i.AcceptedAtUtc == null && i.ExpiresAtUtc > now)
            .Select(i => i.StaffId)
            .ToListAsync(cancellationToken)).ToHashSet();
        var userIds = members.Where(m => m.UserId is not null).Select(m => m.UserId!.Value).ToList();
        var roles = await db.SalonMembers.Where(m => userIds.Contains(m.UserId)).ToDictionaryAsync(m => m.UserId, m => m.Role, cancellationToken);

        return members.Select(m => new StaffDto(
            m.Id,
            m.DisplayName,
            m.Color,
            m.IsBookableOnline,
            m.AutoConfirm,
            m.IsActive,
            m.SortOrder,
            m.UserId is not null,
            m.UserId is { } userId && roles.TryGetValue(userId, out var role) ? role : null,
            m.UserId is not null && m.UserId == currentUser.UserId,
            pending.Contains(m.Id))).ToList();
    }
}
