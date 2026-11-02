using Kalora.Application.Common;
using Kalora.Application.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Application.Auth;

internal sealed class MeResponseFactory(IAppDbContext db, TimeProvider timeProvider)
{
    /// <summary>
    /// Builds the profile of any user, also right after login or registration when no tenant is set yet,
    /// hence the bypass; everything is looked up by the user's own ID.
    /// </summary>
    public async Task<MeResponse> BuildAsync(AppUser user, CancellationToken cancellationToken)
    {
        var membership = await db.AllTenants<Domain.Salons.SalonMember>()
            .FirstOrDefaultAsync(m => m.UserId == user.Id, cancellationToken);

        MeSalon? salon = null;
        Guid? staffId = null;
        if (membership is not null)
        {
            var found = await db.AllSalons().AsNoTracking().FirstAsync(s => s.Id == membership.SalonId, cancellationToken);
            salon = new MeSalon(found.Id, found.Name, found.Slug, found.Status, found.PlanCode, found.TrialEndsAtUtc, found.TimeZone, found.AccessAt(timeProvider.GetUtcNow()));
            staffId = await db.AllTenants<Domain.Staff.StaffMember>()
                .Where(s => s.UserId == user.Id)
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new MeResponse(
            user.Id, user.Email ?? string.Empty, user.DisplayName, user.EmailConfirmed, user.PreferredLanguage,
            user.TwoFactorEnabled, user.IsPlatformAdmin, salon, membership?.Role, staffId);
    }
}
