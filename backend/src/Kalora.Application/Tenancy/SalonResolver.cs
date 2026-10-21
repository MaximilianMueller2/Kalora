using Kalora.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Application.Tenancy;

internal sealed class SalonResolver(IAppDbContext db) : ISalonResolver
{
    public async Task<Result<Guid>> ResolveSlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        // The tenant is not known yet, so this lookup has to bypass the filter. It returns nothing but the ID.
        var salonId = await db.AllSalons()
            .Where(s => s.Slug == slug)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return salonId is { } id ? id : Error.NotFound("salon.not_found", "slug");
    }
}
