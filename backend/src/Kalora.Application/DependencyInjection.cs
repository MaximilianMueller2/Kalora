using Kalora.Application.Configuration;
using Kalora.Application.Ping;
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

        services.AddScoped<IPingService, PingService>();
        return services;
    }
}
