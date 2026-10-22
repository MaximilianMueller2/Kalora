using Kalora.Application.Common;

namespace Kalora.Application.Catalog;

/// <summary>Categories and services of the signed-in user's salon. Every change returns the updated catalog.</summary>
public interface IServiceCatalogService
{
    Task<Result<CatalogDto>> GetCatalogAsync(CancellationToken cancellationToken = default);

    Task<Result<CatalogDto>> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default);

    Task<Result<CatalogDto>> RenameCategoryAsync(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>Only an empty category can be removed.</summary>
    Task<Result<CatalogDto>> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<CatalogDto>> ReorderCategoriesAsync(ReorderRequest request, CancellationToken cancellationToken = default);

    Task<Result<CatalogDto>> CreateServiceAsync(SaveServiceRequest request, CancellationToken cancellationToken = default);

    Task<Result<CatalogDto>> UpdateServiceAsync(Guid id, SaveServiceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// A service with appointments cannot be deleted (past appointments keep pointing at it); it can be deactivated
    /// instead (<c>IsActive = false</c>).
    /// </summary>
    Task<Result<CatalogDto>> DeleteServiceAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<CatalogDto>> ReorderServicesAsync(Guid categoryId, ReorderRequest request, CancellationToken cancellationToken = default);

    /// <summary>Adds the typical categories and services of an industry, named in the salon's default language.</summary>
    Task<Result<CatalogDto>> ApplyTemplateAsync(ApplyTemplateRequest request, CancellationToken cancellationToken = default);
}
