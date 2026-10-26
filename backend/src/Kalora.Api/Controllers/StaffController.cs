using Kalora.Api.Http;
using Kalora.Application.Identity;
using Kalora.Application.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kalora.Api.Controllers;

[Route("api/staff")]
public sealed class StaffController(IStaffService staff) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthPolicies.SalonMember)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        ToActionResult(await staff.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthPolicies.SalonMember)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await staff.GetAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = AuthPolicies.SalonManager)]
    public async Task<IActionResult> Create(CreateStaffRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await staff.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthPolicies.SalonManager)]
    public async Task<IActionResult> Update(Guid id, SaveStaffRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await staff.UpdateAsync(id, request, cancellationToken));

    [HttpGet("{id:guid}/services")]
    [Authorize(Policy = AuthPolicies.SalonMember)]
    public async Task<IActionResult> GetSkills(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await staff.GetSkillsAsync(id, cancellationToken));

    [HttpPut("{id:guid}/services")]
    [Authorize(Policy = AuthPolicies.SalonManager)]
    public async Task<IActionResult> ReplaceSkills(Guid id, StaffSkills skills, CancellationToken cancellationToken) =>
        ToActionResult(await staff.ReplaceSkillsAsync(id, skills, cancellationToken));
}
