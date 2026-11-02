using Kalora.Domain.Salons;

namespace Kalora.Application.Admin;

public sealed record AdminSalonListItem(
    Guid Id,
    string Name,
    string Slug,
    SalonStatus Status,
    PlanCode PlanCode,
    SalonAccess Access,
    int StaffCount,
    int StaffLimit,
    int AppointmentsThisMonth,
    DateTimeOffset? TrialEndsAtUtc,
    DateTimeOffset CreatedAtUtc,
    string? OwnerEmail,
    bool HasNote);

public sealed record AdminSalonList(IReadOnlyList<AdminSalonListItem> Items, bool HasMore);

/// <summary>Master data and subscription of a salon. No customer or appointment data.</summary>
public sealed record AdminSalonDetail(
    Guid Id,
    string Name,
    string Slug,
    string City,
    string Phone,
    string TimeZone,
    SalonStatus Status,
    PlanCode PlanCode,
    SalonAccess Access,
    int StaffCount,
    int StaffLimit,
    int AppointmentsThisMonth,
    DateTimeOffset? TrialEndsAtUtc,
    DateTimeOffset CreatedAtUtc,
    string? OwnerName,
    string? OwnerEmail,
    string? AdminNote);

public sealed record SetPlanRequest(PlanCode PlanCode);

public sealed record ExtendTrialRequest(int Days);

public sealed record SetStatusRequest(SalonStatus Status);

public sealed record SetNoteRequest(string? Note);
