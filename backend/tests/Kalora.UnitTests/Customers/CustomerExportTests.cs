using Kalora.Application.Customers;
using Kalora.Domain.Customers;

namespace Kalora.UnitTests.Customers;

public class CustomerExportTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line\nbreak", "\"line\nbreak\"")]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+49 170", "'+49 170")]
    [InlineData("-5", "'-5")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("=cmd|' /C calc'!A0", "'=cmd|' /C calc'!A0")]
    public void A_cell_is_quoted_when_needed_and_a_formula_is_defused(string? input, string expected)
    {
        Assert.Equal(expected, CustomerExport.Cell(input));
    }

    [Fact]
    public void The_anonymized_name_is_a_fixed_neutral_text()
    {
        var customer = new Customer { Name = "Berta", Email = "b@example.com", Phone = "1", MarketingConsentAtUtc = DateTimeOffset.UnixEpoch, NoShowCount = 3 };

        customer.Anonymize(new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal((Customer.AnonymizedName, null, null, null, 3), (customer.Name, customer.Email, customer.Phone, customer.MarketingConsentAtUtc, customer.NoShowCount));
        Assert.NotNull(customer.AnonymizedAtUtc);
    }
}
