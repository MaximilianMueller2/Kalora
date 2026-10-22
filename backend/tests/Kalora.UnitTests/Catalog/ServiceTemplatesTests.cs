using Kalora.Application.Catalog;
using Kalora.Domain.Salons;

namespace Kalora.UnitTests.Catalog;

public class ServiceTemplatesTests
{
    private static readonly string[] Languages = ["de", "en", "tr"];

    public static TheoryData<SalonIndustry> Industries => [.. Enum.GetValues<SalonIndustry>()];

    [Theory]
    [MemberData(nameof(Industries))]
    public void Every_industry_has_categories_with_services(SalonIndustry industry)
    {
        var categories = ServiceTemplates.For(industry);

        Assert.InRange(categories.Count, 2, 6);
        Assert.All(categories, category => Assert.InRange(category.Services.Count, 2, 8));
    }

    [Theory]
    [MemberData(nameof(Industries))]
    public void Every_name_exists_in_all_languages(SalonIndustry industry)
    {
        var names = ServiceTemplates.For(industry).SelectMany(c => c.Services.Select(s => s.Name).Append(c.Name));

        Assert.All(names, name => Assert.All(Languages, language => Assert.False(string.IsNullOrWhiteSpace(name.GetValueOrDefault(language)), $"{language}: {string.Join('/', name.Values)}")));
    }

    [Theory]
    [MemberData(nameof(Industries))]
    public void Templates_satisfy_the_same_limits_as_services_created_by_hand(SalonIndustry industry)
    {
        var services = ServiceTemplates.For(industry).SelectMany(c => c.Services).ToList();

        Assert.All(services, service =>
        {
            Assert.InRange(service.PriceCents, 0, CatalogRules.MaxPriceCents);
            Assert.True(CatalogRules.IsValidDuration(service.DurationMinutes), service.Name["de"]);
            Assert.True(CatalogRules.IsValidBuffer(service.BufferBeforeMinutes) && CatalogRules.IsValidBuffer(service.BufferAfterMinutes), service.Name["de"]);
            Assert.All(Languages, language => Assert.InRange(service.Name[language].Length, 1, 200));
        });
    }

    [Fact]
    public void Names_within_a_category_are_unique()
    {
        foreach (var industry in Enum.GetValues<SalonIndustry>())
        {
            foreach (var category in ServiceTemplates.For(industry))
            {
                Assert.Equal(category.Services.Count, category.Services.Select(s => s.Name["de"]).Distinct().Count());
            }
        }
    }

    [Fact]
    public void Localize_uses_the_language_and_falls_back_to_german()
    {
        var names = new Dictionary<string, string> { ["de"] = "Haarschnitt", ["en"] = "Haircut" };

        Assert.Equal("Haircut", ServiceTemplates.Localize(names, "en"));
        Assert.Equal("Haarschnitt", ServiceTemplates.Localize(names, "tr"));
        Assert.Equal("Haarschnitt", ServiceTemplates.Localize(names, "fr"));
    }
}
