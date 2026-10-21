using Kalora.Domain.Appointments;
using Kalora.Domain.Customers;
using Kalora.Domain.Salons;
using Kalora.Domain.Services;
using Kalora.Domain.Staff;
using Kalora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class AppointmentOverlapTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Overlapping_confirmed_appointments_of_one_staff_member_are_rejected()
    {
        var world = await SeedAsync();
        await world.AddAppointmentAsync(world.StaffA, Nine, 60);

        await world.AssertRejectedAsync(world.StaffA, Nine.AddMinutes(30), 60);
    }

    [Fact]
    public async Task A_pending_request_blocks_the_slot_for_others()
    {
        var world = await SeedAsync();
        await world.AddAppointmentAsync(world.StaffA, Nine, 60, status: AppointmentStatus.Requested);

        await world.AssertRejectedAsync(world.StaffA, Nine, 60, status: AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task Different_staff_members_can_be_booked_at_the_same_time()
    {
        var world = await SeedAsync();
        await world.AddAppointmentAsync(world.StaffA, Nine, 60);

        await world.AddAppointmentAsync(world.StaffB, Nine, 60);
    }

    [Fact]
    public async Task Back_to_back_appointments_are_allowed()
    {
        var world = await SeedAsync();
        await world.AddAppointmentAsync(world.StaffA, Nine, 60);

        await world.AddAppointmentAsync(world.StaffA, Nine.AddMinutes(60), 30);
    }

    [Fact]
    public async Task Buffers_count_towards_the_blocked_range()
    {
        var world = await SeedAsync();
        await world.AddAppointmentAsync(world.StaffA, Nine, 60, bufferAfter: 15);

        // Starts at 10:00 (service ends 10:00) but the 15 min buffer after the first one is still running.
        await world.AssertRejectedAsync(world.StaffA, Nine.AddMinutes(60), 30);
        await world.AddAppointmentAsync(world.StaffA, Nine.AddMinutes(75), 30);
    }

    [Theory]
    [InlineData(AppointmentStatus.Declined)]
    [InlineData(AppointmentStatus.Expired)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task Inactive_appointments_do_not_block_when_they_are_not_requested_or_confirmed(AppointmentStatus status)
    {
        var world = await SeedAsync();
        await world.AddAppointmentAsync(world.StaffA, Nine, 60, status: status);

        // Completed / NoShow in the past are not expected to overlap in practice, but only
        // Requested and Confirmed are guarded by the constraint.
        await world.AddAppointmentAsync(world.StaffA, Nine, 60, status: AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task Cancelling_an_appointment_frees_the_slot()
    {
        var world = await SeedAsync();
        var appointmentId = await world.AddAppointmentAsync(world.StaffA, Nine, 60);

        await using (var db = TestDb.CreateContext(world.ConnectionString, salonId: world.SalonId))
        {
            var appointment = await db.Appointments.SingleAsync(a => a.Id == appointmentId);
            appointment.Status = AppointmentStatus.Cancelled;
            await db.SaveChangesAsync();
        }

        await world.AddAppointmentAsync(world.StaffA, Nine, 60);
    }

    [Fact]
    public async Task Reactivating_a_cancelled_appointment_into_an_occupied_slot_is_rejected()
    {
        var world = await SeedAsync();
        var cancelledId = await world.AddAppointmentAsync(world.StaffA, Nine, 60, status: AppointmentStatus.Cancelled);
        await world.AddAppointmentAsync(world.StaffA, Nine, 60);

        await using var db = TestDb.CreateContext(world.ConnectionString, salonId: world.SalonId);
        var cancelled = await db.Appointments.SingleAsync(a => a.Id == cancelledId);
        cancelled.Status = AppointmentStatus.Confirmed;

        await AssertExclusionViolationAsync(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Concurrent_requests_for_the_same_slot_let_exactly_one_win()
    {
        var world = await SeedAsync();

        var attempts = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(async () =>
            {
                try
                {
                    await world.AddAppointmentAsync(world.StaffA, Nine, 45);
                    return true;
                }
                catch (DbUpdateException)
                {
                    return false;
                }
            }));
        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(won => won));
    }

    private static async Task AssertExclusionViolationAsync(Func<Task> save)
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(save);
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, postgresException.SqlState);
        Assert.Equal(DatabaseConstraints.AppointmentNoOverlap, postgresException.ConstraintName);
    }

    private async Task<World> SeedAsync()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var db = TestDb.CreateContext(connectionString);

        var salon = new Salon { Name = "Test Salon", Slug = $"salon-{Guid.NewGuid():N}" };
        var staffA = new StaffMember { SalonId = salon.Id, DisplayName = "A" };
        var staffB = new StaffMember { SalonId = salon.Id, DisplayName = "B" };
        var category = new ServiceCategory { SalonId = salon.Id, Name = "Cuts" };
        var service = new Service { SalonId = salon.Id, CategoryId = category.Id, Name = "Cut", PriceCents = 3000, DurationMinutes = 30 };
        var customer = new Customer { SalonId = salon.Id, Name = "Kim", Email = "kim@example.com" };
        db.AddRange(salon, staffA, staffB, category, service, customer);
        await db.SaveChangesAsync();

        return new World(connectionString, salon.Id, staffA.Id, staffB.Id, service.Id, customer.Id);
    }

    private sealed record World(string ConnectionString, Guid SalonId, Guid StaffA, Guid StaffB, Guid ServiceId, Guid CustomerId)
    {
        public async Task<Guid> AddAppointmentAsync(
            Guid staffId, DateTimeOffset start, int minutes, int bufferBefore = 0, int bufferAfter = 0,
            AppointmentStatus status = AppointmentStatus.Confirmed)
        {
            await using var db = TestDb.CreateContext(ConnectionString, salonId: SalonId);
            var appointment = Build(staffId, start, minutes, bufferBefore, bufferAfter, status);
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            return appointment.Id;
        }

        public async Task AssertRejectedAsync(
            Guid staffId, DateTimeOffset start, int minutes, AppointmentStatus status = AppointmentStatus.Confirmed)
        {
            await using var db = TestDb.CreateContext(ConnectionString, salonId: SalonId);
            db.Appointments.Add(Build(staffId, start, minutes, 0, 0, status));
            await AssertExclusionViolationAsync(() => db.SaveChangesAsync());
        }

        private Appointment Build(Guid staffId, DateTimeOffset start, int minutes, int bufferBefore, int bufferAfter, AppointmentStatus status)
        {
            var end = start.AddMinutes(minutes);
            return new Appointment
            {
                SalonId = SalonId,
                StaffId = staffId,
                ServiceId = ServiceId,
                CustomerId = CustomerId,
                StartUtc = start,
                EndUtc = end,
                BlockedStartUtc = start.AddMinutes(-bufferBefore),
                BlockedEndUtc = end.AddMinutes(bufferAfter),
                Status = status,
                Source = AppointmentSource.Online,
                ServiceName = "Cut",
                PriceCents = 3000,
                DurationMinutes = minutes,
            };
        }
    }
}
