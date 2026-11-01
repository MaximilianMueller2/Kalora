using Kalora.Application.Common;

namespace Kalora.Application.Dashboard;

/// <summary>
/// The start page of the salon area. It is made from what the calendar and the inbox already know (one read of the week,
/// one of the open requests), restricted to what the signed-in person may see: owners and managers the whole salon, staff their own column.
/// </summary>
public interface IDashboardService
{
    Task<Result<DashboardDto>> GetAsync(CancellationToken cancellationToken = default);
}
