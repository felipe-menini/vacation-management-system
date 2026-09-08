using Licenses.Domain;

namespace Licenses.Domain.Tests;

public sealed class DomainAssemblyTests
{
    [Fact]
    public void DomainAssemblyMarker_IsAvailable()
    {
        Assert.Equal("Licenses.Domain", typeof(AssemblyMarker).Namespace);
    }
}
