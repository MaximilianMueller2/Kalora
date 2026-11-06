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

    /// <summary>
    /// The pages behind a management token (look, calendar file, cancel). A token has 256 bits, so guessing is hopeless anyway;
    /// this keeps scripts that try from hammering the lookup.
    /// </summary>
    public WindowLimit Manage { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };

    /// <summary>
    /// Sign-in attempts per address. A single account is locked after five wrong passwords anyway (15 minutes); this stops one address from
    /// trying many accounts. Generous, because a whole salon may sit behind one address.
    /// </summary>
    public WindowLimit Login { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };

    /// <summary>New accounts per address.</summary>
    public WindowLimit Register { get; set; } = new() { PermitLimit = 10, WindowSeconds = 3600 };

    /// <summary>E-mail confirmation, "forgot password" and "reset password": each can send a mail or test a token.</summary>
    public WindowLimit Recovery { get; set; } = new() { PermitLimit = 10, WindowSeconds = 900 };

    /// <summary>Everything else behind the API, per address: a backstop against floods, far above what a person does.</summary>
    public WindowLimit General { get; set; } = new() { PermitLimit = 600, WindowSeconds = 60 };
}

public static class PublicRateLimits
{
    public const string Read = "public-read";
    public const string Availability = "public-availability";
    public const string Booking = "public-booking";
    public const string Manage = "public-manage";
    public const string Login = "auth-login";
    public const string Register = "auth-register";
    public const string Recovery = "auth-recovery";

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
            options.AddPolicy(Manage, context => Limit(context, o => o.Manage));
            options.AddPolicy(Login, context => Limit(context, o => o.Login));
            options.AddPolicy(Register, context => Limit(context, o => o.Register));
            options.AddPolicy(Recovery, context => Limit(context, o => o.Recovery));
            // Requests that name no policy of their own count here; health checks do not.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/health")
                    ? RateLimitPartition.GetNoLimiter("health")
                    : General(context));
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

    private static RateLimitPartition<string> General(HttpContext context)
    {
        var limit = context.RequestServices.GetRequiredService<IOptions<PublicRateLimitOptions>>().Value.General;
        return RateLimitPartition.GetFixedWindowLimiter(
            "general:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
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
