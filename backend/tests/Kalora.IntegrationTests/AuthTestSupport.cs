using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Kalora.Application.Auth;
using Kalora.Application.Common;
using Kalora.Domain.Messaging;
using Kalora.Domain.Salons;
using Kalora.Domain.Staff;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Kalora.IntegrationTests;

internal static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(), new Kalora.Api.Http.TimeOnlyJsonConverter() },
    };
}

/// <summary>The real API with real cookie authentication and a controllable clock, over HTTPS (the cookies are Secure).</summary>
public sealed class AuthApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}

/// <summary>A browser-like client: keeps cookies and sends the antiforgery header the way the frontend will.</summary>
internal sealed class ApiSession(HttpClient client)
{
    public HttpClient Client => client;

    public Task<HttpResponseMessage> GetAsync(string url) => client.GetAsync(url);

    public async Task<HttpResponseMessage> PutAsync(string url, object body)
    {
        await RefreshAntiforgeryAsync();
        return await client.PutAsJsonAsync(url, body);
    }

    public async Task<HttpResponseMessage> DeleteAsync(string url)
    {
        await RefreshAntiforgeryAsync();
        return await client.DeleteAsync(url);
    }

    public async Task<T> GetJsonAsync<T>(string url) => (await client.GetFromJsonAsync<T>(url, TestJson.Options))!;

    public async Task<HttpResponseMessage> PostAsync(string url, object? body = null)
    {
        await RefreshAntiforgeryAsync();
        return await client.PostAsJsonAsync(url, body ?? new { });
    }

    /// <summary>Tokens are bound to the signed-in user, so a fresh one is fetched before every write.</summary>
    public async Task RefreshAntiforgeryAsync()
    {
        var response = await client.GetAsync("/api/antiforgery");
        response.EnsureSuccessStatusCode();
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        var token = Uri.UnescapeDataString(cookie["XSRF-TOKEN=".Length..].Split(';')[0]);
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);
    }

    public async Task<MeResponse> MeAsync() =>
        (await client.GetFromJsonAsync<MeResponse>("/api/me", TestJson.Options))!;
}

internal sealed class AuthTestHost : IAsyncDisposable
{
    public const string Password = "correct-horse-battery";

    private readonly AuthApiFactory _factory;

    private AuthTestHost(AuthApiFactory factory, string connectionString)
    {
        _factory = factory;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public FakeTimeProvider Clock => _factory.Clock;

    public static async Task<AuthTestHost> StartAsync(PostgresFixture postgres)
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        return new AuthTestHost(new AuthApiFactory(connectionString), connectionString);
    }

