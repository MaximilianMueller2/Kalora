using Kalora.Domain.Salons;

namespace Kalora.Application.Identity;

/// <summary>The signed-in user of the current request, taken from the authentication cookie's claims.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    SalonRole? SalonRole { get; }

    bool IsPlatformAdmin { get; }
}
