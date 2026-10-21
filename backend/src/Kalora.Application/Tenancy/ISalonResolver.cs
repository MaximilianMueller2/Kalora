using Kalora.Application.Common;

namespace Kalora.Application.Tenancy;

public interface ISalonResolver
{
    /// <summary>Finds the salon behind a public URL slug (error code <c>salon.not_found</c> if unknown).</summary>
    Task<Result<Guid>> ResolveSlugAsync(string slug, CancellationToken cancellationToken = default);
}
