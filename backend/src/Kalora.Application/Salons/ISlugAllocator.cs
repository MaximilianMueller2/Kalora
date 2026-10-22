namespace Kalora.Application.Salons;

/// <summary>Slugs ("kalora.app/&lt;slug&gt;") are unique across all salons, so this looks beyond the current tenant.</summary>
public interface ISlugAllocator
{
    /// <summary>A free slug derived from a salon name (name, name-2, name-3, ...).</summary>
    Task<string> AllocateAsync(string salonName, CancellationToken cancellationToken = default);

    /// <summary>True if no other salon uses the slug and it is not reserved.</summary>
    Task<bool> IsAvailableAsync(string slug, Guid? ownSalonId, CancellationToken cancellationToken = default);
}
