namespace Kalora.Application.Common;

public enum SaveConflict
{
    /// <summary>The exception is something else and must not be swallowed.</summary>
    None,

    /// <summary>Another appointment of the same person overlaps (the database's exclusion constraint).</summary>
    SlotTaken,

    /// <summary>
    /// The database aborted this transaction to resolve a clash with a concurrent one (deadlock, serialization failure).
    /// Concurrent inserts into the same exclusion range can do this; trying again gives a clean answer.
    /// </summary>
    Transient,

    /// <summary>A unique index was violated, e.g. a customer with the same e-mail was created in the meantime.</summary>
    DuplicateKey,
}

/// <summary>Recognizes database conflicts without Application knowing the database driver.</summary>
public interface ISlotConflictDetector
{
    SaveConflict Classify(Exception exception);
}
