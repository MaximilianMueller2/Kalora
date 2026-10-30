using Kalora.Application.Common;
using Kalora.Application.Identity;
using Kalora.Domain.Appointments;
using Kalora.Domain.Salons;
using Kalora.Domain.Staff;

namespace Kalora.Application.Appointments;

/// <summary>
/// Which appointments and calendar columns the signed-in person may see and change: owners and managers all of them,
/// everyone else only those of their own staff profile.
/// </summary>
internal sealed class AppointmentScope(IAppDbContext db, ICurrentUser currentUser)
{
    public bool SeesAll => currentUser.SalonRole is SalonRole.Owner or SalonRole.Manager;

    public Guid? UserId => currentUser.UserId;

    public IQueryable<Appointment> Appointments()
    {
        if (SeesAll)
        {
            return db.Appointments;
        }

        var userId = currentUser.UserId;
        return db.Appointments.Where(a => db.StaffMembers.Any(s => s.Id == a.StaffId && s.UserId == userId));
    }

    public IQueryable<StaffMember> StaffMembers()
    {
        if (SeesAll)
        {
            return db.StaffMembers;
        }

        var userId = currentUser.UserId;
        return db.StaffMembers.Where(s => s.UserId == userId);
    }
}
