using Microsoft.AspNetCore.Identity;

namespace Kalora.Application.Identity;

/// <summary>
/// A login account. Not salon-owned: which salon a user belongs to is expressed by <c>SalonMember</c>.
/// Lives in Application (not Domain) because ASP.NET Core Identity requires its base class.
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    public AppUser() => Id = Guid.CreateVersion7();

    public string DisplayName { get; set; } = string.Empty;

    public string PreferredLanguage { get; set; } = "de";

    public bool IsPlatformAdmin { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
