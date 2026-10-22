using Kalora.Domain.Salons;

namespace Kalora.Application.Salons;

public sealed record SalonProfile(
    string Name,
    string Slug,
    SalonIndustry Industry,
    string Street,
    string PostalCode,
    string City,
    string Phone,
    string TimeZone,
    string DefaultLanguage,
    string? ImprintText,
    string? PrivacyPolicyUrl);

public sealed record BookingRules(
    int SlotIntervalMinutes,
    int MinLeadTimeMinutes,
    int BookingHorizonDays,
    int CancellationDeadlineHours,
    bool AutoConfirm,
    int RequestExpiryHours,
    int RetentionMonths);

public sealed record OpeningBlock(TimeOnly Start, TimeOnly End);

public sealed record OpeningDay(DayOfWeek Weekday, IReadOnlyList<OpeningBlock> Blocks);

/// <summary>The weekly opening hours; a weekday without blocks is a closed day.</summary>
public sealed record OpeningHoursPlan(IReadOnlyList<OpeningDay> Days);

public sealed record Closure(Guid Id, DateOnly Date, string? Reason);

/// <summary>One closed day, or every day from <see cref="Date"/> to <see cref="EndDate"/> (inclusive).</summary>
public sealed record CreateClosureRequest(DateOnly Date, DateOnly? EndDate, string? Reason);

public sealed record SlugSuggestion(string Slug);
