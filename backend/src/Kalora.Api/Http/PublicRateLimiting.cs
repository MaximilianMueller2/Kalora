using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Kalora.Api.Http;

/// <summary>One fixed window: at most <see cref="PermitLimit"/> requests per <see cref="WindowSeconds"/> from one IP address.</summary>
public sealed class WindowLimit
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }
}

/// <summary>Limits of the public endpoints (anyone can call them), configurable under "RateLimiting".</summary>
public sealed class PublicRateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Profile, services and staff of a salon.</summary>
    public WindowLimit Read { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };

    /// <summary>Free days and start times: the expensive queries.</summary>
    public WindowLimit Availability { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };

    /// <summary>Appointment requests. Ten per ten minutes is plenty for a family booking together.</summary>
    public WindowLimit Booking { get; set; } = new() { PermitLimit = 10, WindowSeconds = 600 };
}

public static class PublicRateLimits
{
    public const string Read = "public-read";
    public const string Availability = "public-availability";
    public const string Booking = "public-booking";

    public static IServiceCollection AddPublicRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PublicRateLimitOptions>(configuration.GetSection(PublicRateLimitOptions.SectionName));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;
            options.AddPolicy(Read, context => Limit(context, o => o.Read));
            options.AddPolicy(Availability, context => Limit(context, o => o.Availability));
            options.AddPolicy(Booking, context => Limit(context, o => o.Booking));
        });
        return services;
    }

    // The address behind the reverse proxy comes from the forwarded headers (see Program). Windows are per policy and per address.
    private static RateLimitPartition<string> Limit(HttpContext context, Func<PublicRateLimitOptions, WindowLimit> select)
    {
        var limit = select(context.RequestServices.GetRequiredService<IOptions<PublicRateLimitOptions>>().Value);
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit.PermitLimit,
                Window = TimeSpan.FromSeconds(limit.WindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var problem = new ProblemDetails { Status = StatusCodes.Status429TooManyRequests, Title = "Too Many Requests" };
        problem.Extensions["code"] = "rate_limited";
        await response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);
    }
}
