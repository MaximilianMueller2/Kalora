namespace Kalora.Domain.Messaging;

public enum EmailStatus
{
    Pending,

    /// <summary>Taken by the sender. A mail stuck here (the process died) is put back to <see cref="Pending"/> after a while.</summary>
    Sending,

    Sent,
    /// <summary>Gave up: a permanent error or too many attempts.</summary>
    Failed,
}
