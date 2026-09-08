using Licenses.Domain.Identity;
using Licenses.Domain.Organization;

namespace Licenses.Domain.Tests;

public sealed class OrganizationDomainTests
{
    [Fact]
    public void OrgUnit_CanBeCreatedWithParent()
    {
        var parentId = Guid.NewGuid();
        var unit = OrgUnit.Create("Support", "support", parentId, DateTime.UtcNow);

        Assert.Equal(parentId, unit.ParentId);
        Assert.Equal("SUPPORT", unit.Code);
        Assert.True(unit.IsActive);
    }

    [Fact]
    public void OrgUnit_CannotBeItsOwnParent()
    {
        var unit = OrgUnit.Create("IT", "IT", null, DateTime.UtcNow);

        var ex = Assert.Throws<InvalidOperationException>(() => unit.Update("IT", "IT", unit.Id, true, DateTime.UtcNow));
        Assert.Contains("own parent", ex.Message);
    }

    [Fact]
    public void User_UsesGeneratedInternalIdAndNormalizesEmail()
    {
        var user = User.Create("Felipe", "Felipe@Example.Test", "entra-object-id", DateTime.UtcNow);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("FELIPE@EXAMPLE.TEST", user.Email);
        Assert.Equal("entra-object-id", user.ExternalIdentityId);
    }

    [Fact]
    public void UserOrgAssignment_AllowsHistoricalAssignments()
    {
        var assignment = UserOrgAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), false, DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-1));

        Assert.False(assignment.IsActiveAt(DateTime.UtcNow));
    }
}
