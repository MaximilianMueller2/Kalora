using Kalora.Domain.Salons;
using Microsoft.Extensions.Options;

namespace Kalora.Application.Configuration;

/// <summary>What each plan includes. The only place where the limits live; change them in the configuration, not in code.</summary>
public sealed class PlansOptions
{
    public const string SectionName = "Plans";

    /// <summary>Active staff members (calendar columns), the owner included if they have one.</summary>
    public int SoloMaxStaff { get; set; } = 1;

    public int TeamMaxStaff { get; set; } = 5;

    public int SalonPlusMaxStaff { get; set; } = 15;

    /// <summary>
    /// What a salon may have right now. During the trial everything of the biggest plan is open, so a team can try Kalora as it
    /// will use it; the plan only limits once the salon is Active (or past due).
    /// </summary>
    public int MaxStaff(Salon salon) =>
        salon.Status == SalonStatus.Trial ? Math.Max(SoloMaxStaff, Math.Max(TeamMaxStaff, SalonPlusMaxStaff)) : MaxStaff(salon.PlanCode);

    public int MaxStaff(PlanCode plan) => plan switch
    {
        PlanCode.Solo => SoloMaxStaff,
        PlanCode.Team => TeamMaxStaff,
        PlanCode.SalonPlus => SalonPlusMaxStaff,
        _ => throw new ArgumentOutOfRangeException(nameof(plan), plan, null),
    };
}

internal sealed class PlansOptionsValidator : IValidateOptions<PlansOptions>
{
    public ValidateOptionsResult Validate(string? name, PlansOptions options)
    {
        var errors = new List<string>();
        foreach (var plan in Enum.GetValues<PlanCode>())
        {
            if (options.MaxStaff(plan) < 1)
            {
                errors.Add($"{PlansOptions.SectionName}:{plan}MaxStaff must be at least 1.");
            }
        }

        if (options.SoloMaxStaff > options.TeamMaxStaff || options.TeamMaxStaff > options.SalonPlusMaxStaff)
        {
            errors.Add($"{PlansOptions.SectionName}: a bigger plan must not allow fewer staff than a smaller one.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
