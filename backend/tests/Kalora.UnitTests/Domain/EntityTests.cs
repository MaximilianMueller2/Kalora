using Kalora.Domain.Salons;

namespace Kalora.UnitTests.Domain;

public class EntityTests
{
    [Fact]
    public void New_entities_get_time_ordered_version_7_guids()
    {
        var first = new Salon();
        var second = new Salon();

        Assert.Equal(7, first.Id.Version);
        Assert.NotEqual(first.Id, second.Id);
    }
}
