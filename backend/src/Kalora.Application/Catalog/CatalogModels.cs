using Kalora.Domain.Salons;

namespace Kalora.Application.Catalog;

public sealed record ServiceDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    string? Description,
    int PriceCents,
    bool PriceIsFrom,
    string Currency,
    int DurationMinutes,
    int BufferBeforeMinutes,
    int BufferAfterMinutes,
    bool IsBookableOnline,
    bool IsActive,
    int SortOrder,
    bool HasAppointments);

public sealed record CategoryDto(Guid Id, string Name, int SortOrder, IReadOnlyList<ServiceDto> Services);

/// <summary>Everything the salon offers, in display order. Mutations answer with the new catalog.</summary>
public sealed record CatalogDto(IReadOnlyList<CategoryDto> Categories);

public sealed record SaveCategoryRequest(string Name);

public sealed record SaveServiceRequest(
    Guid CategoryId,
    string Name,
    string? Description,
    int PriceCents,
    bool PriceIsFrom,
    int DurationMinutes,
    int BufferBeforeMinutes,
    int BufferAfterMinutes,
    bool IsBookableOnline,
    bool IsActive);

/// <summary>The complete new order of the IDs of one list (all categories, or all services of one category).</summary>
public sealed record ReorderRequest(IReadOnlyList<Guid> Ids);

/// <summary>Which template to take over; the salon's own industry if omitted.</summary>
public sealed record ApplyTemplateRequest(SalonIndustry? Industry);
