using FluentValidation;
using Kalora.Application.Common;
using Kalora.Application.Identity;
using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Application.Salons;

internal sealed class SalonSettingsService(
    IAppDbContext db,
    ICurrentUser currentUser,
    ISlugAllocator slugs,
    IValidator<SalonProfile> profileValidator,
    IValidator<BookingRules> rulesValidator) : ISalonSettingsService
{
    private static readonly Error SalonNotFound = Error.NotFound("salon.not_found");

    public async Task<Result<SalonProfile>> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        var salon = await db.Salons.FirstOrDefaultAsync(cancellationToken);
        return salon is null ? SalonNotFound : ToProfile(salon);
    }

    public async Task<Result<SalonProfile>> UpdateProfileAsync(SalonProfile profile, CancellationToken cancellationToken = default)
    {
        var normalized = profile with
        {
            Name = profile.Name?.Trim() ?? string.Empty,
            Slug = profile.Slug?.Trim().ToLowerInvariant() ?? string.Empty,
            Street = profile.Street?.Trim() ?? string.Empty,
            PostalCode = profile.PostalCode?.Trim() ?? string.Empty,
            City = profile.City?.Trim() ?? string.Empty,
            Phone = profile.Phone?.Trim() ?? string.Empty,
            ImprintText = NullIfBlank(profile.ImprintText),
            PrivacyPolicyUrl = NullIfBlank(profile.PrivacyPolicyUrl),
        };

        var validation = profileValidator.Validate(normalized);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var salon = await db.Salons.FirstOrDefaultAsync(cancellationToken);
        if (salon is null)
        {
            return SalonNotFound;
        }

        if (normalized.Slug != salon.Slug)
        {
            if (currentUser.SalonRole != SalonRole.Owner)
            {
                return Error.Forbidden("settings.slug_owner_only");
            }

            if (!await slugs.IsAvailableAsync(normalized.Slug, salon.Id, cancellationToken))
            {
                return Error.Conflict("settings.slug_taken", "slug");
            }
        }

        salon.Name = normalized.Name;
        salon.Slug = normalized.Slug;
        salon.Industry = normalized.Industry;
        salon.Street = normalized.Street;
        salon.PostalCode = normalized.PostalCode;
        salon.City = normalized.City;
        salon.Phone = normalized.Phone;
        salon.TimeZone = normalized.TimeZone;
        salon.DefaultLanguage = normalized.DefaultLanguage;
        salon.ImprintText = normalized.ImprintText;
        salon.PrivacyPolicyUrl = normalized.PrivacyPolicyUrl;
        await db.SaveChangesAsync(cancellationToken);
        return ToProfile(salon);
    }

    public async Task<Result<BookingRules>> GetBookingRulesAsync(CancellationToken cancellationToken = default)
    {
        var salon = await db.Salons.FirstOrDefaultAsync(cancellationToken);
        return salon is null ? SalonNotFound : ToRules(salon);
    }

    public async Task<Result<BookingRules>> UpdateBookingRulesAsync(BookingRules rules, CancellationToken cancellationToken = default)
    {
        var validation = rulesValidator.Validate(rules);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var salon = await db.Salons.FirstOrDefaultAsync(cancellationToken);
        if (salon is null)
        {
            return SalonNotFound;
        }

        salon.SlotIntervalMinutes = rules.SlotIntervalMinutes;
        salon.MinLeadTimeMinutes = rules.MinLeadTimeMinutes;
        salon.BookingHorizonDays = rules.BookingHorizonDays;
        salon.CancellationDeadlineHours = rules.CancellationDeadlineHours;
        salon.AutoConfirm = rules.AutoConfirm;
        salon.RequestExpiryHours = rules.RequestExpiryHours;
        salon.RetentionMonths = rules.RetentionMonths;
        await db.SaveChangesAsync(cancellationToken);
        return ToRules(salon);
    }

    public async Task<Result<SlugSuggestion>> SuggestSlugAsync(string name, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(name)
            ? Error.Validation("validation.required", "name")
            : new SlugSuggestion(await slugs.AllocateAsync(name, cancellationToken));

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SalonProfile ToProfile(Salon salon) => new(
        salon.Name, salon.Slug, salon.Industry, salon.Street, salon.PostalCode, salon.City, salon.Phone,
        salon.TimeZone, salon.DefaultLanguage, salon.ImprintText, salon.PrivacyPolicyUrl);

    private static BookingRules ToRules(Salon salon) => new(
        salon.SlotIntervalMinutes, salon.MinLeadTimeMinutes, salon.BookingHorizonDays,
        salon.CancellationDeadlineHours, salon.AutoConfirm, salon.RequestExpiryHours, salon.RetentionMonths);
}
