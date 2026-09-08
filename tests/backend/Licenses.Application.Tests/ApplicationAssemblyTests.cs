using Licenses.Application;

namespace Licenses.Application.Tests;

public sealed class ApplicationAssemblyTests
{
    [Fact]
    public void ApplicationAssemblyMarker_IsAvailable()
    {
        Assert.Equal("Licenses.Application", typeof(AssemblyMarker).Namespace);
    }
}
