using Kalora.Api.Http;
using Kalora.Application.Catalog;
using Kalora.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kalora.Api.Controllers;

/// <summary>Everyone in the salon may read the catalog; owners and managers change it.</summary>
[Route("api/catalog")]
public sealed class CatalogController(IServiceCatalogService catalog) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthPolicies.SalonMember)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        ToActionResult(await catalog.GetCatalogAsync(cancellationToken));

    [HttpPost("categories")]
    [Authorize(Policy = AuthPolicies.SalonManager)]
    public async Task<IActionResult> CreateCategory(SaveCategoryRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await catalog.CreateCategoryAsync(request, cancellationToken));

    [HttpPut("categories/{id:guid}")]
    [Authorize(Policy = AuthPolicies.SalonManager)]
    public async Task<IActionResult> RenameCategory(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await catalog.RenameCategoryAsync(id, request, cancellationToken));

    [HttpDelete("categories/{id:guid}")]
    [Authorize(Policy = AuthPolicies.SalonManager)]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await catalog.DeleteCategoryAsync(id, cancellationToken));

    [HttpPut("categories/order")]
    [Authorize(Policy = AuthPolicies.SalonManager)]
    public async Task<IActionResult> ReorderCategories(ReorderRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await catalog.ReorderCategoriesAsync(request, cancellationToken));

    [HttpPut("categories/{categoryId:guid}/services/order")]
    [Authorize(Policy = AuthPolicies.SalonManager)]
    public async Task<IActionResult> ReorderServices(Guid categoryId, ReorderRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await catalog.ReorderServicesAsync(categoryId, request, cancellationToken));

    [HttpPost("services")]
    [Authorize(Policy = AuthPolicies.SalonManager)]
    public async Task<IActionResult> CreateService(SaveServiceRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await catalog.CreateServiceAsync(request, cancellationToken));

    [HttpPut("services/{id:guid}")]
    [Authorize(Policy = AuthPolicies.SalonManager)]
    public async Task<IActionResult> UpdateService(Guid id, SaveServiceRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await catalog.UpdateServiceAsync(id, request, cancellationToken));

    [HttpDelete("services/{id:guid}")]
    [Authorize(Policy = AuthPolicies.SalonManager)]
    public async Task<IActionResult> DeleteService(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await catalog.DeleteServiceAsync(id, cancellationToken));

    [HttpPost("templates/apply")]
    [Authorize(Policy = AuthPolicies.SalonManager)]
    public async Task<IActionResult> ApplyTemplate(ApplyTemplateRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await catalog.ApplyTemplateAsync(request, cancellationToken));
}
