using Kalora.Application.Staff;
using Kalora.Domain.Staff;

namespace Kalora.UnitTests.Staff;

public class StaffValidatorsTests
{
    [Theory]
    [InlineData("#7C5CFC", true)]
    [InlineData("#abcdef", true)]
    [InlineData("#ABCDEF", true)]
    [InlineData("7C5CFC", false)]
    [InlineData("#7C5CF", false)]
    [InlineData("#7C5CFCC", false)]
    [InlineData("#7C5CFG", false)]
    [InlineData("", false)]
    public void Colors_are_six_digit_hex_codes(string color, bool valid)
    {
        var result = new SaveStaffRequestValidator().Validate(new SaveStaffRequest("Anna", color, true, false, true));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void The_name_is_required_and_limited()
    {
        var validator = new CreateStaffRequestValidator();

        Assert.False(validator.Validate(new CreateStaffRequest(" ", "#000000", true, false, false)).IsValid);
        Assert.False(validator.Validate(new CreateStaffRequest(new string('x', 101), "#000000", true, false, false)).IsValid);
        Assert.True(validator.Validate(new CreateStaffRequest(new string('x', 100), "#000000", true, false, false)).IsValid);
    }

    [Fact]
    public void Skills_need_unique_services_and_durations_in_five_minute_steps()
    {
        var validator = new StaffSkillsValidator();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        Assert.True(validator.Validate(new StaffSkills([new(a, null), new(b, 45)])).IsValid);
        Assert.False(validator.Validate(new StaffSkills([new(a, null), new(a, 45)])).IsValid);
        Assert.False(validator.Validate(new StaffSkills([new(a, 0)])).IsValid);
        Assert.False(validator.Validate(new StaffSkills([new(a, 33)])).IsValid);
        Assert.False(validator.Validate(new StaffSkills([new(a, 725)])).IsValid);
    }

    [Theory]
    [InlineData("2026-11-10", "2026-11-10", true)]
    [InlineData("2026-11-10", "2026-11-09", false)]
    [InlineData("2026-01-01", "2027-01-01", true)] // 366 days counting both ends
    [InlineData("2026-01-01", "2027-01-02", false)] // 367 days
    public void Absences_are_a_forward_range_of_at_most_a_year(string start, string end, bool valid)
    {
        var result = new CreateAbsenceRequestValidator().Validate(new CreateAbsenceRequest(DateOnly.Parse(start), DateOnly.Parse(end), AbsenceType.Vacation));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void Unknown_absence_types_are_invalid()
    {
        var request = new CreateAbsenceRequest(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 10), (AbsenceType)42);

        Assert.False(new CreateAbsenceRequestValidator().Validate(request).IsValid);
    }
}
