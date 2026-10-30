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

var builder = WebApplication.CreateBuilder(args);

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
if (builder.Configuration.GetValue<bool>("Mail:Worker:Enabled"))
{
    builder.Services.AddHostedService<EmailOutboxWorker>();
}

if (builder.Configuration.GetValue<bool>("Scheduler:Enabled"))
{
    builder.Services.AddHostedService<AppointmentSchedulerWorker>();
}

builder.Services.AddPublicRateLimiting(builder.Configuration);
builder.Services
    .AddControllers(options => options.Filters.Add<AntiforgeryValidationFilter>())
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
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();
app.MapAntiforgeryEndpoint();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
