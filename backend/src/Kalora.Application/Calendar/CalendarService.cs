using System.Text.Json;
using FluentValidation;
using Kalora.Application.Appointments;
using Kalora.Application.Availability;
using Kalora.Application.Common;
using Kalora.Application.Identity;
using Kalora.Application.Security;
using Kalora.Domain.Appointments;
using Kalora.Domain.Auditing;
using Kalora.Domain.Customers;
using Kalora.Domain.Scheduling;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Application.Calendar;

internal sealed class CalendarService(
    IAppDbContext db,
    AppointmentScope scope,
    IAvailabilityService availability,
    ISlotConflictDetector conflicts,
    AppointmentMails mails,
    IManageTokens manageTokens,
    ICurrentUser currentUser,
    IValidator<CreateManualAppointmentRequest> manualValidator,
    IValidator<MoveAppointmentRequest> moveValidator,
    IValidator<CreateBlockRequest> blockValidator,
    TimeProvider timeProvider) : ICalendarService
{
    public const int MaxDays = 14;
    private const int MaxSearchHits = 8;
    private const int MinSearchLength = 2;

    /// <summary>What a column shows: open and confirmed appointments and the ones that took place.</summary>
    private static readonly AppointmentStatus[] Shown =
        [AppointmentStatus.Requested, AppointmentStatus.Confirmed, AppointmentStatus.Completed, AppointmentStatus.NoShow];

    public async Task<Result<CalendarDto>> GetAsync(CalendarQuery query, CancellationToken cancellationToken = default)
    {
        if (query.To < query.From || query.To.DayNumber - query.From.DayNumber >= MaxDays)
        {
            return Error.Validation("calendar.range_invalid", "to");
        }

        var salon = await db.Salons.FirstOrDefaultAsync(cancellationToken);
        if (salon is null)
        {
            return Error.NotFound("salon.not_found");
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(salon.TimeZone);
        var from = SalonTime.StartOfDayUtc(query.From, zone);
        var to = SalonTime.StartOfDayUtc(query.To.AddDays(1), zone);

        var people = scope.StaffMembers().Where(s => s.IsActive);
        if (query.StaffIds is { Count: > 0 } wanted)
        {
            people = people.Where(s => wanted.Contains(s.Id));
        }

        var staff = await people.OrderBy(s => s.SortOrder).ThenBy(s => s.DisplayName).ToListAsync(cancellationToken);
        var ids = staff.Select(s => s.Id).ToList();

        var opening = (await db.SalonOpeningHours.ToListAsync(cancellationToken)).Select(h => new WeekBlock(h.Weekday, h.Start, h.End)).ToList();
        var closed = (await db.SalonClosures.Where(c => c.Date >= query.From && c.Date <= query.To).Select(c => c.Date).ToListAsync(cancellationToken)).ToHashSet();
        var working = await db.StaffWorkingHours
            .Where(h => ids.Contains(h.StaffId) && h.ValidFrom <= query.To && (h.ValidTo == null || h.ValidTo >= query.From))
            .ToListAsync(cancellationToken);
        var absences = await db.StaffAbsences.Where(a => ids.Contains(a.StaffId) && a.StartUtc < to && a.EndUtc > from).ToListAsync(cancellationToken);
        var blocks = await db.TimeBlocks.Where(b => ids.Contains(b.StaffId) && b.StartUtc < to && b.EndUtc > from).ToListAsync(cancellationToken);
        var appointments = (await AppointmentProjection.Project(db, scope.Appointments()
                .Where(a => ids.Contains(a.StaffId) && Shown.Contains(a.Status) && a.BlockedStartUtc < to && a.BlockedEndUtc > from))
            .ToListAsync(cancellationToken)).ConvertAll(AppointmentProjection.WithNextStates);

        var columns = staff.Select(person => new CalendarColumnDto(
            person.Id,
            person.DisplayName,
            person.Color,
            WorkingTime.Spans(
                working.Where(h => h.StaffId == person.Id).Select(h => new WorkingBlock(h.Weekday, h.Start, h.End, h.ValidFrom, h.ValidTo)).ToList(),
                opening, closed, query.From, query.To, zone).Select(i => new CalendarSpanDto(i.Start, i.End)).ToList(),
            absences.Where(a => a.StaffId == person.Id).OrderBy(a => a.StartUtc).Select(a => new CalendarAbsenceDto(a.Id, a.StartUtc, a.EndUtc, a.Type)).ToList(),
            blocks.Where(b => b.StaffId == person.Id).OrderBy(b => b.StartUtc).Select(b => new CalendarBlockDto(b.Id, b.StartUtc, b.EndUtc, b.Label)).ToList(),
            appointments.Where(a => a.Staff.Id == person.Id).OrderBy(a => a.StartUtc).ToList())).ToList();

        return new CalendarDto(query.From, query.To, salon.TimeZone, salon.SlotIntervalMinutes, columns);
    }

    public async Task<Result<IReadOnlyList<CustomerHitDto>>> SearchCustomersAsync(string text, CancellationToken cancellationToken = default)
    {
        var term = text?.Trim() ?? string.Empty;
        if (term.Length < MinSearchLength)
        {
            return new List<CustomerHitDto>();
        }

        var lower = term.ToLowerInvariant();
        return await db.Customers
            .Where(c => c.AnonymizedAtUtc == null
                && (c.Name.ToLower().Contains(lower)
                    || (c.Email != null && c.Email.Contains(lower))
                    || (c.Phone != null && c.Phone.Contains(term))))
            .OrderByDescending(c => c.LastVisitAtUtc)
            .ThenBy(c => c.Name)
            .Take(MaxSearchHits)
            .Select(c => new CustomerHitDto(c.Id, c.Name, c.Email, c.Phone))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<AppointmentDto>> CreateAppointmentAsync(CreateManualAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        var validation = manualValidator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var staff = await scope.StaffMembers().FirstOrDefaultAsync(s => s.Id == request.StaffId && s.IsActive, cancellationToken);
        if (staff is null)
        {
            return Error.NotFound("staff.not_found", "staffId");
        }

        var salon = await db.Salons.FirstAsync(cancellationToken);
        var chosen = await availability.CheckSlotAsync(
            new SlotCheckQuery(request.ServiceId, staff.Id, request.StartUtc, AvailabilityAudience.Internal), cancellationToken);
        if (!chosen.IsSuccess)
        {
            return chosen.Error;
        }

        var service = await db.Services.FirstAsync(s => s.Id == request.ServiceId, cancellationToken);
        var customer = await FindOrCreateCustomerAsync(request, salon.DefaultLanguage, cancellationToken);
        if (!customer.IsSuccess)
        {
            return customer.Error;
        }

        var now = timeProvider.GetUtcNow();
        var start = request.StartUtc.ToUniversalTime();
        var end = start.AddMinutes(chosen.Value.DurationMinutes);
        var appointment = new Appointment
        {
            StaffId = staff.Id,
            ServiceId = service.Id,
            CustomerId = customer.Value.Id,
            StartUtc = start,
            EndUtc = end,
            BlockedStartUtc = start.AddMinutes(-service.BufferBeforeMinutes),
            BlockedEndUtc = end.AddMinutes(service.BufferAfterMinutes),
            Status = AppointmentStatus.Confirmed,
            Source = AppointmentSource.Manual,
            Language = customer.Value.PreferredLanguage,
            ServiceName = service.Name,
            PriceCents = service.PriceCents,
            PriceIsFrom = service.PriceIsFrom,
            Currency = service.Currency,
            DurationMinutes = chosen.Value.DurationMinutes,
            InternalNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            ConfirmedAtUtc = now,
        };
        appointment.ManageTokenHash = TokenHasher.Hash(manageTokens.Create(appointment.Id));
        db.Appointments.Add(appointment);
        AddAudit("appointment.created_manually", appointment.Id, new { to = nameof(AppointmentStatus.Confirmed), staff = staff.Id });
        if (start > now)
        {
            mails.QueueForCustomer(EmailTypes.BookingConfirmed, salon, appointment, staff.DisplayName, customer.Value);
        }

        var saved = await SaveAsync(cancellationToken);
        return saved.IsSuccess ? await LoadAsync(appointment.Id, cancellationToken) : saved.Error;
    }

    public async Task<Result<AppointmentDto>> MoveAppointmentAsync(Guid id, MoveAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        var validation = moveValidator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var appointment = await scope.Appointments().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (appointment is null)
        {
            return Error.NotFound("appointment.not_found");
        }

        var now = timeProvider.GetUtcNow();
        if (appointment.Status != AppointmentStatus.Confirmed || appointment.EndUtc <= now)
        {
            return Error.Conflict("appointment.not_movable");
        }

        var targetStaff = await scope.StaffMembers().FirstOrDefaultAsync(s => s.Id == (request.StaffId ?? appointment.StaffId) && s.IsActive, cancellationToken);
        if (targetStaff is null)
        {
            return Error.NotFound("staff.not_found", "staffId");
        }

        var start = request.StartUtc.ToUniversalTime();
        if (start == appointment.StartUtc && targetStaff.Id == appointment.StaffId)
        {
            return await LoadAsync(id, cancellationToken);
        }

        var chosen = await availability.CheckSlotAsync(
            new SlotCheckQuery(appointment.ServiceId, targetStaff.Id, start, AvailabilityAudience.Internal, IgnoreAppointmentId: appointment.Id), cancellationToken);
        if (!chosen.IsSuccess)
        {
            return chosen.Error;
        }

        var service = await db.Services.FirstAsync(s => s.Id == appointment.ServiceId, cancellationToken);
        var before = new { start = appointment.StartUtc, staff = appointment.StaffId };
        appointment.StaffId = targetStaff.Id;
        appointment.StartUtc = start;
        appointment.EndUtc = start.AddMinutes(chosen.Value.DurationMinutes);
        appointment.DurationMinutes = chosen.Value.DurationMinutes;
        appointment.BlockedStartUtc = start.AddMinutes(-service.BufferBeforeMinutes);
        appointment.BlockedEndUtc = appointment.EndUtc.AddMinutes(service.BufferAfterMinutes);
        appointment.CalendarSequence++;
        appointment.ReminderSentAtUtc = null;

        AddAudit("appointment.moved", appointment.Id, new { from = before, to = new { start, staff = targetStaff.Id }, hasReason = !string.IsNullOrWhiteSpace(request.Reason) });
        var salon = await db.Salons.FirstAsync(cancellationToken);
        var customer = await db.Customers.FirstAsync(c => c.Id == appointment.CustomerId, cancellationToken);
        mails.QueueForCustomer(EmailTypes.BookingRescheduled, salon, appointment, targetStaff.DisplayName, customer, request.Reason);

        var saved = await SaveAsync(cancellationToken);
        return saved.IsSuccess ? await LoadAsync(id, cancellationToken) : saved.Error;
    }

    public async Task<Result<CalendarBlockDto>> CreateBlockAsync(CreateBlockRequest request, CancellationToken cancellationToken = default)
    {
        var validation = blockValidator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        if (!await scope.StaffMembers().AnyAsync(s => s.Id == request.StaffId && s.IsActive, cancellationToken))
        {
            return Error.NotFound("staff.not_found", "staffId");
        }

        var start = request.StartUtc.ToUniversalTime();
        var end = request.EndUtc.ToUniversalTime();
        var taken = await db.Appointments.AnyAsync(
            a => a.StaffId == request.StaffId && (a.Status == AppointmentStatus.Requested || a.Status == AppointmentStatus.Confirmed)
                && a.BlockedStartUtc < end && a.BlockedEndUtc > start, cancellationToken);
        if (taken)
        {
            return Error.Conflict("calendar.block_overlaps_appointment");
        }

        var block = new TimeBlock { StaffId = request.StaffId, StartUtc = start, EndUtc = end, Label = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim() };
        db.TimeBlocks.Add(block);
        await db.SaveChangesAsync(cancellationToken);
        return new CalendarBlockDto(block.Id, block.StartUtc, block.EndUtc, block.Label);
    }

    public async Task<Result> DeleteBlockAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var block = await db.TimeBlocks.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (block is null || !await scope.StaffMembers().AnyAsync(s => s.Id == block.StaffId, cancellationToken))
        {
            return Error.NotFound("calendar.block_not_found");
        }

        db.TimeBlocks.Remove(block);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<Customer>> FindOrCreateCustomerAsync(CreateManualAppointmentRequest request, string defaultLanguage, CancellationToken cancellationToken)
    {
        if (request.CustomerId is { } customerId)
        {
            var known = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.AnonymizedAtUtc == null, cancellationToken);
            return known is null ? Error.NotFound("customer.not_found", "customerId") : known;
        }

        var email = string.IsNullOrWhiteSpace(request.CustomerEmail) ? null : EmailAddresses.Normalize(request.CustomerEmail);
        var customer = email is null ? null : await db.Customers.FirstOrDefaultAsync(c => c.Email == email, cancellationToken);
        if (customer is null)
        {
            customer = new Customer { Name = request.CustomerName!.Trim(), Email = email, PreferredLanguage = defaultLanguage };
            db.Customers.Add(customer);
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerPhone))
        {
            customer.Phone = request.CustomerPhone.Trim();
        }

        return customer;
    }

    private void AddAudit(string action, Guid appointmentId, object details) =>
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = currentUser.UserId,
            Action = action,
            EntityType = nameof(Appointment),
            EntityId = appointmentId,
            Details = JsonSerializer.Serialize(details),
        });

    private async Task<Result> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return Error.Conflict("appointment.invalid_transition");
        }
        catch (DbUpdateException exception) when (conflicts.Classify(exception) is SaveConflict.SlotTaken or SaveConflict.Transient)
        {
            // Somebody else took the time in the same moment.
            db.ChangeTracker.Clear();
            return Error.Conflict("booking.slot_unavailable");
        }
        catch (DbUpdateException exception) when (conflicts.Classify(exception) == SaveConflict.DuplicateKey)
        {
            db.ChangeTracker.Clear();
            return Error.Conflict("customer.duplicate");
        }
    }

    private async Task<Result<AppointmentDto>> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var dto = await AppointmentProjection.Project(db, scope.Appointments().Where(a => a.Id == id)).FirstOrDefaultAsync(cancellationToken);
        return dto is null ? Error.NotFound("appointment.not_found") : AppointmentProjection.WithNextStates(dto);
    }
}
