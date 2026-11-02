using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalora.Application.Subscriptions;

internal sealed class SubscriptionGuard(IAppDbContext db, IOptions<PlansOptions> plans, TimeProvider timeProvider) : ISubscriptionGuard
{
    public async Task<Result> EnsureWritableAsync(CancellationToken cancellationToken = default)
    {
        var salon = await db.Salons.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return salon is null || salon.AccessAt(timeProvider.GetUtcNow()) == Domain.Salons.SalonAccess.Full
            ? Result.Success()
            : Error.Forbidden("subscription.read_only");
    }

    public async Task<Result> EnsureCanAddStaffAsync(int activating = 1, CancellationToken cancellationToken = default)
    {
        var salon = await db.Salons.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (salon is null)
        {
            return Result.Success();
        }

        if (salon.AccessAt(timeProvider.GetUtcNow()) != Domain.Salons.SalonAccess.Full)
        {
            return Error.Forbidden("subscription.read_only");
        }

        var active = await db.StaffMembers.CountAsync(s => s.IsActive, cancellationToken);
        return active + activating > plans.Value.MaxStaff(salon) ? Error.Conflict("subscription.staff_limit") : Result.Success();
    }
}
