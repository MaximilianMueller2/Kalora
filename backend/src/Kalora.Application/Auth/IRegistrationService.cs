using Kalora.Application.Common;

namespace Kalora.Application.Auth;

public interface IRegistrationService
{
    /// <summary>Creates the owner account, the salon (30-day trial) and the owner membership, and signs the owner in.</summary>
    Task<Result<MeResponse>> RegisterOwnerAsync(RegisterRequest request, CancellationToken cancellationToken = default);
}
