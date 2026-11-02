namespace Kalora.Api.Http;

/// <summary>
/// Marks endpoints that still work for a salon that may only look at its data (suspended, cancelled, trial over): signing in and out,
/// the own profile, and the data-protection rights of customers. Everything else that writes is refused by <see cref="ReadOnlySalonFilter"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class AllowWhenReadOnlyAttribute : Attribute;
