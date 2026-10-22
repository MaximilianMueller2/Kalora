using Kalora.Application.Common;

namespace Kalora.Application.Salons;

/// <summary>Weekly opening hours and closed days of the signed-in user's salon.</summary>
public interface IOpeningHoursService
{
    Task<Result<OpeningHoursPlan>> GetPlanAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces the whole week; weekdays that are not listed become closed days.</summary>
    Task<Result<OpeningHoursPlan>> ReplacePlanAsync(OpeningHoursPlan plan, CancellationToken cancellationToken = default);

    /// <summary>Upcoming closed days (today and later, in the salon's time zone).</summary>
    Task<Result<IReadOnlyList<Closure>>> ListClosuresAsync(CancellationToken cancellationToken = default);

    /// <summary>Closes one day or a range of days; days that are already closed are skipped.</summary>
    Task<Result<IReadOnlyList<Closure>>> CreateClosuresAsync(CreateClosureRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteClosureAsync(Guid id, CancellationToken cancellationToken = default);
}
