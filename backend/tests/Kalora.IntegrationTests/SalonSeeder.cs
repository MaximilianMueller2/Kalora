using Kalora.Domain.Salons;
using Kalora.Domain.Services;
using Kalora.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace Kalora.IntegrationTests;

/// <summary>Creates the data a salon needs to take bookings, directly in the database of a test host.</summary>
internal sealed class SalonSeeder(string connectionString, Guid salonId)
{
    public Guid SalonId => salonId;

    public async Task<T> WithDbAsync<T>(Func<Kalora.Infrastructure.Persistence.AppDbContext, Task<T>> action)
    {
        await using var db = TestDb.CreateContext(connectionString, salonId: salonId);
        return await action(db);
    }

    /// <summary>Opens the salon every day from 08:00 to 18:00.</summary>
    public Task OpenAsync() => WithDbAsync(async db =>
    {
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            db.SalonOpeningHours.Add(new SalonOpeningHours { Weekday = day, Start = new TimeOnly(8, 0), End = new TimeOnly(18, 0) });
        }

        await db.SaveChangesAsync();
        return 0;
    });

    public Task ConfigureSalonAsync(Action<Salon> change) => WithDbAsync(async db =>
    {
        change(await db.Salons.SingleAsync());
        await db.SaveChangesAsync();
        return 0;
    });

    public Task<Service> AddServiceAsync(string name = "Haarschnitt", int minutes = 60, int before = 0, int after = 0, bool active = true, bool online = true,
        string category = "Damen", int priceCents = 4500, bool priceIsFrom = false, string? description = null) => WithDbAsync(async db =>
    {
        var existing = await db.ServiceCategories.FirstOrDefaultAsync(c => c.Name == category);
        var categoryEntity = existing ?? new ServiceCategory { Name = category, SortOrder = await db.ServiceCategories.CountAsync() };
        if (existing is null)
        {
            db.ServiceCategories.Add(categoryEntity);
            await db.SaveChangesAsync();
        }

        var service = new Service
        {
            CategoryId = categoryEntity.Id,
            Name = name,
            Description = description,
            DurationMinutes = minutes,
            BufferBeforeMinutes = before,
            BufferAfterMinutes = after,
            IsActive = active,
            IsBookableOnline = online,
            PriceCents = priceCents,
            PriceIsFrom = priceIsFrom,
        };
        db.Services.Add(service);
        await db.SaveChangesAsync();
        return service;
    });

    /// <summary>A person who works every day from 09:00 to 17:00 and offers the given services.</summary>
    public Task<StaffMember> AddStaffAsync(string name, int sortOrder, bool active = true, bool online = true, bool autoConfirm = false, params Service[] offers) => WithDbAsync(async db =>
    {
        var staff = new StaffMember { DisplayName = name, Color = "#2563EB", SortOrder = sortOrder, IsActive = active, IsBookableOnline = online, AutoConfirm = autoConfirm };
        db.StaffMembers.Add(staff);
        await db.SaveChangesAsync();
        db.StaffServices.AddRange(offers.Select(service => new StaffService { StaffId = staff.Id, ServiceId = service.Id }));
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            db.StaffWorkingHours.Add(new StaffWorkingHours { StaffId = staff.Id, Weekday = day, Start = new TimeOnly(9, 0), End = new TimeOnly(17, 0), ValidFrom = new DateOnly(2026, 1, 1) });
        }

        await db.SaveChangesAsync();
        return staff;
    });

    /// <summary>An appointment of a new customer, straight in the database (no availability check, no mails).</summary>
    public Task<Kalora.Domain.Appointments.Appointment> AddAppointmentAsync(
        Service service,
        StaffMember staff,
        DateTimeOffset start,
        Kalora.Domain.Appointments.AppointmentStatus status = Kalora.Domain.Appointments.AppointmentStatus.Requested,
        DateTimeOffset? expiresAt = null,
        int minutes = 60,
        string customerName = "Kim Kundin",
        string language = "de") => WithDbAsync(async db =>
    {
        var customer = new Kalora.Domain.Customers.Customer { Name = customerName, Email = $"kim-{Guid.NewGuid():N}@example.com", Phone = "+49 170 1234567", PreferredLanguage = language };
        db.Customers.Add(customer);
        var appointment = new Kalora.Domain.Appointments.Appointment
        {
            StaffId = staff.Id,
            ServiceId = service.Id,
            CustomerId = customer.Id,
            StartUtc = start,
            EndUtc = start.AddMinutes(minutes),
            BlockedStartUtc = start.AddMinutes(-service.BufferBeforeMinutes),
            BlockedEndUtc = start.AddMinutes(minutes + service.BufferAfterMinutes),
            Status = status,
            Source = Kalora.Domain.Appointments.AppointmentSource.Online,
            Language = language,
            ServiceName = service.Name,
            DurationMinutes = minutes,
            PriceCents = 4500,
            CustomerNote = "Bitte ohne Föhn",
            ExpiresAtUtc = status == Kalora.Domain.Appointments.AppointmentStatus.Requested ? expiresAt ?? start.AddHours(-2) : null,
        };
        appointment.ManageTokenHash = Kalora.Application.Security.TokenHasher.Hash(appointment.Id.ToString("N"));
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment;
    });
}
