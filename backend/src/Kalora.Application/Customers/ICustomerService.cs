using Kalora.Application.Common;
using Kalora.Application.Dashboard;

namespace Kalora.Application.Customers;

/// <summary>
/// The salon's customer file and the data subject's rights: look up, export (JSON or CSV) and delete (anonymize). Only for
/// owners and managers. Exporting and deleting are written to the audit log.
/// </summary>
public interface ICustomerService
{
    /// <summary>Customers whose name, e-mail or phone contains the text, by name; anonymized ones are not listed.</summary>
    Task<Result<CustomerListDto>> ListAsync(string? search, CancellationToken cancellationToken = default);

    Task<Result<CustomerDetailDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<BinaryFile>> ExportJsonAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<BinaryFile>> ExportCsvAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes name, e-mail and phone; the appointments stay without any link to a person (notes are cleared, links invalid, queued
    /// and sent mails about them emptied). Errors: <c>customer.has_open_appointments</c> (cancel or finish them first),
    /// <c>customer.already_anonymized</c>.
    /// </summary>
    Task<Result> AnonymizeAsync(Guid id, CancellationToken cancellationToken = default);
}
