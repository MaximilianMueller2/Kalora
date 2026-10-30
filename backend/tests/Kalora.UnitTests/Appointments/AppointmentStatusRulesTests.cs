using Kalora.Domain.Appointments;

namespace Kalora.UnitTests.Appointments;

public class AppointmentStatusRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private static readonly HashSet<(AppointmentStatus, AppointmentStatus)> Allowed =
    [
        (AppointmentStatus.Requested, AppointmentStatus.Confirmed),
        (AppointmentStatus.Requested, AppointmentStatus.Declined),
        (AppointmentStatus.Requested, AppointmentStatus.Expired),
        (AppointmentStatus.Requested, AppointmentStatus.Cancelled),
        (AppointmentStatus.Confirmed, AppointmentStatus.Cancelled),
        (AppointmentStatus.Confirmed, AppointmentStatus.Completed),
        (AppointmentStatus.Confirmed, AppointmentStatus.NoShow),
    ];

    public static TheoryData<AppointmentStatus, AppointmentStatus> AllPairs()
    {
        var data = new TheoryData<AppointmentStatus, AppointmentStatus>();
        foreach (var from in Enum.GetValues<AppointmentStatus>())
        {
            foreach (var to in Enum.GetValues<AppointmentStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void Exactly_the_listed_moves_are_allowed(AppointmentStatus from, AppointmentStatus to)
    {
        Assert.Equal(Allowed.Contains((from, to)), AppointmentStatusRules.CanMove(from, to));
    }

    [Theory]
    [InlineData(AppointmentStatus.Declined)]
    [InlineData(AppointmentStatus.Expired)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow)]
    public void The_final_states_lead_nowhere(AppointmentStatus status)
    {
        Assert.Empty(AppointmentStatusRules.NextStates(status));
    }

    [Fact]
    public void Only_open_and_confirmed_appointments_hold_their_time()
    {
        var blocking = Enum.GetValues<AppointmentStatus>().Where(AppointmentStatusRules.BlocksTime);

        Assert.Equal([AppointmentStatus.Requested, AppointmentStatus.Confirmed], blocking);
    }

    [Theory]
    [InlineData(AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Declined)]
    [InlineData(AppointmentStatus.Expired)]
    [InlineData(AppointmentStatus.Cancelled)]
    public void A_move_sets_the_status_and_records_exactly_when(AppointmentStatus to)
    {
        var appointment = new Appointment { ExpiresAtUtc = Now.AddHours(5) };

        Assert.True(appointment.TryMoveTo(to, Now));

        Assert.Equal(to, appointment.Status);
        var stamps = new[] { appointment.ConfirmedAtUtc, appointment.DeclinedAtUtc, appointment.ExpiredAtUtc, appointment.CancelledAtUtc, appointment.CompletedAtUtc, appointment.NoShowAtUtc };
        Assert.Single(stamps, stamp => stamp is not null);
        Assert.Equal(Now, stamps.Single(stamp => stamp is not null));
    }

    [Fact]
    public void Confirming_ends_the_time_to_answer()
    {
        var appointment = new Appointment { ExpiresAtUtc = Now.AddHours(5) };

        appointment.TryMoveTo(AppointmentStatus.Confirmed, Now);

        Assert.Null(appointment.ExpiresAtUtc);
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow)]
    public void What_happened_is_recorded_for_confirmed_appointments(AppointmentStatus to)
    {
        var appointment = new Appointment { Status = AppointmentStatus.Confirmed };

        Assert.True(appointment.TryMoveTo(to, Now));
        Assert.Equal(to, appointment.Status);
        Assert.Equal(Now, to == AppointmentStatus.Completed ? appointment.CompletedAtUtc : appointment.NoShowAtUtc);
    }

    [Fact]
    public void A_forbidden_move_changes_nothing()
    {
        var appointment = new Appointment { Status = AppointmentStatus.Declined };

        Assert.False(appointment.TryMoveTo(AppointmentStatus.Confirmed, Now));

        Assert.Equal(AppointmentStatus.Declined, appointment.Status);
        Assert.Null(appointment.ConfirmedAtUtc);
    }
}
