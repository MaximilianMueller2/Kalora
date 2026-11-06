using System.Text.Json.Serialization;
using Kalora.Api.Auth;
using Kalora.Api.Background;
using Kalora.Api.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Kalora.Application;
using Kalora.Infrastructure;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// No request of this API has a reason to be big (the longest text is a privacy notice of 20,000 characters).
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1024 * 1024;
    // No "Server: Kestrel": it only tells an attacker which software to look up.
    options.AddServerHeader = false;
});
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

builder.Services.AddSerilog((_, logger) => logger.ReadFrom.Configuration(builder.Configuration));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// TLS ends at the reverse proxy (Caddy), which tells the API the original scheme. The API container is only reachable
// through the proxy (see deploy/), so its forwarded headers can be trusted.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddKaloraAuth();
builder.Services.AddHostedService<PlatformAdminBootstrap>();
if (builder.Configuration.GetValue<bool>("Mail:Worker:Enabled"))
{
    builder.Services.AddHostedService<EmailOutboxWorker>();
}

if (builder.Configuration.GetValue<bool>("Retention:Enabled"))
{
    builder.Services.AddHostedService<RetentionWorker>();
}

if (builder.Configuration.GetValue<bool>("Scheduler:Enabled"))
{
    builder.Services.AddHostedService<AppointmentSchedulerWorker>();
}

builder.Services.AddPublicRateLimiting(builder.Configuration);
builder.Services
    .AddControllers(options =>
    {
        options.Filters.Add<AntiforgeryValidationFilter>();
        options.Filters.Add<ReadOnlySalonFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
    });
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateDatabaseAsync();
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    // Browsers only honour it over https; the proxy tells the API the original scheme (forwarded headers above).
    app.UseHsts();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseExceptionHandler();
// The request log must not carry the secrets some paths contain (management and invitation links).
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {SafePath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.GetMessageTemplateProperties = (context, _, elapsed, statusCode) =>
    [
        new LogEventProperty("RequestMethod", new ScalarValue(context.Request.Method)),
        new LogEventProperty("SafePath", new ScalarValue(LogPaths.Redact(context.Request.Path.Value ?? string.Empty))),
        new LogEventProperty("StatusCode", new ScalarValue(statusCode)),
        new LogEventProperty("Elapsed", new ScalarValue(elapsed)),
    ];
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();
app.MapAntiforgeryEndpoint();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program;
