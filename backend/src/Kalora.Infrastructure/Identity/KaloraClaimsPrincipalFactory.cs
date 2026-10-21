using System.Security.Claims;
using Kalora.Application.Common;
using Kalora.Application.Identity;
using Kalora.Application.Tenancy;
using Kalora.Domain.Salons;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalora.Infrastructure.Identity;

/// <summary>Adds the user's salon and salon role to the login cookie. Runs before a tenant exists, hence the bypass.</summary>
public sealed class KaloraClaimsPrincipalFactory(
    UserManager<AppUser> userManager, IOptions<IdentityOptions> options, IAppDbContext db)
    : UserClaimsPrincipalFactory<AppUser>(userManager, options)
{
    public const string PlatformAdminRole = "PlatformAdmin";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        var membership = await db.AllTenants<SalonMember>()
            .Where(m => m.UserId == user.Id)
            .Select(m => new { m.SalonId, m.Role })
            .FirstOrDefaultAsync();
        if (membership is not null)
        {
            identity.AddClaim(new Claim(TenantClaims.SalonId, membership.SalonId.ToString()));
            identity.AddClaim(new Claim(ClaimTypes.Role, membership.Role.ToString()));
        }

        if (user.IsPlatformAdmin)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, PlatformAdminRole));
        }

        return identity;
    }
}
