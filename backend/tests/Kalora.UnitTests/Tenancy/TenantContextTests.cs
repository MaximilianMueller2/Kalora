using Kalora.Application.Tenancy;

namespace Kalora.UnitTests.Tenancy;

public class TenantContextTests
{
    [Fact]
    public void Starts_without_a_tenant()
    {
        Assert.Null(new TenantContext().SalonId);
    }

    [Fact]
    public void Initialize_sets_the_salon_and_allows_repeating_the_same_value()
    {
        var salonId = Guid.NewGuid();
        var context = new TenantContext();

        context.Initialize(salonId);
        context.Initialize(salonId);

        Assert.Equal(salonId, context.SalonId);
    }

    [Fact]
    public void The_tenant_cannot_be_switched_within_a_request()
    {
        var context = new TenantContext();
        context.Initialize(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => context.Initialize(Guid.NewGuid()));
    }
}
