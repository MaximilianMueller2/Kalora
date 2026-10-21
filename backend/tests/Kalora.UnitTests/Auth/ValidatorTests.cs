using Kalora.Application.Auth;
using Kalora.Application.Common;
using Kalora.Domain.Salons;

namespace Kalora.UnitTests.Auth;

public class ValidatorTests
{
    private readonly RegisterRequestValidator _register = new();

    private static RegisterRequest Valid() =>
        new("Salon", SalonIndustry.Barber, "Maria", "maria@example.com", "ten-chars-ok", "tr", AcceptTerms: true);

    [Fact]
    public void A_complete_registration_is_valid() => Assert.True(_register.Validate(Valid()).IsValid);

    [Theory]
    [InlineData("", "validation.required", "email")]
    [InlineData("no-at-sign", "validation.email_invalid", "email")]
    public void Invalid_emails_report_code_and_field(string email, string code, string field)
    {
        var error = _register.Validate(Valid() with { Email = email }).ToError();

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal((code, field), (error.Code, error.Parameter));
    }

    [Theory]
    [InlineData("123456789", false)]
    [InlineData("1234567890", true)]
    public void Passwords_need_at_least_ten_characters(string password, bool valid) =>
        Assert.Equal(valid, _register.Validate(Valid() with { Password = password }).IsValid);

    [Fact]
    public void The_terms_must_be_accepted() =>
        Assert.Equal("auth.terms_not_accepted", _register.Validate(Valid() with { AcceptTerms = false }).ToError().Code);

    [Fact]
    public void Invitations_cannot_grant_the_owner_role()
    {
        var validator = new CreateInvitationRequestValidator();

        Assert.True(validator.Validate(new CreateInvitationRequest(Guid.NewGuid(), "a@example.com", SalonRole.Staff)).IsValid);
        Assert.Equal(
            "invitation.role_invalid",
            validator.Validate(new CreateInvitationRequest(Guid.NewGuid(), "a@example.com", SalonRole.Owner)).ToError().Code);
    }
}
