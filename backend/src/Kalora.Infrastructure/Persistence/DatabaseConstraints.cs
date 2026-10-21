namespace Kalora.Infrastructure.Persistence;

public static class DatabaseConstraints
{
    /// <summary>Exclusion constraint: no two active appointments of one staff member may overlap.</summary>
    public const string AppointmentNoOverlap = "ex_appointments_no_overlap";
}
