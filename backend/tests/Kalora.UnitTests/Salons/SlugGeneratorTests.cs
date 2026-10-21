using Kalora.Domain.Salons;

namespace Kalora.UnitTests.Salons;

public class SlugGeneratorTests
{
    [Theory]
    [InlineData("Haarwerk Müller", "haarwerk-mueller")]
    [InlineData("Schnitt & Stil", "schnitt-stil")]
    [InlineData("  Barber's   Corner!  ", "barber-s-corner")]
    [InlineData("Straße der Schönheit", "strasse-der-schoenheit")]
    [InlineData("Güzellik Salonu Çiçek", "guezellik-salonu-cicek")]
    [InlineData("Café Coiffure Éclat", "cafe-coiffure-eclat")]
    [InlineData("Nails 24/7", "nails-24-7")]
    public void Slugify_makes_url_safe_ascii(string name, string expected) =>
        Assert.Equal(expected, SlugGenerator.Slugify(name));

    [Theory]
    [InlineData("!!!")]
    [InlineData("A")]
    [InlineData("💇")]
    public void Slugify_always_returns_a_usable_slug_of_at_least_three_characters(string name)
    {
        var slug = SlugGenerator.Slugify(name);

        Assert.True(slug.Length >= 3, slug);
        Assert.Matches("^[a-z0-9-]+$", slug);
    }

    [Fact]
    public void Slugify_limits_the_length_without_a_trailing_hyphen()
    {
        var slug = SlugGenerator.Slugify(new string('a', 39) + " bbbbbbbbbb");

        Assert.True(slug.Length <= SlugGenerator.MaxLength);
        Assert.False(slug.EndsWith('-'));
    }

    [Fact]
    public void Candidates_start_with_the_slug_and_continue_with_numbered_variants_within_the_length_limit()
    {
        var longSlug = new string('a', SlugGenerator.MaxLength);

        var firstThree = SlugGenerator.Candidates("salon").Take(3).ToList();
        var longVariant = SlugGenerator.Candidates(longSlug).Skip(1).First();

        Assert.Equal(["salon", "salon-2", "salon-3"], firstThree);
        Assert.Equal(SlugGenerator.MaxLength, longVariant.Length);
        Assert.EndsWith("-2", longVariant);
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("api", true)]
    [InlineData("m", true)]
    [InlineData("haarwerk", false)]
    public void Reserved_slugs_are_recognised(string slug, bool reserved) =>
        Assert.Equal(reserved, SlugGenerator.IsReserved(slug));
}
