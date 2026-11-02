using Kalora.Application.Identity;
using Microsoft.AspNetCore.Identity;

namespace Kalora.Api.Auth;

/// <summary>
/// The platform admin is an ordinary account with a flag. There is no screen to hand it out: whoever runs the server lists the
/// address in <c>PlatformAdmin:Emails</c> and restarts. Only an existing account with a confirmed address is flagged, so nobody can
/// claim the role by registering the address first and never confirming it. Entries are only ever added, never removed;
/// to take the role away, change the account in the database.
/// </summary>
internal sealed partial class PlatformAdminBootstrap(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<PlatformAdminBootstrap> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var emails = configuration.GetSection("PlatformAdmin:Emails").Get<string[]>() ?? [];
        if (emails.Length == 0)
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        foreach (var email in emails.Where(e => !string.IsNullOrWhiteSpace(e)))
        {
            var user = await users.FindByEmailAsync(email.Trim());
            if (user is null || !user.EmailConfirmed)
            {
                LogSkipped(logger);
                continue;
            }

            if (user.IsPlatformAdmin)
            {
                continue;
            }

            user.IsPlatformAdmin = true;
            await users.UpdateAsync(user);
            await users.UpdateSecurityStampAsync(user);
            LogGranted(logger, user.Id);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "PlatformAdmin:Emails names an address without a confirmed account; it was skipped")]
    private static partial void LogSkipped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} is now a platform admin")]
    private static partial void LogGranted(ILogger logger, Guid userId);
}
