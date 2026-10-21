using Kalora.Application.Common;
using Kalora.Domain.Staff;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kalora.IntegrationTests;

public sealed record RenameRequest(string DisplayName);

/// <summary>Deliberately attempts to take the SalonId from the body, like a careless service might.</summary>
public sealed record CreateStaffRequest(string DisplayName, Guid? SalonId);

/// <summary>Stand-in for real CRUD endpoints (they do not exist yet): same access pattern, IDs taken from the URL.</summary>
[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/test/staff")]
public sealed class TestStaffController(IAppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await db.StaffMembers.OrderBy(s => s.DisplayName).Select(s => s.DisplayName).ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var staff = await db.StaffMembers.FirstOrDefaultAsync(s => s.Id == id);
        return staff is null ? NotFound() : Ok(staff.DisplayName);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, RenameRequest request)
    {
        var staff = await db.StaffMembers.FirstOrDefaultAsync(s => s.Id == id);
        if (staff is null)
        {
            return NotFound();
        }

        staff.DisplayName = request.DisplayName;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var staff = await db.StaffMembers.FirstOrDefaultAsync(s => s.Id == id);
        if (staff is null)
        {
            return NotFound();
        }

        db.StaffMembers.Remove(staff);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateStaffRequest request)
    {
        var staff = new StaffMember { DisplayName = request.DisplayName, SalonId = request.SalonId ?? Guid.Empty };
        db.StaffMembers.Add(staff);
        await db.SaveChangesAsync();
        return Ok(staff.Id);
    }
}

[ApiController]
[Route("api/public/salons/{slug}/test-staff")]
public sealed class TestPublicStaffController(IAppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(string slug) =>
        Ok(await db.StaffMembers.OrderBy(s => s.DisplayName).Select(s => s.DisplayName).ToListAsync());
}
