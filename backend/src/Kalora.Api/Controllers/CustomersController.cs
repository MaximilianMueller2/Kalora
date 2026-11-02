using Kalora.Api.Http;
using Kalora.Application.Common;
using Kalora.Application.Customers;
using Kalora.Application.Dashboard;
using Kalora.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kalora.Api.Controllers;

/// <summary>The salon's customer file with export and deletion. Owners and managers only.</summary>
[Route("api/customers")]
[Authorize(Policy = AuthPolicies.SalonManager)]
public sealed class CustomersController(ICustomerService customers) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, CancellationToken cancellationToken) =>
        ToActionResult(await customers.ListAsync(q, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await customers.GetAsync(id, cancellationToken));

    [HttpGet("{id:guid}/export.json")]
    public async Task<IActionResult> ExportJson(Guid id, CancellationToken cancellationToken) =>
        Download(await customers.ExportJsonAsync(id, cancellationToken));

    [HttpGet("{id:guid}/export.csv")]
    public async Task<IActionResult> ExportCsv(Guid id, CancellationToken cancellationToken) =>
        Download(await customers.ExportCsvAsync(id, cancellationToken));

    /// <summary>"Delete customer": anonymizes. A POST rather than a DELETE, because the appointments stay.</summary>
    [HttpPost("{id:guid}/anonymize")]
    public async Task<IActionResult> Anonymize(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await customers.AnonymizeAsync(id, cancellationToken));

    private IActionResult Download(Result<BinaryFile> result)
    {
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        // Personal data: no cache may keep it.
        Response.Headers.CacheControl = "no-store";
        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }
}
