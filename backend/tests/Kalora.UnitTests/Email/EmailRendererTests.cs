using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kalora.Application.Common;
using Kalora.Application.Email;

namespace Kalora.UnitTests.Email;

public partial class EmailRendererTests
{
    private static readonly IEmailRenderer Renderer = new EmailRenderer();

    private static readonly string[] Languages = ["de", "en", "tr"];

    private static readonly string[] CustomerTypes =
    [
        EmailTypes.BookingRequestReceived, EmailTypes.BookingConfirmed, EmailTypes.BookingDeclined, EmailTypes.BookingExpired,
        EmailTypes.BookingCancelledByCustomer, EmailTypes.BookingCancelledBySalon, EmailTypes.BookingReminder, EmailTypes.BookingRescheduled,
    ];

    private static readonly string[] SalonTypes = [EmailTypes.BookingNewForSalon, EmailTypes.BookingCancellationForSalon];

    private static readonly string[] AccountTypes = [EmailTypes.ConfirmEmail, EmailTypes.ResetPassword, EmailTypes.StaffInvitation];

    private static readonly string[] AllTypes = [.. CustomerTypes, .. SalonTypes, .. AccountTypes];

    public static TheoryData<string, string> EveryTypeAndLanguage()
    {
        var data = new TheoryData<string, string>();
        foreach (var type in AllTypes)
        {
            foreach (var language in Languages)
            {
                data.Add(type, language);
            }
        }

        return data;
    }

    private const string ManageUrl = "https://kalora.app/haarwerk/appointment/TOKEN123";

    private static Dictionary<string, object?> Payload(string customerName = "Kim Kundin") => new()
    {
        ["AppointmentId"] = "0197a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b",
        ["CalendarSequence"] = 0,
        ["EndUtc"] = "2026-11-04T09:00:00+00:00",
        ["SalonName"] = "Haarwerk Müller",
        ["SalonAddress"] = "Hauptstraße 1, 10115 Berlin",
        ["SalonPhone"] = "+49 30 123456",
        ["ServiceName"] = "Färben",
        ["StaffName"] = "Anna",
        ["StartUtc"] = "2026-11-04T08:00:00+00:00",
        ["TimeZone"] = "Europe/Berlin",
        ["CustomerName"] = customerName,
        ["CustomerEmail"] = "kim@example.com",
        ["CustomerPhone"] = "+49 170 1234567",
        ["CustomerNote"] = "Bitte ohne Föhn",
        ["Status"] = "Requested",
        ["RespondByUtc"] = "2026-11-05T09:00:00+00:00",
        ["ManageUrl"] = ManageUrl,
        ["BookingUrl"] = "https://kalora.app/haarwerk",
        ["RequestUrl"] = "https://kalora.app/app/requests/abc",
        ["DisplayName"] = "Maria",
        ["ConfirmUrl"] = "https://kalora.app/auth/confirm-email?token=1",
        ["ResetUrl"] = "https://kalora.app/auth/reset-password?token=2",
        ["InvitationUrl"] = "https://kalora.app/auth/invitation/3",
        ["ExpiresAtUtc"] = "2026-11-10T09:00:00+00:00",
    };

    private static RenderedEmail Render(string type, string language, Dictionary<string, object?>? payload = null) =>
        Renderer.Render(type, language, JsonSerializer.Serialize(payload ?? Payload()));

    [Fact]
    public void Every_language_has_every_template()
    {
        foreach (var language in Languages)
        {
            Assert.Equal(AllTypes.Order(), EmailTexts.For(language).Mails.Keys.Order());
        }
    }

    [Fact]
    public void Every_language_has_the_same_labels_status_and_layout_texts()
    {
        var german = EmailTexts.For("de");
        foreach (var language in Languages)
        {
            var texts = EmailTexts.For(language);
            Assert.Equal(german.Labels.Keys.Order(), texts.Labels.Keys.Order());
            Assert.Equal(german.Status.Keys.Order(), texts.Status.Keys.Order());
            Assert.Equal(german.Layout.Keys.Order(), texts.Layout.Keys.Order());
            Assert.Equal(7, texts.Weekdays.Length);
            Assert.Equal(12, texts.Months.Length);
            Assert.All(texts.Mails.Values, mail => Assert.False(string.IsNullOrWhiteSpace(mail.Subject) || string.IsNullOrWhiteSpace(mail.Heading) || mail.Button is null || mail.Intro.Count == 0));
        }
    }

