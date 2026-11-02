using Kalora.Application.Common;

namespace Kalora.Application.Subscriptions;

/// <summary>Checks, for the salon of the current request, what its plan and status allow.</summary>
public interface ISubscriptionGuard
{
    /// <summary>Fails with <c>subscription.read_only</c> if the salon may only look at its data (suspended, cancelled, trial over).</summary>
    Task<Result> EnsureWritableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Also fails with <c>subscription.staff_limit</c> if one more active staff member would exceed the plan.
    /// <paramref name="activating"/> staff members are the ones about to become active (1 for a new one, 1 for a reactivation).
    /// </summary>
    Task<Result> EnsureCanAddStaffAsync(int activating = 1, CancellationToken cancellationToken = default);
}
