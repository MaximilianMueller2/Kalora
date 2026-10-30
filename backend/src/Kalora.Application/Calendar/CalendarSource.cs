using System.Globalization;
using System.Text.Json;

namespace Kalora.Application.Calendar;

/// <summary>The facts about an appointment that calendar entries and links are made from; it comes from the database or from a queued mail.</summary>
internal sealed record CalendarSource(
    Guid AppointmentId,
    int Sequence,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string TimeZone,
    DateTimeOffset Stamp,
    string SalonName,
    string? SalonAddress,
    string ServiceName,
    string? StaffName,
    string? ManageUrl,
    string Language,
    string? CustomerName)
{
    /// <summary>Reads the payload of a customer mail (names as the booking service writes them). Null when the appointment's identity or time is missing.</summary>
    public static CalendarSource? FromPayload(JsonElement root, string language)
    {
        string? Text(string name) =>
            root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString() : null;

        DateTimeOffset? Instant(string name) =>
            Text(name) is { } text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant) ? instant : null;

        if (!Guid.TryParse(Text("AppointmentId"), out var id) || Instant("StartUtc") is not { } start || Instant("EndUtc") is not { } end
            || Text("SalonName") is not { } salon || Text("ServiceName") is not { } service)
        {
            return null;
        }

        var sequence = root.TryGetProperty("CalendarSequence", out var number) && number.TryGetInt32(out var parsed) ? parsed : 0;
        return new CalendarSource(
            id, sequence, start, end, Text("TimeZone") ?? "UTC", Instant("StampUtc") ?? start, salon, Text("SalonAddress"), service,
            Text("StaffName"), Text("ManageUrl"), language, Text("CustomerName"));
    }
}
