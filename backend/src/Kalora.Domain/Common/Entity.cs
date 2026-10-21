namespace Kalora.Domain.Common;

public abstract class Entity
{
    /// <summary>Time-ordered GUID (v7), so IDs neither reveal counts nor fragment indexes.</summary>
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