    [Theory]
    [MemberData(nameof(EveryTypeAndLanguage))]
    public void Every_mail_renders_with_all_placeholders_filled_and_no_empty_parts(string type, string language)
    {
        var mail = Render(type, language);

        foreach (var part in new[] { mail.Subject, mail.Html, mail.Text })
        {
            Assert.False(string.IsNullOrWhiteSpace(part));
            Assert.DoesNotMatch(@"\{[a-zA-Z0-9]+\}", part);
        }

        Assert.DoesNotContain("\n", mail.Subject);
        Assert.InRange(mail.Subject.Length, 8, 120);
    }

    [Theory]
    [MemberData(nameof(EveryTypeAndLanguage))]
    public void Every_mail_has_exactly_one_main_button_and_a_plain_text_alternative(string type, string language)
    {
        var mail = Render(type, language);

        Assert.Equal(1, Regex.Count(mail.Html, "class=\"button\""));
        Assert.DoesNotMatch("<[a-zA-Z/]", mail.Text);
        Assert.StartsWith("<!doctype html>", mail.Html);
        Assert.Contains($"<html lang=\"{language}\">", mail.Html);
        Assert.Contains("name=\"viewport\"", mail.Html);
        Assert.Contains("max-width:560px", mail.Html);
    }

    [Theory]
    [MemberData(nameof(EveryTypeAndLanguage))]
    public void Mails_contain_no_advertising(string type, string language)
    {
        var mail = Render(type, language);

        Assert.DoesNotMatch("(?i)newsletter|unsubscribe|abmelden|rabatt|gutschein|discount|coupon|angebot|kampanya|indirim", mail.Html + mail.Text);
    }

    [Theory]
    [MemberData(nameof(EveryTypeAndLanguage))]
    public void Customer_mails_carry_the_management_link_and_the_salons_address_and_phone(string type, string language)
    {
        if (!CustomerTypes.Contains(type))
        {
            return;
        }

        var mail = Render(type, language);

        foreach (var body in new[] { WebUtility.HtmlDecode(mail.Html), mail.Text })
        {
            Assert.Contains(ManageUrl, body);
            Assert.Contains("Haarwerk Müller", body);
            Assert.Contains("Hauptstraße 1, 10115 Berlin", body);
            Assert.Contains("+49 30 123456", body);
        }
    }

    [Theory]
    [InlineData("de", "Mittwoch, 4. November 2026, 09:00 Uhr")]
    [InlineData("en", "Wednesday, November 4, 2026 at 9:00 AM")]
    [InlineData("tr", "4 Kasım 2026 Çarşamba, 09:00")]
    public void The_time_is_written_in_the_salons_zone_and_the_readers_language(string language, string expected)
    {
        var mail = Render(EmailTypes.BookingConfirmed, language);

        Assert.Contains(expected, mail.Text);
        Assert.Contains(expected, WebUtility.HtmlDecode(mail.Html));
    }

    [Theory]
    [InlineData("2026-11-04T08:00:00+00:00", "09:00")] // winter time
    [InlineData("2026-07-01T07:00:00+00:00", "09:00")] // summer time
    [InlineData("2026-03-29T00:30:00+00:00", "01:30")] // before the clocks go forward
    [InlineData("2026-03-29T01:30:00+00:00", "03:30")] // after
    [InlineData("2026-10-25T00:30:00+00:00", "02:30")] // first 02:30 on the day the clocks go back
    [InlineData("2026-10-25T01:30:00+00:00", "02:30")] // second 02:30
    public void Summer_and_winter_time_are_respected(string instant, string localTime)
    {
        var payload = Payload();
        payload["StartUtc"] = instant;

        var mail = Render(EmailTypes.BookingReminder, "de", payload);

        Assert.Contains($"{localTime} Uhr", mail.Text);
    }

