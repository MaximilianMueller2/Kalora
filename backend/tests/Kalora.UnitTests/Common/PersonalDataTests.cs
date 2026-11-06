using Kalora.Application.Common;

namespace Kalora.UnitTests.Common;

public class PersonalDataTests
{
    [Theory]
    [InlineData("550 5.1.1 someone@example.com does not exist", "550 5.1.1 <address> does not exist")]
    [InlineData("a.b+tag@sub.example.co.uk and x_y@z.de", "<address> and <address>")]
    [InlineData("no address here", "no address here")]
    [InlineData("", "")]
    public void Addresses_are_masked(string text, string expected)
    {
        Assert.Equal(expected, PersonalData.MaskEmails(text));
    }

    [Fact]
    public void Nothing_in_gives_nothing_out()
    {
        Assert.Equal(string.Empty, PersonalData.MaskEmails(null));
        Assert.Equal(string.Empty, PersonalData.MaskForLog(null));
    }

    [Fact]
    public void The_value_of_a_violated_database_key_is_masked_with_the_column()
    {
        var message = "23505: duplicate key value violates unique constraint \"ix_customers_email\"\n\nDETAIL: Key (salon_id, email)=(0197a1b2, kim@example.com) already exists.";

        var masked = PersonalData.MaskForLog(message);

        Assert.DoesNotContain("kim@example.com", masked);
        Assert.DoesNotContain("0197a1b2", masked);
        Assert.Contains("Key (salon_id, email)=(<value>) already exists", masked);
    }

    [Fact]
    public void A_logged_exception_keeps_type_message_stack_and_inner_exceptions_but_not_the_personal_data()
    {
        Exception thrown;
        try
        {
            throw new InvalidOperationException("Cannot save kim@example.com", new ArgumentException("Key (email)=(kim@example.com) already exists"));
        }
        catch (Exception exception)
        {
            thrown = exception;
        }

        var text = PersonalData.ForLog(thrown).ToString();

        Assert.DoesNotContain("kim@example.com", text);
        Assert.Contains("System.InvalidOperationException: Cannot save <address>", text);
        Assert.Contains("---> System.ArgumentException: Key (email)=(<value>) already exists", text);
        Assert.Contains(nameof(A_logged_exception_keeps_type_message_stack_and_inner_exceptions_but_not_the_personal_data), text);
        Assert.Equal("Cannot save <address>", PersonalData.ForLog(thrown).Message);
    }

    [Fact]
    public void The_mail_error_column_uses_the_same_idea()
    {
        Assert.DoesNotContain("@", Kalora.Application.Email.EmailOutboxProcessor.Sanitize("550 5.1.1 <kim@example.com>: Recipient address rejected"));
    }
}
