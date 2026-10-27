namespace Kalora.Domain.Scheduling;

public static class StaffPicker
{
    /// <summary>
    /// Fewest appointments that day wins, so the load is spread; on a tie the earlier calendar column wins.
    /// The staff ID is the last tie-breaker so the result never depends on the order of the list.
    /// </summary>
    public static FreeStaff Pick(IEnumerable<FreeStaff> free) =>
        free.OrderBy(s => s.AppointmentsThatDay).ThenBy(s => s.SortOrder).ThenBy(s => s.StaffId).First();
}
