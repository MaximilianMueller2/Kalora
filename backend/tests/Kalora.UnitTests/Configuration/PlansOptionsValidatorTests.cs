using Kalora.Application.Configuration;
using Kalora.Domain.Salons;

namespace Kalora.UnitTests.Configuration;

public class PlansOptionsValidatorTests
{
    private readonly PlansOptionsValidator _validator = new();

    [Fact]
    public void The_defaults_are_one_five_and_fifteen_staff_members()
    {
        var options = new PlansOptions();

        Assert.True(_validator.Validate(null, options).Succeeded);
        Assert.Equal([1, 5, 15], [options.MaxStaff(PlanCode.Solo), options.MaxStaff(PlanCode.Team), options.MaxStaff(PlanCode.SalonPlus)]);
    }

    [Theory]
    [InlineData(0, 5, 15, "SoloMaxStaff")]
    [InlineData(1, 0, 15, "TeamMaxStaff")]
    [InlineData(1, 5, 0, "SalonPlusMaxStaff")]
    public void A_plan_without_room_for_anyone_is_refused_by_name(int solo, int team, int plus, string named)
    {
        var result = _validator.Validate(null, new PlansOptions { SoloMaxStaff = solo, TeamMaxStaff = team, SalonPlusMaxStaff = plus });

        Assert.True(result.Failed);
        Assert.Contains(named, result.FailureMessage);
    }

    [Fact]
    public void A_bigger_plan_must_not_allow_fewer_staff()
    {
        var result = _validator.Validate(null, new PlansOptions { SoloMaxStaff = 6, TeamMaxStaff = 5, SalonPlusMaxStaff = 15 });

        Assert.True(result.Failed);
    }
}
