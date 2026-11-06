using System.Text.Json;
using Kalora.Application.Appointments;
using Kalora.Application.Common;
using Kalora.Application.Dashboard;
using Kalora.Application.Identity;
using Kalora.Domain.Appointments;
using Kalora.Domain.Auditing;
using Kalora.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Application.Customers;

internal sealed class CustomerService(
    IAppDbContext db,
    AppointmentScope scope,
    ICurrentUser currentUser,
    CustomerAnonymizer anonymizer,
    TimeProvider timeProvider) : ICustomerService
{
    private const int PageSize = 50;
    private const int MinSearchLength = 2;
    private const int MaxSearchLength = 100;

    public async Task<Result<CustomerListDto>> ListAsync(string? search, CancellationToken cancellationToken = default)
    {
        if (!scope.SeesAll)
        {
            return Error.Forbidden();
        }

        if (search is { Length: > MaxSearchLength })
        {
            return Error.Validation("validation.too_long", "q");
        }

        var customers = db.Customers.Where(c => c.AnonymizedAtUtc == null);
        var term = search?.Trim().ToLowerInvariant();
        if (term is { Length: >= MinSearchLength })
        {
            customers = customers.Where(c => c.Name.ToLower().Contains(term) || (c.Email != null && c.Email.Contains(term)) || (c.Phone != null && c.Phone.Contains(term)));
        }

        var rows = await customers
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Select(c => new CustomerListItemDto(
                c.Id,
                c.Name,
                c.Email,
                c.Phone,
                c.LastVisitAtUtc,
                db.Appointments.Count(a => a.CustomerId == c.Id),
                c.NoShowCount))
            .Take(PageSize + 1)
            .ToListAsync(cancellationToken);
        return new CustomerListDto(rows.Take(PageSize).ToList(), rows.Count > PageSize);
    }

    public async Task<Result<CustomerDetailDto>> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        scope.SeesAll ? await LoadAsync(id, cancellationToken) : Error.Forbidden();

    public async Task<Result<BinaryFile>> ExportJsonAsync(Guid id, CancellationToken cancellationToken = default) =>
        await ExportAsync(id, "json", "application/json", (customer, now) => CustomerExport.Json(customer, now), cancellationToken);

    public async Task<Result<BinaryFile>> ExportCsvAsync(Guid id, CancellationToken cancellationToken = default) =>
        await ExportAsync(id, "csv", "text/csv; charset=utf-8", (customer, _) => CustomerExport.Csv(customer), cancellationToken);

    public async Task<Result> AnonymizeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!scope.SeesAll)
        {
            return Error.Forbidden();
        }

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null)
        {
            return Error.NotFound("customer.not_found");
        }

        if (customer.AnonymizedAtUtc is not null)
        {
            return Error.Conflict("customer.already_anonymized");
        }

        var now = timeProvider.GetUtcNow();
        if (await db.Appointments.AnyAsync(
                a => a.CustomerId == id && (a.Status == AppointmentStatus.Requested || a.Status == AppointmentStatus.Confirmed) && a.EndUtc > now, cancellationToken))
        {
            return Error.Conflict("customer.has_open_appointments");
        }

        await anonymizer.AnonymizeAsync(customer, now, cancellationToken);
        AddAudit("customer.anonymized", id, new { reason = "salon" });
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<BinaryFile>> ExportAsync(
        Guid id, string extension, string contentType, Func<CustomerDetailDto, DateTimeOffset, byte[]> render, CancellationToken cancellationToken)
    {
        if (!scope.SeesAll)
        {
            return Error.Forbidden();
        }

        var customer = await LoadAsync(id, cancellationToken);
        if (!customer.IsSuccess)
        {
            return customer.Error;
        }

        var now = timeProvider.GetUtcNow();
        AddAudit("customer.exported", id, new { format = extension });
        await db.SaveChangesAsync(cancellationToken);
        return new BinaryFile($"customer-{id:N}.{extension}", contentType, render(customer.Value, now));
    }

    private async Task<Result<CustomerDetailDto>> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null)
        {
            return Error.NotFound("customer.not_found");
        }

        var appointments = await (
            from a in db.Appointments.Where(a => a.CustomerId == id)
            join s in db.StaffMembers on a.StaffId equals s.Id into staff
            from s in staff.DefaultIfEmpty()
            orderby a.StartUtc descending
            select new CustomerAppointmentDto(
                a.Id, a.Status, a.StartUtc, a.EndUtc, a.ServiceName, s.DisplayName, a.PriceCents, a.PriceIsFrom, a.Currency, a.CustomerNote, a.InternalNote))
            .ToListAsync(cancellationToken);
        return new CustomerDetailDto(
            customer.Id, customer.Name, customer.Email, customer.Phone, customer.PreferredLanguage, customer.CreatedAtUtc,
            customer.LastVisitAtUtc, customer.NoShowCount, customer.MarketingConsentAtUtc, customer.AnonymizedAtUtc is not null, appointments);
    }

    private void AddAudit(string action, Guid customerId, object details) =>
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = currentUser.UserId,
            Action = action,
            EntityType = nameof(Customer),
            EntityId = customerId,
            Details = JsonSerializer.Serialize(details),
        });
}