    [Theory]
    [InlineData("2026-11-04T23:30:00+00:00", "Donnerstag, 5. November 2026, 00:30 Uhr")] // already the next day in Berlin
    [InlineData("2026-11-04T00:00:00+00:00", "Mittwoch, 4. November 2026, 01:00 Uhr")]
    public void A_time_near_midnight_lands_on_the_right_local_day(string instant, string expected)
    {
        var payload = Payload();
        payload["StartUtc"] = instant;

        Assert.Contains(expected, Render(EmailTypes.BookingReminder, "de", payload).Text);
    }

    [Fact]
    public void Twelve_hour_clocks_handle_noon_and_midnight()
    {
        var noon = Payload();
        noon["StartUtc"] = "2026-11-04T11:00:00+00:00";
        var midnight = Payload();
        midnight["StartUtc"] = "2026-11-04T23:00:00+00:00";

        Assert.Contains("12:00 PM", Render(EmailTypes.BookingReminder, "en", noon).Text);
        Assert.Contains("12:00 AM", Render(EmailTypes.BookingReminder, "en", midnight).Text);
    }

    [Fact]
    public void An_unknown_time_zone_falls_back_to_UTC()
    {
        var payload = Payload();
        payload["TimeZone"] = "Mars/Olympus";

        Assert.Contains("08:00 Uhr", Render(EmailTypes.BookingReminder, "de", payload).Text);
    }

    [Fact]
    public void Customer_input_is_HTML_encoded_in_the_html_and_raw_in_the_text()
    {
        const string evil = "<script>alert('x')</script> & \"Co\"";
        var payload = Payload(evil);
        payload["CustomerNote"] = "<img src=x onerror=alert(1)>";

        var mail = Render(EmailTypes.BookingNewForSalon, "de", payload);

        Assert.DoesNotContain("<script", mail.Html);
        Assert.DoesNotContain("<img", mail.Html);
        Assert.Contains("&lt;script&gt;", mail.Html);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", mail.Html);
        Assert.Contains(evil, mail.Text);
    }

    [Fact]
    public void Salon_names_are_encoded_in_the_header_and_the_title()
    {
        var payload = Payload();
        payload["SalonName"] = "Haar & Mehr <b>";

        var mail = Render(EmailTypes.BookingConfirmed, "de", payload);

        Assert.DoesNotContain("<b>", mail.Html);
        Assert.Contains("Haar &amp; Mehr &lt;b&gt;", mail.Html);
    }

    [Fact]
    public void Links_that_are_not_web_links_are_left_out()
    {
        var payload = Payload();
        payload["ManageUrl"] = "javascript:alert(1)";

        var mail = Render(EmailTypes.BookingConfirmed, "de", payload);

        Assert.DoesNotContain("javascript:", mail.Html + mail.Text);
        Assert.Equal(0, Regex.Count(mail.Html, "class=\"button\""));
    }

    [Fact]
    public void Only_confirmation_and_reschedule_offer_web_calendar_links_and_names_the_attached_file()
    {
        foreach (var language in Languages)
        {
            var confirmed = Render(EmailTypes.BookingConfirmed, language);
            var html = WebUtility.HtmlDecode(confirmed.Html);
            var texts = EmailTexts.For(language);

            foreach (var body in new[] { html, confirmed.Text })
            {
                Assert.Contains("https://calendar.google.com/calendar/render?action=TEMPLATE", body);
                Assert.Contains("https://outlook.live.com/calendar/0/deeplink/compose", body);
                Assert.Contains(texts.Layout["googleCalendar"], body);
                Assert.Contains(texts.Layout["outlookCalendar"], body);
                Assert.Contains(texts.Layout["calendarAttached"], body);
            }

            Assert.Equal(1, Regex.Count(confirmed.Html, "class=\"button\"")); // the calendar links are quiet text links

            foreach (var other in AllTypes.Where(type => type is not (EmailTypes.BookingConfirmed or EmailTypes.BookingRescheduled)))
            {
                var mail = Render(other, language);
                Assert.DoesNotContain("calendar.google.com", mail.Html + mail.Text);
                Assert.DoesNotContain("outlook.live.com", mail.Html + mail.Text);
            }
        }
    }

