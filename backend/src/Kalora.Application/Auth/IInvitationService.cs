using Kalora.Application.Common;

namespace Kalora.Application.Auth;

public interface IInvitationService
{
    /// <summary>Owners and managers invite a staff member by e-mail; managers can only invite staff, not other managers.</summary>
    Task<Result<InvitationCreatedResponse>> CreateAsync(CreateInvitationRequest request, CancellationToken cancellationToken = default);

    Task<Result<InvitationPreviewResponse>> PreviewAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Creates the account behind an invitation, links it to the staff member and signs the user in.</summary>
    Task<Result<MeResponse>> AcceptAsync(string token, AcceptInvitationRequest request, CancellationToken cancellationToken = default);
}
