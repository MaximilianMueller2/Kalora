using System.Text.RegularExpressions;
using FluentValidation;
using Kalora.Application.Catalog;

namespace Kalora.Application.Staff;

internal static partial class StaffRules
{
    public const int MaxAbsenceDays = 366;

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    public static partial Regex HexColor();
}

internal sealed class CreateStaffRequestValidator : AbstractValidator<CreateStaffRequest>
{
    public CreateStaffRequestValidator()
    {
        RuleFor(r => r.DisplayName).NotEmpty().WithErrorCode("validation.required")
            .MaximumLength(100).WithErrorCode("validation.too_long");
        RuleFor(r => r.Color).Must(color => color is not null && StaffRules.HexColor().IsMatch(color)).WithErrorCode("staff.color_invalid");
    }
}

internal sealed class SaveStaffRequestValidator : AbstractValidator<SaveStaffRequest>
{
    public SaveStaffRequestValidator()
    {
        RuleFor(r => r.DisplayName).NotEmpty().WithErrorCode("validation.required")
            .MaximumLength(100).WithErrorCode("validation.too_long");
        RuleFor(r => r.Color).Must(color => color is not null && StaffRules.HexColor().IsMatch(color)).WithErrorCode("staff.color_invalid");
    }
}

internal sealed class StaffSkillsValidator : AbstractValidator<StaffSkills>
{
    public StaffSkillsValidator()
    {
        RuleFor(s => s.Skills).NotNull().WithErrorCode("validation.required");
        RuleFor(s => s.Skills).Must(skills => skills is null || skills.Select(s => s.ServiceId).Distinct().Count() == skills.Count)
            .WithErrorCode("staff.skills_duplicate");
        RuleForEach(s => s.Skills).Must(skill => skill.DurationOverrideMinutes is null || CatalogRules.IsValidDuration(skill.DurationOverrideMinutes.Value))
            .WithErrorCode("service.duration_invalid");
    }
}

internal sealed class CreateAbsenceRequestValidator : AbstractValidator<CreateAbsenceRequest>
{
    public CreateAbsenceRequestValidator()
    {
        RuleFor(r => r.Type).IsInEnum().WithErrorCode("validation.invalid");
        RuleFor(r => r.EndDate).Must((request, end) => end >= request.StartDate).WithErrorCode("absence.range_invalid");
        RuleFor(r => r.EndDate).Must((request, end) => end.DayNumber - request.StartDate.DayNumber < StaffRules.MaxAbsenceDays)
            .WithErrorCode("absence.range_too_long");
    }
}
