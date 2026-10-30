using FluentValidation;
using Kalora.Application.Auth;
using Kalora.Application.Availability;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Application.Identity;
using Kalora.Application.Security;
using Kalora.Domain.Appointments;
using Kalora.Domain.Customers;
using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalora.Application.PublicBooking;

internal sealed class BookingService(
    IAppDbContext db,
    IAvailabilityService availability,
    ISlotConflictDetector conflicts,
    IEmailOutbox outbox,
    IValidator<CreateAppointmentRequest> validator,
    IOptions<AppOptions> appOptions,
    IManageTokens manageTokens,
    TimeProvider timeProvider) : IBookingService
{
    public const int MaxOpenRequestsPerEmail = 3;

    /// <summary>Tries again when "anyone" lost a race for one person but another may still be free.</summary>
    private const int MaxAttempts = 3;

    public async Task<Result<BookingReceipt>> RequestAsync(CreateAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            return LookAlikeReceipt(request);
        }

        var salon = await db.Salons.Where(s => s.Status != SalonStatus.Suspended && s.Status != SalonStatus.Cancelled).FirstOrDefaultAsync(cancellationToken);
        if (salon is null)
        {
            return Error.NotFound("salon.not_found", "slug");
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await TryBookAsync(salon, request, cancellationToken);
            }
            catch (DbUpdateException exception) when (conflicts.Classify(exception) != SaveConflict.None)
            {
                db.ChangeTracker.Clear();
                var slotTaken = conflicts.Classify(exception) == SaveConflict.SlotTaken;
                // A wished person who is taken stays taken. For "anyone" a colleague may be free, a customer created
                // in the meantime is simply found on the next attempt, and after an aborted transaction the re-check
                // gives the clean answer.
                if (attempt >= MaxAttempts || (slotTaken && request.StaffId is not null))
                {
                    return Error.Conflict("booking.slot_unavailable");
                }
            }
        }
    }

    private async Task<Result<BookingReceipt>> TryBookAsync(Salon salon, CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var email = EmailAddresses.Normalize(request.Email);

        var openRequests = await (
            from existing in db.Appointments
            join known in db.Customers on existing.CustomerId equals known.Id
            where existing.Status == AppointmentStatus.Requested && known.Email == email
            select existing.Id).CountAsync(cancellationToken);
        if (openRequests >= MaxOpenRequestsPerEmail)
        {
            return Error.Unprocessable("booking.too_many_open_requests", "email");
        }

        var chosen = await availability.CheckSlotAsync(
            new SlotCheckQuery(request.ServiceId, request.StaffId, request.StartUtc, AvailabilityAudience.Customer), cancellationToken);
        if (!chosen.IsSuccess)
        {
            return chosen.Error;
        }

        var service = await db.Services.FirstAsync(s => s.Id == request.ServiceId, cancellationToken);
        var staff = await db.StaffMembers.FirstAsync(s => s.Id == chosen.Value.StaffId, cancellationToken);
        var language = string.IsNullOrEmpty(request.Language) ? salon.DefaultLanguage : request.Language;

        var customerEntity = await db.Customers.FirstOrDefaultAsync(c => c.Email == email, cancellationToken);
        if (customerEntity is null)
        {
            customerEntity = new Customer { Email = email };
            db.Customers.Add(customerEntity);
        }

        customerEntity.Name = request.Name.Trim();
        customerEntity.PreferredLanguage = language;
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            customerEntity.Phone = request.Phone.Trim();
        }

        var start = request.StartUtc.ToUniversalTime();
        var end = start.AddMinutes(chosen.Value.DurationMinutes);
        var autoConfirm = salon.AutoConfirm || staff.AutoConfirm;
        var appointment = new Appointment
        {
            StaffId = staff.Id,
            ServiceId = service.Id,
            CustomerId = customerEntity.Id,
            StartUtc = start,
            EndUtc = end,
            BlockedStartUtc = start.AddMinutes(-service.BufferBeforeMinutes),
            BlockedEndUtc = end.AddMinutes(service.BufferAfterMinutes),
            Status = autoConfirm ? AppointmentStatus.Confirmed : AppointmentStatus.Requested,
            Source = AppointmentSource.Online,
            Language = language,
            ServiceName = service.Name,
            PriceCents = service.PriceCents,
            PriceIsFrom = service.PriceIsFrom,
            Currency = service.Currency,
            DurationMinutes = chosen.Value.DurationMinutes,
            CustomerNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            ManageTokenHash = null,
        };
        // The management link is derived from the appointment's ID, so later e-mails can contain it again; only its hash is stored.
        var token = manageTokens.Create(appointment.Id);
        appointment.ManageTokenHash = TokenHasher.Hash(token);
        if (autoConfirm)
        {
            appointment.ConfirmedAtUtc = now;
        }
        else
        {
            // The salon has to answer before the appointment would begin, and within its own response time.
            var respondBy = now.AddHours(salon.RequestExpiryHours);
            appointment.ExpiresAtUtc = respondBy < start ? respondBy : start;
        }

        db.Appointments.Add(appointment);
        await QueueEmailsAsync(salon, appointment, staff.DisplayName, customerEntity, token, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new BookingReceipt(appointment.Status, start, end, service.Name, staff.DisplayName, token, appointment.ExpiresAtUtc);
    }

    private async Task QueueEmailsAsync(Salon salon, Appointment appointment, string staffName, Customer customer, string token, CancellationToken cancellationToken)
    {
        var baseUrl = appOptions.Value.PublicBaseUrl;
        var confirmed = appointment.Status == AppointmentStatus.Confirmed;
        var manageUrl = AppLinks.ManageAppointment(baseUrl, salon.Slug, token);

        outbox.Enqueue(salon.Id, customer.Email!, confirmed ? EmailTypes.BookingConfirmed : EmailTypes.BookingRequestReceived, appointment.Language, new
        {
            SalonName = salon.Name,
            SalonAddress = Address(salon),
            SalonPhone = salon.Phone,
            appointment.ServiceName,
            StaffName = staffName,
            appointment.StartUtc,
            appointment.EndUtc,
            salon.TimeZone,
            CustomerName = customer.Name,
            RespondByUtc = appointment.ExpiresAtUtc,
            ManageUrl = manageUrl,
            BookingUrl = AppLinks.Booking(baseUrl, salon.Slug),
        });

        // The owners and managers, and the person the appointment is with if they have a login.
        var staffUserId = await db.StaffMembers.Where(s => s.Id == appointment.StaffId).Select(s => s.UserId).FirstAsync(cancellationToken);
        var recipients = await (
            from member in db.SalonMembers
            join user in db.Users on member.UserId equals user.Id
            where member.Role == SalonRole.Owner || member.Role == SalonRole.Manager || member.UserId == staffUserId
            select user.Email).Distinct().ToListAsync(cancellationToken);
        foreach (var recipient in recipients.Where(email => !string.IsNullOrEmpty(email)))
        {
            outbox.Enqueue(salon.Id, recipient!, EmailTypes.BookingNewForSalon, salon.DefaultLanguage, new
            {
                SalonName = salon.Name,
                appointment.ServiceName,
                StaffName = staffName,
                appointment.StartUtc,
                appointment.EndUtc,
                salon.TimeZone,
                Status = appointment.Status.ToString(),
                CustomerName = customer.Name,
                CustomerEmail = customer.Email,
                CustomerPhone = customer.Phone,
                appointment.CustomerNote,
                RespondByUtc = appointment.ExpiresAtUtc,
                AppointmentId = appointment.Id,
                RequestUrl = AppLinks.Request(baseUrl, appointment.Id),
            });
        }
    }

    private static string Address(Salon salon)
    {
        var city = string.Join(' ', new[] { salon.PostalCode, salon.City }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.Join(", ", new[] { salon.Street, city }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static Result<BookingReceipt> LookAlikeReceipt(CreateAppointmentRequest request) =>
        new BookingReceipt(AppointmentStatus.Requested, request.StartUtc, request.StartUtc, string.Empty, string.Empty, TokenHasher.NewToken(), null);
}
