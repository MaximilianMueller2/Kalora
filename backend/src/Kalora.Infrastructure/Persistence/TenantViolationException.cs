namespace Kalora.Infrastructure.Persistence;

/// <summary>Thrown when a save would write data into, or move data between, salons. Always a bug in calling code.</summary>
public sealed class TenantViolationException(string message) : InvalidOperationException(message);
