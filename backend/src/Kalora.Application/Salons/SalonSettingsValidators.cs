using System.Text.RegularExpressions;
using FluentValidation;
using Kalora.Application.Common;
using Kalora.Domain.Salons;

namespace Kalora.Application.Salons;

internal static partial class Rules
{
    public static readonly int[] SlotIntervals = [5, 10, 15, 30];

    public static bool IsValidTimeZone(string id) =>
        (id.Contains('/') || id == "UTC") && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);

    [GeneratedRegex("^[0-9+()./ -]{3,50}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    public static partial Regex Phone();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    public static partial Regex Slug();
}

internal sealed class SalonProfileValidator : AbstractValidator<SalonProfile>
{
    public SalonProfileValidator()
    {
        RuleFor(p => p.Name).NotEmpty().WithErrorCode("validation.required")
            .MaximumLength(200).WithErrorCode("validation.too_long");
        RuleFor(p => p.Slug)
            .Must(slug => slug is { Length: >= 3 and <= SlugGenerator.MaxLength } && Rules.Slug().IsMatch(slug))
            .WithErrorCode("settings.slug_invalid")
            .Must(slug => !SlugGenerator.IsReserved(slug)).WithErrorCode("settings.slug_reserved");
        RuleFor(p => p.Industry).IsInEnum().WithErrorCode("validation.invalid");
        RuleFor(p => p.Street).MaximumLength(200).WithErrorCode("validation.too_long");
        RuleFor(p => p.PostalCode).MaximumLength(20).WithErrorCode("validation.too_long");
        RuleFor(p => p.City).MaximumLength(100).WithErrorCode("validation.too_long");
        RuleFor(p => p.Phone).Must(phone => phone == string.Empty || Rules.Phone().IsMatch(phone))
            .WithErrorCode("settings.phone_invalid");
        RuleFor(p => p.TimeZone).Must(Rules.IsValidTimeZone).WithErrorCode("settings.timezone_invalid");
        RuleFor(p => p.DefaultLanguage).SupportedLanguage();
        RuleFor(p => p.ImprintText).MaximumLength(4000).WithErrorCode("validation.too_long");
        RuleFor(p => p.PrivacyPolicyUrl)
            .Must(url => string.IsNullOrEmpty(url)
                || (url.Length <= 500 && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
            .WithErrorCode("settings.url_invalid");
    }
}

internal sealed class BookingRulesValidator : AbstractValidator<BookingRules>
{
    public BookingRulesValidator()
    {
        RuleFor(r => r.SlotIntervalMinutes).Must(Rules.SlotIntervals.Contains).WithErrorCode("settings.slot_interval_invalid");
        RuleFor(r => r.MinLeadTimeMinutes).InclusiveBetween(0, 7 * 24 * 60).WithErrorCode("settings.value_out_of_range");
        RuleFor(r => r.BookingHorizonDays).InclusiveBetween(1, 365).WithErrorCode("settings.value_out_of_range");
        RuleFor(r => r.CancellationDeadlineHours).InclusiveBetween(0, 7 * 24).WithErrorCode("settings.value_out_of_range");
        RuleFor(r => r.RequestExpiryHours).InclusiveBetween(1, 72).WithErrorCode("settings.value_out_of_range");
        RuleFor(r => r.RetentionMonths).InclusiveBetween(1, 120).WithErrorCode("settings.value_out_of_range");
    }
}

internal sealed class OpeningHoursPlanValidator : AbstractValidator<OpeningHoursPlan>
{
    public const int MaxBlocksPerDay = 4;

    public OpeningHoursPlanValidator()
    {
        RuleFor(plan => plan.Days).NotNull().WithErrorCode("validation.required");
        RuleFor(plan => plan).Custom((plan, context) =>
        {
            var seen = new HashSet<DayOfWeek>();
            foreach (var day in plan.Days ?? [])
            {
                var name = day.Weekday.ToString();
                if (!Enum.IsDefined(day.Weekday) || !seen.Add(day.Weekday))
                {
                    context.AddFailure(new FluentValidation.Results.ValidationFailure(name, "opening_hours.duplicate_day") { ErrorCode = "opening_hours.duplicate_day" });
                    return;
                }

                var failure = FirstProblem(day.Blocks ?? []);
                if (failure is not null)
                {
                    context.AddFailure(new FluentValidation.Results.ValidationFailure(name, failure) { ErrorCode = failure });
                    return;
                }
            }
        });
    }

    /// <summary>Blocks may touch (9-13, 13-18) but not overlap, and each must end after it starts.</summary>
    private static string? FirstProblem(IReadOnlyList<OpeningBlock> blocks)
    {
        if (blocks.Count > MaxBlocksPerDay)
        {
            return "opening_hours.too_many_blocks";
        }

        if (blocks.Any(block => block.End <= block.Start))
        {
            return "opening_hours.block_invalid";
        }

        var ordered = blocks.OrderBy(block => block.Start).ToList();
        for (var index = 1; index < ordered.Count; index++)
        {
            if (ordered[index].Start < ordered[index - 1].End)
            {
                return "opening_hours.overlap";
            }
        }

        return null;
    }
}

internal sealed class CreateClosureRequestValidator : AbstractValidator<CreateClosureRequest>
{
    public const int MaxDays = 62;

    public CreateClosureRequestValidator()
    {
        RuleFor(r => r.Reason).MaximumLength(500).WithErrorCode("validation.too_long");
        RuleFor(r => r.EndDate).Must((request, end) => end is null || end >= request.Date).WithErrorCode("closure.range_invalid");
        RuleFor(r => r.EndDate).Must((request, end) => end is null || end.Value.DayNumber - request.Date.DayNumber < MaxDays)
            .WithErrorCode("closure.range_too_long");
    }
}