    [Fact]
    public void The_google_link_is_prefilled_with_title_time_place_and_link()
    {
        var text = Render(EmailTypes.BookingConfirmed, "de").Text;

        var url = Regex.Match(text, @"https://calendar\.google\.com\S+").Value;
        var query = System.Web.HttpUtility.ParseQueryString(url[(url.IndexOf('?') + 1)..]);
        Assert.Equal("TEMPLATE", query["action"]);
        Assert.Equal("Färben – Haarwerk Müller", query["text"]);
        Assert.Equal("20261104T080000Z/20261104T090000Z", query["dates"]);
        Assert.Equal("Hauptstraße 1, 10115 Berlin", query["location"]);
        Assert.Contains(ManageUrl, query["details"]);
    }

    [Fact]
    public void The_outlook_link_is_prefilled_with_title_time_place_and_link()
    {
        var text = Render(EmailTypes.BookingConfirmed, "en").Text;

        var url = Regex.Match(text, @"https://outlook\.live\.com\S+").Value;
        var query = System.Web.HttpUtility.ParseQueryString(url[(url.IndexOf('?') + 1)..]);
        Assert.Equal("addevent", query["rru"]);
        Assert.Equal("Färben – Haarwerk Müller", query["subject"]);
        Assert.Equal("2026-11-04T08:00:00Z", query["startdt"]);
        Assert.Equal("2026-11-04T09:00:00Z", query["enddt"]);
        Assert.Equal("Hauptstraße 1, 10115 Berlin", query["location"]);
        Assert.Contains(ManageUrl, query["body"]);
    }

    [Fact]
    public void Without_an_appointment_in_the_data_the_confirmation_has_no_calendar_part()
    {
        var payload = Payload();
        payload.Remove("AppointmentId");

        var mail = Render(EmailTypes.BookingConfirmed, "de", payload);

        Assert.DoesNotContain("calendar.google.com", mail.Html + mail.Text);
        Assert.DoesNotContain("Zum Kalender hinzufügen", mail.Text);
    }

    [Fact]
    public void The_button_leads_where_the_mail_says()
    {
        string Button(string type) => Regex.Match(Render(type, "de").Html, "class=\"button\" href=\"([^\"]+)\"").Groups[1].Value;

        Assert.Equal(ManageUrl, Button(EmailTypes.BookingRequestReceived));
        Assert.Equal(ManageUrl, Button(EmailTypes.BookingConfirmed));
        Assert.Equal(ManageUrl, Button(EmailTypes.BookingReminder));
        Assert.Equal("https://kalora.app/haarwerk", Button(EmailTypes.BookingDeclined));
        Assert.Equal("https://kalora.app/haarwerk", Button(EmailTypes.BookingExpired));
        Assert.Equal("https://kalora.app/haarwerk", Button(EmailTypes.BookingCancelledByCustomer));
        Assert.Equal("https://kalora.app/haarwerk", Button(EmailTypes.BookingCancelledBySalon));
        Assert.Equal("https://kalora.app/app/requests/abc", Button(EmailTypes.BookingNewForSalon));
        Assert.Equal("https://kalora.app/auth/confirm-email?token=1", Button(EmailTypes.ConfirmEmail));
        Assert.Equal("https://kalora.app/auth/reset-password?token=2", Button(EmailTypes.ResetPassword));
        Assert.Equal("https://kalora.app/auth/invitation/3", Button(EmailTypes.StaffInvitation));
    }

    [Fact]
    public void A_declined_request_shows_the_reason_only_when_there_is_one()
    {
        var without = Render(EmailTypes.BookingDeclined, "de");
        var payload = Payload();
        payload["Reason"] = "Wir haben an dem Tag Betriebsausflug.";
        var with = Render(EmailTypes.BookingDeclined, "de", payload);

        Assert.DoesNotContain("Begründung", without.Text);
        Assert.Contains("Begründung des Salons: Wir haben an dem Tag Betriebsausflug.", with.Text);
    }

