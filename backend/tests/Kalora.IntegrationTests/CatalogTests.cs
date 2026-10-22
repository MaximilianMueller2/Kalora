using System.Net;
using System.Net.Http.Json;
using Kalora.Application.Catalog;
using Kalora.Domain.Salons;

namespace Kalora.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class CatalogTests(PostgresFixture postgres)
{
    private const string Catalog = "/api/catalog";

    private static SaveServiceRequest NewService(Guid categoryId, string name = "Haarschnitt") =>
        new(categoryId, name, null, 3000, false, 30, 0, 5, true, true);

    private static async Task<CatalogDto> ReadAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CatalogDto>(TestJson.Options))!;
    }

    private static async Task<CategoryDto> AddCategoryAsync(ApiSession session, string name)
    {
        var catalog = await ReadAsync(await session.PostAsync($"{Catalog}/categories", new SaveCategoryRequest(name)));
        return catalog.Categories.Single(c => c.Name == name.Trim());
    }

    private static async Task<ServiceDto> AddServiceAsync(ApiSession session, SaveServiceRequest request)
    {
        var catalog = await ReadAsync(await session.PostAsync($"{Catalog}/services", request));
        return catalog.Categories.SelectMany(c => c.Services).Single(s => s.Name == request.Name);
    }

    [Fact]
    public async Task A_new_salon_has_an_empty_catalog()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");

        Assert.Empty((await owner.GetJsonAsync<CatalogDto>(Catalog)).Categories);
    }

    [Fact]
    public async Task Categories_and_services_can_be_created_in_display_order()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");

        var women = await AddCategoryAsync(owner, "  Damen ");
        var men = await AddCategoryAsync(owner, "Herren");
        await AddServiceAsync(owner, NewService(women.Id, "Färben"));
        await AddServiceAsync(owner, NewService(women.Id, "Schneiden"));
        var catalog = await owner.GetJsonAsync<CatalogDto>(Catalog);

        Assert.Equal(["Damen", "Herren"], catalog.Categories.Select(c => c.Name));
        Assert.Equal(["Färben", "Schneiden"], catalog.Categories[0].Services.Select(s => s.Name));
        Assert.Equal([0, 1], catalog.Categories[0].Services.Select(s => s.SortOrder));
        Assert.Empty(catalog.Categories[1].Services);
        var service = catalog.Categories[0].Services[0];
        Assert.Equal((3000, false, "EUR", 30, 0, 5, true, true, false), (service.PriceCents, service.PriceIsFrom, service.Currency,
            service.DurationMinutes, service.BufferBeforeMinutes, service.BufferAfterMinutes, service.IsBookableOnline, service.IsActive, service.HasAppointments));
        _ = men;
    }

    [Fact]
    public async Task A_service_can_be_updated_moved_to_another_category_and_deactivated()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var women = await AddCategoryAsync(owner, "Damen");
        var men = await AddCategoryAsync(owner, "Herren");
        await AddServiceAsync(owner, NewService(men.Id, "Bart"));
        var cut = await AddServiceAsync(owner, NewService(women.Id));

        var catalog = await ReadAsync(await owner.PutAsync($"{Catalog}/services/{cut.Id}",
            new SaveServiceRequest(men.Id, "Haarschnitt Herren", "Mit Waschen", 2800, true, 45, 5, 10, false, false)));

        var moved = catalog.Categories.Single(c => c.Id == men.Id).Services;
        Assert.Equal(["Bart", "Haarschnitt Herren"], moved.Select(s => s.Name));
        var updated = moved[1];
        Assert.Equal(("Mit Waschen", 2800, true, 45, 5, 10, false, false), (updated.Description, updated.PriceCents, updated.PriceIsFrom,
            updated.DurationMinutes, updated.BufferBeforeMinutes, updated.BufferAfterMinutes, updated.IsBookableOnline, updated.IsActive));
        Assert.Empty(catalog.Categories.Single(c => c.Id == women.Id).Services);
    }

    [Theory]
    [InlineData("", 3000, 30, 0, 0, "validation.required")]
    [InlineData("X", -1, 30, 0, 0, "service.price_invalid")]
    [InlineData("X", 1_000_001, 30, 0, 0, "service.price_invalid")]
    [InlineData("X", 3000, 0, 0, 0, "service.duration_invalid")]
    [InlineData("X", 3000, 32, 0, 0, "service.duration_invalid")]
    [InlineData("X", 3000, 725, 0, 0, "service.duration_invalid")]
    [InlineData("X", 3000, 30, 3, 0, "service.buffer_invalid")]
    [InlineData("X", 3000, 30, 0, -5, "service.buffer_invalid")]
    [InlineData("X", 3000, 30, 0, 245, "service.buffer_invalid")]
    public async Task Invalid_services_are_rejected_with_a_translatable_code(string name, int price, int duration, int before, int after, string code)
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var category = await AddCategoryAsync(owner, "Damen");

        var response = await owner.PostAsync($"{Catalog}/services", new SaveServiceRequest(category.Id, name, null, price, false, duration, before, after, true, true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Category_names_are_validated_and_can_be_changed()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var category = await AddCategoryAsync(owner, "Damen");

        var blank = await owner.PostAsync($"{Catalog}/categories", new SaveCategoryRequest("  "));
        var tooLong = await owner.PutAsync($"{Catalog}/categories/{category.Id}", new SaveCategoryRequest(new string('x', 101)));
        var renamed = await ReadAsync(await owner.PutAsync($"{Catalog}/categories/{category.Id}", new SaveCategoryRequest(" Frauen ")));

        Assert.Equal("validation.required", await blank.ProblemCodeAsync());
        Assert.Equal("validation.too_long", await tooLong.ProblemCodeAsync());
        Assert.Equal("Frauen", renamed.Categories.Single().Name);
    }

    [Fact]
    public async Task A_service_without_appointments_can_be_deleted_but_one_with_appointments_only_deactivated()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");
        var category = await AddCategoryAsync(owner, "Damen");
        var free = await AddServiceAsync(owner, NewService(category.Id, "Frei"));
        var booked = await AddServiceAsync(owner, NewService(category.Id, "Gebucht"));
        await host.AddAppointmentAsync(me.Salon!.Id, booked.Id);

        var deleted = await ReadAsync(await owner.DeleteAsync($"{Catalog}/services/{free.Id}"));
        var refused = await owner.DeleteAsync($"{Catalog}/services/{booked.Id}");
        var deactivated = await ReadAsync(await owner.PutAsync($"{Catalog}/services/{booked.Id}", NewService(category.Id, "Gebucht") with { IsActive = false }));

        Assert.Equal(["Gebucht"], deleted.Categories.Single().Services.Select(s => s.Name));
        Assert.True(deleted.Categories.Single().Services.Single().HasAppointments);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("service.has_appointments", await refused.ProblemCodeAsync());
        Assert.False(deactivated.Categories.Single().Services.Single().IsActive);
    }

    [Fact]
    public async Task Only_empty_categories_can_be_deleted()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var full = await AddCategoryAsync(owner, "Voll");
        var empty = await AddCategoryAsync(owner, "Leer");
        await AddServiceAsync(owner, NewService(full.Id));

        var refused = await owner.DeleteAsync($"{Catalog}/categories/{full.Id}");
        var removed = await ReadAsync(await owner.DeleteAsync($"{Catalog}/categories/{empty.Id}"));

        Assert.Equal("category.not_empty", await refused.ProblemCodeAsync());
        Assert.Equal(["Voll"], removed.Categories.Select(c => c.Name));
    }

    [Fact]
    public async Task Categories_and_services_can_be_reordered_and_incomplete_lists_are_refused()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        var a = await AddCategoryAsync(owner, "A");
        var b = await AddCategoryAsync(owner, "B");
        var one = await AddServiceAsync(owner, NewService(a.Id, "Eins"));
        var two = await AddServiceAsync(owner, NewService(a.Id, "Zwei"));
        var three = await AddServiceAsync(owner, NewService(a.Id, "Drei"));

        var categories = await ReadAsync(await owner.PutAsync($"{Catalog}/categories/order", new ReorderRequest([b.Id, a.Id])));
        var services = await ReadAsync(await owner.PutAsync($"{Catalog}/categories/{a.Id}/services/order", new ReorderRequest([three.Id, one.Id, two.Id])));
        var missing = await owner.PutAsync($"{Catalog}/categories/order", new ReorderRequest([a.Id]));
        var duplicate = await owner.PutAsync($"{Catalog}/categories/{a.Id}/services/order", new ReorderRequest([one.Id, one.Id, two.Id]));
        var foreignItem = await owner.PutAsync($"{Catalog}/categories/{a.Id}/services/order", new ReorderRequest([one.Id, two.Id, Guid.NewGuid()]));

        Assert.Equal(["B", "A"], categories.Categories.Select(c => c.Name));
        Assert.Equal(["Drei", "Eins", "Zwei"], services.Categories.Single(c => c.Id == a.Id).Services.Select(s => s.Name));
        Assert.Equal("catalog.order_mismatch", await missing.ProblemCodeAsync());
        Assert.Equal("catalog.order_mismatch", await duplicate.ProblemCodeAsync());
        Assert.Equal("catalog.order_mismatch", await foreignItem.ProblemCodeAsync());
    }

    [Fact]
    public async Task Everyone_in_the_salon_can_read_the_catalog_but_only_managers_change_it()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, me) = await host.RegisterOwnerAsync("maria@example.com");
        var category = await AddCategoryAsync(owner, "Damen");
        var staff = await host.InviteAndAcceptAsync(owner, me.Salon!.Id, "Tom", "tom@example.com", SalonRole.Staff);
        var manager = await host.InviteAndAcceptAsync(owner, me.Salon.Id, "Mia", "mia@example.com", SalonRole.Manager);

        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync(Catalog)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsync($"{Catalog}/categories", new SaveCategoryRequest("X"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsync($"{Catalog}/services", NewService(category.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.DeleteAsync($"{Catalog}/categories/{category.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsync($"{Catalog}/templates/apply", new ApplyTemplateRequest(null))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsync($"{Catalog}/categories", new SaveCategoryRequest("Von Manager"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.NewSession().GetAsync(Catalog)).StatusCode);
    }

    [Fact]
    public async Task One_salon_cannot_see_or_change_the_catalog_of_another()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (ownerA, _) = await host.RegisterOwnerAsync("a@example.com", "Salon A");
        var (ownerB, _) = await host.RegisterOwnerAsync("b@example.com", "Salon B");
        var categoryA = await AddCategoryAsync(ownerA, "Nur A");
        var serviceA = await AddServiceAsync(ownerA, NewService(categoryA.Id, "Geheim"));
        var categoryB = await AddCategoryAsync(ownerB, "Nur B");

        Assert.Equal(["Nur B"], (await ownerB.GetJsonAsync<CatalogDto>(Catalog)).Categories.Select(c => c.Name));
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.PutAsync($"{Catalog}/services/{serviceA.Id}", NewService(categoryB.Id, "Gehackt"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.DeleteAsync($"{Catalog}/services/{serviceA.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.PutAsync($"{Catalog}/categories/{categoryA.Id}", new SaveCategoryRequest("Gehackt"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.DeleteAsync($"{Catalog}/categories/{categoryA.Id}")).StatusCode);
        // A new service must not be attached to a category of another salon, even though the foreign key would allow it.
        var intruder = await ownerB.PostAsync($"{Catalog}/services", NewService(categoryA.Id, "Eindringling"));
        Assert.Equal(HttpStatusCode.NotFound, intruder.StatusCode);
        Assert.Equal("category.not_found", await intruder.ProblemCodeAsync());
        Assert.Equal(["Geheim"], (await ownerA.GetJsonAsync<CatalogDto>(Catalog)).Categories.Single().Services.Select(s => s.Name));
    }

    [Fact]
    public async Task Moving_a_service_into_a_category_of_another_salon_is_refused()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (ownerA, _) = await host.RegisterOwnerAsync("a@example.com", "Salon A");
        var (ownerB, _) = await host.RegisterOwnerAsync("b@example.com", "Salon B");
        var categoryA = await AddCategoryAsync(ownerA, "Nur A");
        var categoryB = await AddCategoryAsync(ownerB, "Nur B");
        var serviceB = await AddServiceAsync(ownerB, NewService(categoryB.Id));

        var response = await ownerB.PutAsync($"{Catalog}/services/{serviceB.Id}", NewService(categoryA.Id));

        Assert.Equal("category.not_found", await response.ProblemCodeAsync());
        Assert.Empty((await ownerA.GetJsonAsync<CatalogDto>(Catalog)).Categories.Single().Services);
    }

    [Fact]
    public async Task A_template_adds_categories_and_services_in_the_salons_language()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");

        var catalog = await ReadAsync(await owner.PostAsync($"{Catalog}/templates/apply", new ApplyTemplateRequest(null)));

        Assert.Equal(["Damen", "Herren und Kinder", "Styling"], catalog.Categories.Select(c => c.Name));
        Assert.Contains("Haarschnitt Damen", catalog.Categories[0].Services.Select(s => s.Name));
        Assert.All(catalog.Categories.SelectMany(c => c.Services), s => Assert.True(s.IsActive && s.IsBookableOnline && s.DurationMinutes % 5 == 0));
        Assert.Equal(Enumerable.Range(0, catalog.Categories[0].Services.Count), catalog.Categories[0].Services.Select(s => s.SortOrder));
    }

    [Fact]
    public async Task A_template_can_be_chosen_by_industry_and_follows_the_salon_language()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var owner = host.NewSession();
        (await owner.PostAsync("/api/auth/register", AuthTestHost.Registration("maria@example.com", "Berber Salonu", "tr"))).EnsureSuccessStatusCode();

        var catalog = await ReadAsync(await owner.PostAsync($"{Catalog}/templates/apply", new ApplyTemplateRequest(SalonIndustry.Barber)));

        Assert.Equal(["Saç", "Sakal", "Bakım"], catalog.Categories.Select(c => c.Name));
        Assert.Contains("Sıcak havluyla tıraş", catalog.Categories[1].Services.Select(s => s.Name));
    }

    [Fact]
    public async Task Applying_a_template_twice_adds_to_the_catalog_and_an_unknown_industry_is_rejected()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");
        await owner.PostAsync($"{Catalog}/templates/apply", new ApplyTemplateRequest(SalonIndustry.Nails));

        var twice = await ReadAsync(await owner.PostAsync($"{Catalog}/templates/apply", new ApplyTemplateRequest(SalonIndustry.Nails)));
        await owner.RefreshAntiforgeryAsync();
        var unknown = await owner.Client.PostAsJsonAsync($"{Catalog}/templates/apply", new { industry = "Plumbing" });

        Assert.Equal(6, twice.Categories.Count);
        Assert.Equal([0, 1, 2, 3, 4, 5], twice.Categories.Select(c => c.SortOrder));
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Writing_requests_need_the_antiforgery_token()
    {
        await using var host = await AuthTestHost.StartAsync(postgres);
        var (owner, _) = await host.RegisterOwnerAsync("maria@example.com");

        var response = await owner.Client.PostAsJsonAsync($"{Catalog}/categories", new SaveCategoryRequest("X"));

        Assert.Equal("security.antiforgery_failed", await response.ProblemCodeAsync());
    }
}
