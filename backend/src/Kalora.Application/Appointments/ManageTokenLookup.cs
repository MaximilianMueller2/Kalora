using Kalora.Application.Common;
using Kalora.Application.Security;
using Kalora.Domain.Appointments;
using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Application.Appointments;

/// <summary>An appointment found by the token of a customer's link, with its salon.</summary>
internal sealed record ManagedAppointment(Appointment Appointment, Salon Salon);

/// <summary>
/// Finds the appointment behind a customer's management link. The URL has no salon in it, so the hash of the token is looked
/// up across all salons (listed in the architecture test); the lookup is the credential check. An unknown token, a salon that
/// is switched off and a link older than <see cref="ValidAfterEnd"/> all look the same: <c>appointment.not_found</c>.
/// </summary>
internal sealed class ManageTokenLookup(IAppDbContext db, TimeProvider timeProvider)
{
    private const int MaxTokenLength = 200;

    /// <summary>How long after the end of the appointment the link still works.</summary>
    public static readonly TimeSpan ValidAfterEnd = TimeSpan.FromDays(30);

    public Task<Result<ManagedAppointment>> FindAsync(string token, CancellationToken cancellationToken) => FindAsync(token, track: false, cancellationToken);

    /// <param name="track">Load the appointment for changing it.</param>
    public async Task<Result<ManagedAppointment>> FindAsync(string token, bool track, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength)
        {
            return Error.NotFound("appointment.not_found");
        }

        var hash = TokenHasher.Hash(token);
        var appointments = db.AllTenants<Appointment>();
        var appointment = await (track ? appointments : appointments.AsNoTracking()).FirstOrDefaultAsync(a => a.ManageTokenHash == hash, cancellationToken);
        if (appointment is null || appointment.EndUtc + ValidAfterEnd < timeProvider.GetUtcNow())
        {
            return Error.NotFound("appointment.not_found");
        }

        var salon = await db.AllSalons().AsNoTracking().FirstOrDefaultAsync(
            s => s.Id == appointment.SalonId && s.Status != SalonStatus.Suspended && s.Status != SalonStatus.Cancelled, cancellationToken);
        return salon is null ? Error.NotFound("appointment.not_found") : new ManagedAppointment(appointment, salon);
    }
}
