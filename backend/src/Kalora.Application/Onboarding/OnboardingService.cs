using Kalora.Application.Auth;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Application.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalora.Application.Onboarding;

internal sealed class OnboardingService(IAppDbContext db, ICurrentUser currentUser, IOptions<AppOptions> appOptions) : IOnboardingService
{
    public async Task<Result<OnboardingStatusDto>> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (currentUser.SalonRole is not (Domain.Salons.SalonRole.Owner or Domain.Salons.SalonRole.Manager))
        {
            return Error.Forbidden();
        }

        var salon = await db.Salons.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (salon is null)
        {
            return Error.NotFound("salon.not_found");
        }

        var profile = salon.Street.Length > 0 && salon.PostalCode.Length > 0 && salon.City.Length > 0 && salon.Phone.Length > 0;
        var hours = await db.SalonOpeningHours.AnyAsync(cancellationToken);
        var services = await db.Services.AnyAsync(s => s.IsActive && s.IsBookableOnline, cancellationToken);
        // Someone customers can book: active, online, with working hours and at least one of the bookable services.
        var team = await db.StaffMembers.AnyAsync(
            member => member.IsActive && member.IsBookableOnline
                && db.StaffWorkingHours.Any(h => h.StaffId == member.Id)
                && db.StaffServices.Any(link => link.StaffId == member.Id
                    && db.Services.Any(s => s.Id == link.ServiceId && s.IsActive && s.IsBookableOnline)),
            cancellationToken);

        var steps = new List<OnboardingStepDto>
        {
            new(OnboardingSteps.Profile, profile),
            new(OnboardingSteps.Hours, hours),
            new(OnboardingSteps.Services, services),
            new(OnboardingSteps.Team, team),
        };
        return new OnboardingStatusDto(steps, steps.All(s => s.Done), AppLinks.Booking(appOptions.Value.PublicBaseUrl, salon.Slug));
    }
}
