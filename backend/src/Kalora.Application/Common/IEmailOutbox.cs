namespace Kalora.Application.Common;

public interface IEmailOutbox
{
    /// <summary>
    /// Queues a mail; nothing is sent here. The entry is saved with the caller's next <c>SaveChangesAsync</c>, so it
    /// commits or rolls back together with the change that caused it.
    /// </summary>
    void Enqueue(Guid salonId, string toEmail, string type, string language, object payload);
}
