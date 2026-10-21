using System.Text.Json;
using Kalora.Domain.Messaging;

namespace Kalora.Application.Common;

internal sealed class EmailOutboxWriter(IAppDbContext db, TimeProvider timeProvider) : IEmailOutbox
{
    public void Enqueue(Guid salonId, string toEmail, string type, string language, object payload) =>
        db.EmailOutbox.Add(new EmailOutbox
        {
            SalonId = salonId,
            ToEmail = toEmail,
            Type = type,
            Language = language,
            PayloadJson = JsonSerializer.Serialize(payload),
            Status = EmailStatus.Pending,
            NextAttemptAtUtc = timeProvider.GetUtcNow(),
        });
}
