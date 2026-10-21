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

        services.AddScoped<IPingService, PingService>();
        return services;
    }
}
