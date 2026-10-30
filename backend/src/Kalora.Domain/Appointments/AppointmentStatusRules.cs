namespace Kalora.Domain.Appointments;

/// <summary>
/// The life of an appointment. An open request is answered (confirmed or declined), runs out (expired) or is withdrawn
/// (cancelled); a confirmed appointment is cancelled or, once it took place, marked as attended or missed. The other
/// states are final.
/// </summary>
public static class AppointmentStatusRules
{
    private static readonly IReadOnlyDictionary<AppointmentStatus, AppointmentStatus[]> Allowed = new Dictionary<AppointmentStatus, AppointmentStatus[]>
    {
        [AppointmentStatus.Requested] = [AppointmentStatus.Confirmed, AppointmentStatus.Declined, AppointmentStatus.Expired, AppointmentStatus.Cancelled],
        [AppointmentStatus.Confirmed] = [AppointmentStatus.Cancelled, AppointmentStatus.Completed, AppointmentStatus.NoShow],
        [AppointmentStatus.Declined] = [],
        [AppointmentStatus.Expired] = [],
        [AppointmentStatus.Cancelled] = [],
        [AppointmentStatus.Completed] = [],
        [AppointmentStatus.NoShow] = [],
    };

    public static bool CanMove(AppointmentStatus from, AppointmentStatus to) => Allowed[from].Contains(to);

    public static IReadOnlyList<AppointmentStatus> NextStates(AppointmentStatus from) => Allowed[from];

    /// <summary>Requested and confirmed appointments hold their time; every other status gives it back.</summary>
    public static bool BlocksTime(AppointmentStatus status) => status is AppointmentStatus.Requested or AppointmentStatus.Confirmed;
}
