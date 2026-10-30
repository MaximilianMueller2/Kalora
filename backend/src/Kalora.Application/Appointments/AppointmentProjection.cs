using Kalora.Application.Common;
using Kalora.Domain.Appointments;
using Kalora.Domain.Scheduling;

namespace Kalora.Application.Appointments;

internal static class AppointmentProjection
{
    /// <summary>Appointments with their person and customer; the allowed next states are added by <see cref="WithNextStates"/> after the query.</summary>
    public static IQueryable<AppointmentDto> Project(IAppDbContext db, IQueryable<Appointment> appointments) =>
        from a in appointments
        join s in db.StaffMembers on a.StaffId equals s.Id
        join c in db.Customers on a.CustomerId equals c.Id
        select new AppointmentDto(
            a.Id,
            a.Status,
            a.Source,
            a.StartUtc,
            a.EndUtc,
            a.BlockedStartUtc,
            a.BlockedEndUtc,
            a.ServiceId,
            a.ServiceName,
            a.PriceCents,
            a.PriceIsFrom,
            a.Currency,
            new AppointmentStaffDto(s.Id, s.DisplayName, s.Color),
            c.Id,
            new AppointmentCustomerDto(c.Name, c.Email, c.Phone),
            a.CustomerNote,
            a.InternalNote,
            a.ExpiresAtUtc,
            a.CreatedAtUtc,
            Array.Empty<AppointmentStatus>());

    public static AppointmentDto WithNextStates(AppointmentDto dto) => dto with { NextStates = AppointmentStatusRules.NextStates(dto.Status) };
}
