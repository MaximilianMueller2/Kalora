using Kalora.Application.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kalora.Infrastructure.Persistence;

internal sealed class SlotConflictDetector : ISlotConflictDetector
{
    private const string ExclusionViolation = PostgresErrorCodes.ExclusionViolation;
    private const string UniqueViolation = PostgresErrorCodes.UniqueViolation;
    private const string DeadlockDetected = PostgresErrorCodes.DeadlockDetected;
    private const string SerializationFailure = PostgresErrorCodes.SerializationFailure;

    public SaveConflict Classify(Exception exception)
    {
        if (exception is not DbUpdateException { InnerException: PostgresException postgres })
        {
            return SaveConflict.None;
        }

        return postgres switch
        {
            { SqlState: ExclusionViolation, ConstraintName: DatabaseConstraints.AppointmentNoOverlap } => SaveConflict.SlotTaken,
            { SqlState: UniqueViolation } => SaveConflict.DuplicateKey,
            { SqlState: DeadlockDetected or SerializationFailure } => SaveConflict.Transient,
            _ => SaveConflict.None,
        };
    }
}
