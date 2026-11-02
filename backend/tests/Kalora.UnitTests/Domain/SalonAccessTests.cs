using Kalora.Domain.Salons;

namespace Kalora.UnitTests.Domain;

public class SalonAccessTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private static SalonAccess Access(SalonStatus status, DateTimeOffset? trialEnds = null) =>
        new Salon { Status = status, TrialEndsAtUtc = trialEnds }.AccessAt(Now);

    [Theory]
    [InlineData(SalonStatus.Active)]
    [InlineData(SalonStatus.PastDue)]
    public void Active_and_past_due_salons_work_fully_whatever_the_trial_date_says(SalonStatus status)
    {
        Assert.Equal(SalonAccess.Full, Access(status, Now.AddDays(-30)));
    }

    [Fact]
    public void A_running_trial_works_fully_and_so_does_one_without_an_end()
    {
        Assert.Equal(SalonAccess.Full, Access(SalonStatus.Trial, Now.AddSeconds(1)));
        Assert.Equal(SalonAccess.Full, Access(SalonStatus.Trial));
    }

    [Fact]
    public void A_trial_is_over_at_the_moment_it_ends_without_any_job()
    {
        Assert.Equal(SalonAccess.TrialExpired, Access(SalonStatus.Trial, Now));
        Assert.Equal(SalonAccess.TrialExpired, Access(SalonStatus.Trial, Now.AddDays(-1)));
    }

    [Fact]
    public void A_suspended_or_cancelled_salon_is_limited_even_with_a_running_trial()
    {
        Assert.Equal(SalonAccess.Suspended, Access(SalonStatus.Suspended, Now.AddDays(10)));
        Assert.Equal(SalonAccess.Cancelled, Access(SalonStatus.Cancelled, Now.AddDays(10)));
    }
}
