using System.Security.Claims;
using Kalora.Application.Identity;
using Kalora.Domain.Salons;

namespace Kalora.Api.Auth;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public Guid? UserId => Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public SalonRole? SalonRole => User?.Claims
        .Where(claim => claim.Type == ClaimTypes.Role)
        .Select(claim => Enum.TryParse<SalonRole>(claim.Value, out var role) ? role : (SalonRole?)null)
        .FirstOrDefault(role => role is not null);

    public bool IsPlatformAdmin => User?.IsInRole(Infrastructure.Identity.KaloraClaimsPrincipalFactory.PlatformAdminRole) ?? false;
}
