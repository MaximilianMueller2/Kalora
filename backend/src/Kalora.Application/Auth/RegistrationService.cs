using FluentValidation;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Application.Identity;
using Kalora.Application.Salons;
using Kalora.Domain.Salons;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Kalora.Application.Auth;

internal sealed class RegistrationService(
    UserManager<AppUser> users,
    IAppDbContext db,
    ISessionService session,
    IEmailOutbox outbox,
    ISlugAllocator slugs,
    MeResponseFactory meFactory,
    IValidator<RegisterRequest> validator,
    IOptions<AppOptions> appOptions,
    TimeProvider timeProvider) : IRegistrationService
{
    private const int TrialDays = 30;

    public async Task<Result<MeResponse>> RegisterOwnerAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var email = EmailAddresses.Normalize(request.Email);
        if (await users.FindByEmailAsync(email) is not null)
        {
            return Error.Conflict("auth.email_taken", "email");
        }

        var now = timeProvider.GetUtcNow();
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = request.Name.Trim(),
            PreferredLanguage = request.Language,
            CreatedAtUtc = now,
        };

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return created.ToError();
        }

        var salon = new Salon
        {
            Name = request.SalonName.Trim(),
            Slug = await slugs.AllocateAsync(request.SalonName, cancellationToken),
            Industry = request.Industry,
            DefaultLanguage = request.Language,
            Status = SalonStatus.Trial,
            TrialEndsAtUtc = now.AddDays(TrialDays),
        };
        db.Salons.Add(salon);
        db.SalonMembers.Add(new SalonMember { SalonId = salon.Id, UserId = user.Id, Role = SalonRole.Owner });

        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        outbox.Enqueue(salon.Id, email, EmailTypes.ConfirmEmail, request.Language, new
        {
            user.DisplayName,
            SalonName = salon.Name,
            ConfirmUrl = AppLinks.ConfirmEmail(appOptions.Value.PublicBaseUrl, user.Id, token),
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await session.SignInAsync(user, persistent: false);
        return await meFactory.BuildAsync(user, cancellationToken);
    }
}
