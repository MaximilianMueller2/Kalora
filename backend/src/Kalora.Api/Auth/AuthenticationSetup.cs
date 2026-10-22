using Kalora.Application.Identity;
using Kalora.Domain.Salons;
using Kalora.Infrastructure.Identity;
using Kalora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace Kalora.Api.Auth;

internal static class AuthenticationSetup
{
    public const string AntiforgeryHeader = "X-XSRF-TOKEN";
    public const string AntiforgeryRequestCookie = "XSRF-TOKEN";

    public static IServiceCollection AddKaloraAuth(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<ISessionService, SessionService>();

        // Keys are stored in the database so cookies, reset links and e-mail tokens survive restarts and redeploys.
        services.AddDataProtection().SetApplicationName("Kalora").PersistKeysToDbContext<AppDbContext>();

        services.AddIdentityCore<AppUser>(options =>
            {
                // Length matters more than character-class rules; the validator enforces the same minimum.
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
                options.User.RequireUniqueEmail = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<KaloraClaimsPrincipalFactory>();

        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(24));
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "kalora.session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;

            // An API answers with status codes; redirecting to a login page is the frontend's job.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicies.SalonMember, policy => policy.RequireRole(
                nameof(SalonRole.Owner), nameof(SalonRole.Manager), nameof(SalonRole.Staff)))
            .AddPolicy(AuthPolicies.SalonManager, policy => policy.RequireRole(nameof(SalonRole.Owner), nameof(SalonRole.Manager)))
            .AddPolicy(AuthPolicies.SalonOwner, policy => policy.RequireRole(nameof(SalonRole.Owner)))
            .AddPolicy(AuthPolicies.PlatformAdmin, policy => policy.RequireRole(KaloraClaimsPrincipalFactory.PlatformAdminRole));

        services.AddAntiforgery(options =>
        {
            options.HeaderName = AntiforgeryHeader;
            options.Cookie.Name = "kalora.af";
            options.Cookie.HttpOnly = true;
            // "Always" makes the antiforgery service throw on plain HTTP (local development). Behind the reverse proxy the
            // request counts as HTTPS through the forwarded headers, so the cookie is Secure in production.
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

        return services;
    }
}