    public ApiSession NewSession(Uri? baseAddress = null) => new(_factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = baseAddress ?? new Uri("https://localhost"),
    }));

    public static RegisterRequest Registration(string email, string salonName = "Haarwerk Müller", string language = "de") =>
        new(salonName, SalonIndustry.Hair, "Maria Müller", email, Password, language, AcceptTerms: true);

    /// <summary>Registers an owner in a new session; the session stays signed in.</summary>
    public async Task<(ApiSession Session, MeResponse Me)> RegisterOwnerAsync(string email, string salonName = "Haarwerk Müller")
    {
        var session = NewSession();
        var response = await session.PostAsync("/api/auth/register", Registration(email, salonName));
        response.EnsureSuccessStatusCode();
        return (session, (await response.Content.ReadFromJsonAsync<MeResponse>(TestJson.Options))!);
    }

    /// <summary>Adds a staff member, invites them with the given role and returns their signed-in session.</summary>
    public async Task<ApiSession> InviteAndAcceptAsync(ApiSession owner, Guid salonId, string displayName, string email, SalonRole role)
    {
        var staff = await AddStaffAsync(salonId, displayName);
        (await owner.PostAsync("/api/invitations", new CreateInvitationRequest(staff.Id, email, role))).EnsureSuccessStatusCode();
        var mail = Assert.Single(await OutboxAsync(EmailTypes.StaffInvitation, email));
        var url = JsonDocument.Parse(mail.PayloadJson!).RootElement.GetProperty("InvitationUrl").GetString()!;
        var token = Uri.UnescapeDataString(new Uri(url).Segments[^1]);

        var session = NewSession();
        (await session.PostAsync($"/api/public/invitations/{token}/accept", new AcceptInvitationRequest(Password, "de"))).EnsureSuccessStatusCode();
        return session;
    }

    public async Task<StaffMember> AddStaffAsync(Guid salonId, string displayName)
    {
        var staff = new StaffMember { DisplayName = displayName };
        await using var db = TestDb.CreateContext(ConnectionString, salonId: salonId);
        db.StaffMembers.Add(staff);
        await db.SaveChangesAsync();
        return staff;
    }

    /// <summary>Books a service for a new customer, so the service counts as "has appointments".</summary>
    public async Task AddAppointmentAsync(Guid salonId, Guid serviceId)
    {
        var staff = new StaffMember { DisplayName = "Booked staff" };
        var customer = new Kalora.Domain.Customers.Customer { Name = "Kim", Email = $"kim-{Guid.NewGuid():N}@example.com" };
        await using var db = TestDb.CreateContext(ConnectionString, salonId: salonId);
        db.AddRange(staff, customer);
        await db.SaveChangesAsync();

        var start = new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero);
        db.Appointments.Add(new Kalora.Domain.Appointments.Appointment
        {
            StaffId = staff.Id,
            ServiceId = serviceId,
            CustomerId = customer.Id,
            StartUtc = start,
            EndUtc = start.AddMinutes(30),
            BlockedStartUtc = start,
            BlockedEndUtc = start.AddMinutes(30),
            ServiceName = "Cut",
            DurationMinutes = 30,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>An appointment of a new customer for the given staff member, service and status.</summary>
    public async Task<Guid> AddBookingAsync(Guid salonId, Guid staffId, Guid serviceId, DateTimeOffset startUtc, Kalora.Domain.Appointments.AppointmentStatus status, int minutes = 30)
    {
        var customer = new Kalora.Domain.Customers.Customer { Name = $"Kundin {Guid.NewGuid():N}"[..14], Email = $"k-{Guid.NewGuid():N}@example.com" };
        await using var db = TestDb.CreateContext(ConnectionString, salonId: salonId);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var appointment = new Kalora.Domain.Appointments.Appointment
        {
            StaffId = staffId,
            ServiceId = serviceId,
            CustomerId = customer.Id,
            StartUtc = startUtc,
            EndUtc = startUtc.AddMinutes(minutes),
            BlockedStartUtc = startUtc,
            BlockedEndUtc = startUtc.AddMinutes(minutes),
            Status = status,
            ServiceName = "Cut",
            DurationMinutes = minutes,
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment.Id;
    }

    public async Task<List<EmailOutbox>> OutboxAsync(string type, string toEmail)
    {
        await using var db = TestDb.CreateContext(ConnectionString);
        return await db.AllTenants<EmailOutbox>().Where(e => e.Type == type && e.ToEmail == toEmail).ToListAsync();
    }

    /// <summary>Reads a link parameter (e.g. "token") out of a queued mail's payload URL property.</summary>
    public async Task<string> LinkParameterAsync(string type, string toEmail, string urlProperty, string parameter)
    {
        var mail = Assert.Single(await OutboxAsync(type, toEmail));
        var url = JsonDocument.Parse(mail.PayloadJson!).RootElement.GetProperty(urlProperty).GetString()!;
        return HttpUtility.ParseQueryString(new Uri(url).Query)[parameter]
            ?? throw new InvalidOperationException($"No '{parameter}' in {url}");
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();
}

internal static class ResponseExtensions
{
    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response)
    {
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}

/// <summary>RFC 6238 time-based one-time passwords, as produced by an authenticator app.</summary>
internal static class Totp
{
    public static string Code(string base32Key)
    {
        var key = Base32Decode(base32Key);
        var counter = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        Array.Reverse(counter);
        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        var buffer = 0;
        var bits = 0;
        foreach (var character in input.TrimEnd('=').ToUpperInvariant())
        {
            buffer = (buffer << 5) | alphabet.IndexOf(character);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)(buffer >> bits));
                buffer &= (1 << bits) - 1;
            }
        }

        return [.. bytes];
    }
}
