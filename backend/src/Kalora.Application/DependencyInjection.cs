using FluentValidation;
using Kalora.Application.Auth;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Application.Ping;
using Kalora.Application.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Kalora.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddOptions<AppOptions>()
            .BindConfiguration(AppOptions.SectionName)
            .ValidateOnStart();
        services.TryAddSingleton<IValidateOptions<AppOptions>, AppOptionsValidator>();

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextInitializer>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<ISalonResolver, SalonResolver>();

        services.AddSingleton<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddSingleton<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddSingleton<IValidator<ForgotPasswordRequest>, ForgotPasswordRequestValidator>();
        services.AddSingleton<IValidator<ResetPasswordRequest>, ResetPasswordRequestValidator>();
        services.AddSingleton<IValidator<CreateInvitationRequest>, CreateInvitationRequestValidator>();
        services.AddSingleton<IValidator<AcceptInvitationRequest>, AcceptInvitationRequestValidator>();

        services.AddScoped<IEmailOutbox, EmailOutboxWriter>();
        services.AddScoped<MeResponseFactory>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITwoFactorService, TwoFactorService>();
        services.AddScoped<IInvitationService, InvitationService>();

        services.AddScoped<IPingService, PingService>();
        return services;
    }
}
