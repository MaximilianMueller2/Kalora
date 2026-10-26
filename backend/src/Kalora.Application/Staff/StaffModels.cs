using Kalora.Application.Salons;
using Kalora.Domain.Appointments;
using Kalora.Domain.Salons;
using Kalora.Domain.Staff;

namespace Kalora.Application.Staff;

/// <param name="HasLogin">A user account is linked to this calendar column.</param>
/// <param name="Role">Role of the linked account, if any.</param>
/// <param name="IsMe">The signed-in user is this staff member.</param>
/// <param name="InvitationPending">An invitation was sent and is neither accepted nor expired.</param>
public sealed record StaffDto(
    Guid Id,
    string DisplayName,
    string Color,
    bool IsBookableOnline,
    bool AutoConfirm,
    bool IsActive,
    int SortOrder,
    bool HasLogin,
    SalonRole? Role,
    bool IsMe,
    bool InvitationPending);

/// <param name="LinkToCurrentUser">The signed-in user takes this calendar column ("only me" salons).</param>
public sealed record CreateStaffRequest(
    string DisplayName,
    string Color,
    bool IsBookableOnline,
    bool AutoConfirm,
    bool LinkToCurrentUser);

public sealed record SaveStaffRequest(string DisplayName, string Color, bool IsBookableOnline, bool AutoConfirm, bool IsActive);

/// <param name="DurationOverrideMinutes">This person needs a different time than the service's default.</param>
public sealed record StaffSkill(Guid ServiceId, int? DurationOverrideMinutes);

/// <summary>The services a staff member offers; saving replaces the whole list.</summary>
public sealed record StaffSkills(IReadOnlyList<StaffSkill> Skills);

/// <summary>A weekly plan that applies from <paramref name="ValidFrom"/> until the next version (or open-ended).</summary>
public sealed record StaffScheduleVersion(DateOnly ValidFrom, DateOnly? ValidTo, IReadOnlyList<OpeningDay> Days);

/// <summary>The plan in effect today and the ones that start later, oldest first.</summary>
public sealed record StaffSchedule(IReadOnlyList<StaffScheduleVersion> Versions);

/// <summary>Replaces every plan from <paramref name="ValidFrom"/> on; earlier plans end the day before.</summary>
public sealed record SaveStaffScheduleRequest(DateOnly ValidFrom, IReadOnlyList<OpeningDay> Days);

/// <summary>Whole days in the salon's time zone, first and last day included.</summary>
public sealed record CreateAbsenceRequest(DateOnly StartDate, DateOnly EndDate, AbsenceType Type);

/// <param name="ConflictCount">Open or confirmed appointments that fall into the absence.</param>
public sealed record AbsenceDto(Guid Id, DateOnly StartDate, DateOnly EndDate, AbsenceType Type, int ConflictCount);

public sealed record AffectedAppointment(
    Guid Id,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string ServiceName,
    string CustomerName,
    AppointmentStatus Status);

/// <summary>The absence is saved even when appointments exist; they are listed so the salon can react.</summary>
public sealed record AbsenceCreated(AbsenceDto Absence, IReadOnlyList<AffectedAppointment> AffectedAppointments);
