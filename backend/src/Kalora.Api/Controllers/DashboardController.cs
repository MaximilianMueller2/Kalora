using Kalora.Api.Http;
using Kalora.Application.Dashboard;
using Kalora.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kalora.Api.Controllers;

/// <summary>The start page of the salon area and the hand-outs of the booking link.</summary>
[Route("api/dashboard")]
public sealed class DashboardController(IDashboardService dashboard, IBookingLinkService bookingLink) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthPolicies.SalonMember)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        ToActionResult(await dashboard.GetAsync(cancellationToken));

    [HttpGet("booking-link/qr.png")]
    [Authorize(Policy = AuthPolicies.SalonManager)]
    public async Task<IActionResult> QrPng(CancellationToken cancellationToken) =>
        await FileResult(await bookingLink.QrPngAsync(cancellationToken));

    [HttpGet("booking-link/poster.pdf")]
    [Authorize(Policy = AuthPolicies.SalonManager)]
    public async Task<IActionResult> PosterPdf(CancellationToken cancellationToken) =>
        await FileResult(await bookingLink.PosterPdfAsync(cancellationToken));

    private Task<IActionResult> FileResult(Kalora.Application.Common.Result<BinaryFile> result) =>
        Task.FromResult<IActionResult>(result.IsSuccess ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName) : ToActionResult(result));
}
