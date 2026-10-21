using FluentValidation;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Application.Identity;
using Kalora.Application.Security;
using Kalora.Domain.Salons;
using Kalora.Domain.Staff;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalora.Application.Auth;

internal sealed class InvitationService(
    UserManager<AppUser> users,
    IAppDbContext db,
    ISessionService session,
    ICurrentUser currentUser,
    IEmailOutbox outbox,
    MeResponseFactory meFactory,
    IValidator<CreateInvitationRequest> createValidator,
    IValidator<AcceptInvitationRequest> acceptValidator,
    IOptions<AppOptions> appOptions,
    TimeProvider timeProvider) : IInvitationService
{
    private static readonly TimeSpan Validity = TimeSpan.FromDays(7);

    public async Task<Result<InvitationCreatedResponse>> CreateAsync(CreateInvitationRequest request, CancellationToken cancellationToken = default)
    {
        if (currentUser.SalonRole is not (SalonRole.Owner or SalonRole.Manager))
        {
            return Error.Forbidden();
        }

        var validation = createValidator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        if (request.Role == SalonRole.Manager && currentUser.SalonRole != SalonRole.Owner)
        {
            return Error.Forbidden("invitation.manager_requires_owner");
        }

        var staff = await db.StaffMembers.FirstOrDefaultAsync(s => s.Id == request.StaffId, cancellationToken);
        if (staff is null)
        {
            return Error.NotFound("staff.not_found", "staffId");
        }

        if (staff.UserId is not null)
        {
            return Error.Conflict("invitation.staff_already_linked", "staffId");
        }

        var salon = await db.Salons.FirstAsync(cancellationToken);
        var email = EmailAddresses.Normalize(request.Email);
        var now = timeProvider.GetUtcNow();
        var token = TokenHasher.NewToken();

        // A new invitation replaces any pending one for the same staff member.
        var pending = await db.StaffInvitations
            .Where(i => i.StaffId == staff.Id && i.AcceptedAtUtc == null)
            .ToListAsync(cancellationToken);
        db.StaffInvitations.RemoveRange(pending);

        var invitation = new StaffInvitation
        {
            StaffId = staff.Id,
            Email = email,
            Role = request.Role,
            TokenHash = TokenHasher.Hash(token),
            ExpiresAtUtc = now.Add(Validity),
            InvitedByUserId = currentUser.UserId,
        };
        db.StaffInvitations.Add(invitation);

        outbox.Enqueue(salon.Id, email, EmailTypes.StaffInvitation, salon.DefaultLanguage, new
        {
            SalonName = salon.Name,
            StaffName = staff.DisplayName,
            InvitationUrl = AppLinks.Invitation(appOptions.Value.PublicBaseUrl, token),
            invitation.ExpiresAtUtc,
        });

        await db.SaveChangesAsync(cancellationToken);
        return new InvitationCreatedResponse(invitation.ExpiresAtUtc);
    }

    public async Task<Result<InvitationPreviewResponse>> PreviewAsync(string token, CancellationToken cancellationToken = default)
    {
        var found = await FindOpenInvitationAsync(token, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var (invitation, staff, salon) = found.Value;
        return new InvitationPreviewResponse(salon.Name, staff.DisplayName, invitation.Email, invitation.Role, invitation.ExpiresAtUtc);
    }

    public async Task<Result<MeResponse>> AcceptAsync(string token, AcceptInvitationRequest request, CancellationToken cancellationToken = default)
    {
        var validation = acceptValidator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var found = await FindOpenInvitationAsync(token, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var (invitation, staff, salon) = found.Value;
        if (staff.UserId is not null)
        {
            return Error.Conflict("invitation.staff_already_linked");
        }

        if (await users.FindByEmailAsync(invitation.Email) is not null)
        {
            return Error.Conflict("auth.email_taken", "email");
        }

        var now = timeProvider.GetUtcNow();
        // The invitation went to this address, so opening the link proves it: the e-mail counts as confirmed.
        var user = new AppUser
        {
            UserName = invitation.Email,
            Email = invitation.Email,
            EmailConfirmed = true,
            DisplayName = staff.DisplayName,
            PreferredLanguage = request.Language,
            CreatedAtUtc = now,
        };

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return created.ToError();
        }

        db.SalonMembers.Add(new SalonMember { SalonId = salon.Id, UserId = user.Id, Role = invitation.Role });
        staff.UserId = user.Id;
        invitation.AcceptedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await session.SignInAsync(user, persistent: false);
        return await meFactory.BuildAsync(user, cancellationToken);
    }

    /// <summary>
    /// The visitor has no session, so the salon is not known: the token itself identifies the invitation
    /// (bypass). Unknown, used and expired tokens are reported with distinct codes the frontend can explain.
    /// </summary>
    private async Task<Result<(StaffInvitation Invitation, StaffMember Staff, Salon Salon)>> FindOpenInvitationAsync(
        string token, CancellationToken cancellationToken)
    {
        var hash = TokenHasher.Hash(token);
        var invitation = await db.AllTenants<StaffInvitation>().FirstOrDefaultAsync(i => i.TokenHash == hash, cancellationToken);
        if (invitation is null || invitation.AcceptedAtUtc is not null)
        {
            return Error.NotFound("invitation.invalid");
        }

        if (invitation.ExpiresAtUtc <= timeProvider.GetUtcNow())
        {
            return Error.NotFound("invitation.expired");
        }

        var staff = await db.AllTenants<StaffMember>().FirstAsync(s => s.Id == invitation.StaffId, cancellationToken);
        var salon = await db.AllSalons().FirstAsync(s => s.Id == invitation.SalonId, cancellationToken);
        return (invitation, staff, salon);
    }
}
