using FluentValidation;
using Kalora.Application.Common;
using Kalora.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Kalora.Application.Catalog;

internal sealed class ServiceCatalogService(
    IAppDbContext db,
    IValidator<SaveCategoryRequest> categoryValidator,
    IValidator<SaveServiceRequest> serviceValidator,
    IValidator<ApplyTemplateRequest> templateValidator) : IServiceCatalogService
{
    private const string Currency = "EUR";

    public async Task<Result<CatalogDto>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        await BuildCatalogAsync(cancellationToken);

    public async Task<Result<CatalogDto>> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var validation = categoryValidator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var last = await db.ServiceCategories.MaxAsync(c => (int?)c.SortOrder, cancellationToken);
        db.ServiceCategories.Add(new ServiceCategory { Name = request.Name.Trim(), SortOrder = (last ?? -1) + 1 });
        await db.SaveChangesAsync(cancellationToken);
        return await BuildCatalogAsync(cancellationToken);
    }

    public async Task<Result<CatalogDto>> RenameCategoryAsync(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var validation = categoryValidator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var category = await db.ServiceCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return CategoryNotFound;
        }

        category.Name = request.Name.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return await BuildCatalogAsync(cancellationToken);
    }

    public async Task<Result<CatalogDto>> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await db.ServiceCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return CategoryNotFound;
        }

        if (await db.Services.AnyAsync(s => s.CategoryId == id, cancellationToken))
        {
            return Error.Conflict("category.not_empty");
        }

        db.ServiceCategories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
        return await BuildCatalogAsync(cancellationToken);
    }

    public async Task<Result<CatalogDto>> ReorderCategoriesAsync(ReorderRequest request, CancellationToken cancellationToken = default)
    {
        var categories = await db.ServiceCategories.ToListAsync(cancellationToken);
        if (!IsSameSet(request.Ids, categories.Select(c => c.Id)))
        {
            return OrderMismatch;
        }

        AssignOrder(request.Ids, categories, (category, order) => category.SortOrder = order, category => category.Id);
        await db.SaveChangesAsync(cancellationToken);
        return await BuildCatalogAsync(cancellationToken);
    }

    public async Task<Result<CatalogDto>> CreateServiceAsync(SaveServiceRequest request, CancellationToken cancellationToken = default)
    {
        var validation = serviceValidator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        // The category lookup goes through the tenant filter: a category of another salon is "not found", not just invalid.
        if (!await db.ServiceCategories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken))
        {
            return CategoryNotFound;
        }

        var service = new Service { SortOrder = await NextServiceOrderAsync(request.CategoryId, cancellationToken) };
        Apply(service, request);
        db.Services.Add(service);
        await db.SaveChangesAsync(cancellationToken);
        return await BuildCatalogAsync(cancellationToken);
    }

    public async Task<Result<CatalogDto>> UpdateServiceAsync(Guid id, SaveServiceRequest request, CancellationToken cancellationToken = default)
    {
        var validation = serviceValidator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (service is null)
        {
            return ServiceNotFound;
        }

        if (request.CategoryId != service.CategoryId)
        {
            if (!await db.ServiceCategories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken))
            {
                return CategoryNotFound;
            }

            service.SortOrder = await NextServiceOrderAsync(request.CategoryId, cancellationToken);
        }

        Apply(service, request);
        await db.SaveChangesAsync(cancellationToken);
        return await BuildCatalogAsync(cancellationToken);
    }

    public async Task<Result<CatalogDto>> DeleteServiceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (service is null)
        {
            return ServiceNotFound;
        }

        if (await db.Appointments.AnyAsync(a => a.ServiceId == id, cancellationToken))
        {
            return Error.Conflict("service.has_appointments");
        }

        db.Services.Remove(service);
        await db.SaveChangesAsync(cancellationToken);
        return await BuildCatalogAsync(cancellationToken);
    }

    public async Task<Result<CatalogDto>> ReorderServicesAsync(Guid categoryId, ReorderRequest request, CancellationToken cancellationToken = default)
    {
        if (!await db.ServiceCategories.AnyAsync(c => c.Id == categoryId, cancellationToken))
        {
            return CategoryNotFound;
        }

        var services = await db.Services.Where(s => s.CategoryId == categoryId).ToListAsync(cancellationToken);
        if (!IsSameSet(request.Ids, services.Select(s => s.Id)))
        {
            return OrderMismatch;
        }

        AssignOrder(request.Ids, services, (service, order) => service.SortOrder = order, service => service.Id);
        await db.SaveChangesAsync(cancellationToken);
        return await BuildCatalogAsync(cancellationToken);
    }

    public async Task<Result<CatalogDto>> ApplyTemplateAsync(ApplyTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var validation = templateValidator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var salon = await db.Salons.FirstOrDefaultAsync(cancellationToken);
        if (salon is null)
        {
            return Error.NotFound("salon.not_found");
        }

        var nextCategoryOrder = ((await db.ServiceCategories.MaxAsync(c => (int?)c.SortOrder, cancellationToken)) ?? -1) + 1;
        foreach (var template in ServiceTemplates.For(request.Industry ?? salon.Industry))
        {
            var category = new ServiceCategory
            {
                Name = ServiceTemplates.Localize(template.Name, salon.DefaultLanguage),
                SortOrder = nextCategoryOrder++,
            };
            db.ServiceCategories.Add(category);

            var order = 0;
            foreach (var item in template.Services)
            {
                db.Services.Add(new Service
                {
                    CategoryId = category.Id,
                    Name = ServiceTemplates.Localize(item.Name, salon.DefaultLanguage),
                    PriceCents = item.PriceCents,
                    PriceIsFrom = item.PriceIsFrom,
                    Currency = Currency,
                    DurationMinutes = item.DurationMinutes,
                    BufferBeforeMinutes = item.BufferBeforeMinutes,
                    BufferAfterMinutes = item.BufferAfterMinutes,
                    SortOrder = order++,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return await BuildCatalogAsync(cancellationToken);
    }

    private static Error CategoryNotFound => Error.NotFound("category.not_found");

    private static Error ServiceNotFound => Error.NotFound("service.not_found");

    private static Error OrderMismatch => Error.Validation("catalog.order_mismatch", "ids");

    private async Task<int> NextServiceOrderAsync(Guid categoryId, CancellationToken cancellationToken) =>
        ((await db.Services.Where(s => s.CategoryId == categoryId).MaxAsync(s => (int?)s.SortOrder, cancellationToken)) ?? -1) + 1;

    private static void Apply(Service service, SaveServiceRequest request)
    {
        service.CategoryId = request.CategoryId;
        service.Name = request.Name.Trim();
        service.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        service.PriceCents = request.PriceCents;
        service.PriceIsFrom = request.PriceIsFrom;
        service.Currency = Currency;
        service.DurationMinutes = request.DurationMinutes;
        service.BufferBeforeMinutes = request.BufferBeforeMinutes;
        service.BufferAfterMinutes = request.BufferAfterMinutes;
        service.IsBookableOnline = request.IsBookableOnline;
        service.IsActive = request.IsActive;
    }

    /// <summary>A reorder must name every item exactly once, so a stale list from another tab cannot drop or duplicate entries.</summary>
    private static bool IsSameSet(IReadOnlyList<Guid> requested, IEnumerable<Guid> existing)
    {
        var current = existing.ToHashSet();
        return requested.Count == current.Count && requested.ToHashSet().SetEquals(current);
    }

    private static void AssignOrder<T>(IReadOnlyList<Guid> ids, List<T> items, Action<T, int> setOrder, Func<T, Guid> idOf)
    {
        var byId = items.ToDictionary(idOf);
        for (var index = 0; index < ids.Count; index++)
        {
            setOrder(byId[ids[index]], index);
        }
    }

    private async Task<CatalogDto> BuildCatalogAsync(CancellationToken cancellationToken)
    {
        var categories = await db.ServiceCategories.OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAtUtc).ToListAsync(cancellationToken);
        var services = await db.Services
            .OrderBy(s => s.SortOrder).ThenBy(s => s.CreatedAtUtc)
            .Select(s => new { Service = s, HasAppointments = db.Appointments.Any(a => a.ServiceId == s.Id) })
            .ToListAsync(cancellationToken);

        var byCategory = services.ToLookup(item => item.Service.CategoryId);
        return new CatalogDto(categories
            .Select(c => new CategoryDto(c.Id, c.Name, c.SortOrder, byCategory[c.Id].Select(item => ToDto(item.Service, item.HasAppointments)).ToList()))
            .ToList());
    }

    private static ServiceDto ToDto(Service s, bool hasAppointments) => new(
        s.Id, s.CategoryId, s.Name, s.Description, s.PriceCents, s.PriceIsFrom, s.Currency, s.DurationMinutes,
        s.BufferBeforeMinutes, s.BufferAfterMinutes, s.IsBookableOnline, s.IsActive, s.SortOrder, hasAppointments);
}