    [Fact]
    public void The_answer_deadline_appears_only_when_known()
    {
        var payload = Payload();
        payload.Remove("RespondByUtc");

        Assert.Contains("Der Salon antwortet bis Donnerstag, 5. November 2026, 10:00 Uhr.", Render(EmailTypes.BookingRequestReceived, "de").Text);
        Assert.DoesNotContain("antwortet bis", Render(EmailTypes.BookingRequestReceived, "de", payload).Text);
    }

    [Fact]
    public void Salon_mails_show_the_customer_with_contact_details_and_the_state_of_the_request()
    {
        var mail = Render(EmailTypes.BookingNewForSalon, "de");

        Assert.Contains("Kim Kundin", mail.Subject);
        Assert.Contains("Kunde: Kim Kundin", mail.Text);
        Assert.Contains("E-Mail: kim@example.com", mail.Text);
        Assert.Contains("Telefon: +49 170 1234567", mail.Text);
        Assert.Contains("Nachricht: Bitte ohne Föhn", mail.Text);
        Assert.Contains("Status: Wartet auf Bestätigung", mail.Text);
        Assert.DoesNotContain(ManageUrl, mail.Text); // customers' secret link never goes to the salon
    }

    [Fact]
    public void Account_mails_come_from_Kalora_and_say_they_are_automatic()
    {
        var mail = Render(EmailTypes.ResetPassword, "de");

        Assert.Contains("<td style=\"padding:24px 28px 8px;font-size:15px;font-weight:bold;color:#6a1bd0;\">Kalora</td>", mail.Html);
        Assert.Contains("automatisch verschickt", mail.Text);
        Assert.DoesNotContain(ManageUrl, mail.Text);
    }

    [Fact]
    public void An_invitation_names_the_salon_and_the_expiry()
    {
        var mail = Render(EmailTypes.StaffInvitation, "en");

        Assert.Equal("Haarwerk Müller invites you to Kalora", mail.Subject);
        Assert.Contains("valid until Tuesday, November 10, 2026 at 10:00 AM", mail.Text);
    }

    [Fact]
    public void An_unsupported_language_falls_back_to_German()
    {
        var mail = Render(EmailTypes.BookingConfirmed, "fr");

        Assert.Equal("Dein Termin bei Haarwerk Müller ist bestätigt", mail.Subject);
        Assert.Contains("<html lang=\"de\">", mail.Html);
    }

    [Fact]
    public void An_unknown_template_or_a_broken_payload_is_an_error()
    {
        Assert.Throws<InvalidOperationException>(() => Renderer.Render("booking.unknown", "de", "{}"));
        Assert.ThrowsAny<JsonException>(() => Renderer.Render(EmailTypes.BookingConfirmed, "de", "{not json"));
    }

    [Fact]
    public void Missing_optional_data_does_not_break_a_mail()
    {
        var mail = Renderer.Render(EmailTypes.BookingConfirmed, "de", "{}");

        Assert.False(string.IsNullOrWhiteSpace(mail.Html));
        Assert.DoesNotMatch(@"\{[a-zA-Z0-9]+\}", mail.Text);
        Assert.Equal(0, Regex.Count(mail.Html, "class=\"button\""));
        Assert.Equal(1, Regex.Count(Renderer.Render(EmailTypes.BookingConfirmed, "de", null).Html, "<h1"));
    }

    [Fact]
    public void Facts_are_listed_in_order_in_the_text()
    {
        var text = Render(EmailTypes.BookingConfirmed, "de").Text;

        Assert.True(text.IndexOf("Leistung: Färben", StringComparison.Ordinal) < text.IndexOf("Bei: Anna", StringComparison.Ordinal));
        Assert.True(text.IndexOf("Bei: Anna", StringComparison.Ordinal) < text.IndexOf("Wann:", StringComparison.Ordinal));
    }

    [Fact]
    public void Dark_text_on_white_and_a_readable_button()
    {
        var html = Render(EmailTypes.BookingConfirmed, "de").Html;

        Assert.Contains("background:#ffffff", html);
        Assert.Contains("color:#1d1b20", html);
        Assert.Contains("background:#6a1bd0;color:#ffffff", html);
    }
}
