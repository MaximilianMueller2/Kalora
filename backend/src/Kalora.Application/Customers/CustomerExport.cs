using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Kalora.Application.Customers;

/// <summary>The two file formats of the data export (Art. 15 and 20 GDPR): JSON for machines, CSV for people with a spreadsheet.</summary>
internal static class CustomerExport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static readonly string[] Header =
    [
        "appointmentId", "status", "startUtc", "endUtc", "service", "staff", "priceCents", "priceIsFrom", "currency", "customerNote", "internalNote",
        "customerName", "customerEmail", "customerPhone",
    ];

    public static byte[] Json(CustomerDetailDto customer, DateTimeOffset exportedAt) =>
        JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                exportedAtUtc = exportedAt,
                customer = new
                {
                    customer.Name,
                    customer.Email,
                    customer.Phone,
                    customer.PreferredLanguage,
                    customer.CreatedAtUtc,
                    customer.LastVisitAtUtc,
                    noShowCount = customer.NoShows,
                    customer.MarketingConsentAtUtc,
                },
                appointments = customer.Appointments.Select(a => new
                {
                    a.Id,
                    status = a.Status.ToString(),
                    a.StartUtc,
                    a.EndUtc,
                    a.ServiceName,
                    a.StaffName,
                    a.PriceCents,
                    a.PriceIsFrom,
                    a.Currency,
                    a.CustomerNote,
                    a.InternalNote,
                }),
            },
            JsonOptions);

    /// <summary>One row per appointment with the customer's data repeated; a customer without appointments gets one row with only that. UTF-8 with a byte order mark so spreadsheets read umlauts correctly.</summary>
    public static byte[] Csv(CustomerDetailDto customer)
    {
        var text = new StringBuilder();
        text.Append(string.Join(',', Header)).Append("\r\n");
        var who = new[] { customer.Name, customer.Email, customer.Phone };
        if (customer.Appointments.Count == 0)
        {
            text.Append(Row(["", "", "", "", "", "", "", "", "", "", "", .. who])).Append("\r\n");
        }

        foreach (var a in customer.Appointments)
        {
            text.Append(Row([
                a.Id.ToString(), a.Status.ToString(), Iso(a.StartUtc), Iso(a.EndUtc), a.ServiceName, a.StaffName,
                a.PriceCents.ToString(CultureInfo.InvariantCulture), a.PriceIsFrom ? "true" : "false", a.Currency, a.CustomerNote, a.InternalNote,
                .. who])).Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    private static string Iso(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string Row(IEnumerable<string?> cells) => string.Join(',', cells.Select(Cell));

    /// <summary>Quotes a value when needed. A value that a spreadsheet would read as a formula (starts with = + - @ or a control character) gets a leading apostrophe, so a note cannot run code on the salon's computer.</summary>
    internal static string Cell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var safe = value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;
        return safe.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + safe.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : safe;
    }
}
