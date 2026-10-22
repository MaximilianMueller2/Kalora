using Kalora.Application.Common;
using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Application.Salons;

internal sealed class SlugAllocator(IAppDbContext db) : ISlugAllocator
{
    public async Task<string> AllocateAsync(string salonName, CancellationToken cancellationToken = default)
    {
        foreach (var candidate in SlugGenerator.Candidates(SlugGenerator.Slugify(salonName)))
        {
            if (await IsAvailableAsync(candidate, null, cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unreachable: slug candidates are unbounded.");
    }

    public async Task<bool> IsAvailableAsync(string slug, Guid? ownSalonId, CancellationToken cancellationToken = default) =>
        !SlugGenerator.IsReserved(slug)
        && !await db.AllSalons().AnyAsync(s => s.Slug == slug && s.Id != ownSalonId, cancellationToken);
}
