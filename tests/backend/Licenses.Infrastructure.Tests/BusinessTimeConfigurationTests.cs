using Licenses.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Licenses.Infrastructure.Tests;

public sealed class BusinessTimeConfigurationTests
{
    [Fact]
    public void ValidBusinessTimezoneRegistersBusinessDateProvider()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(Configuration("America/Montevideo"));
        using var provider = services.BuildServiceProvider();

        Assert.Equal("America/Montevideo", provider.GetRequiredService<TimeZoneInfo>().Id);
        Assert.NotNull(provider.GetRequiredService<IBusinessDateProvider>());
    }

    [Fact]
    public void MissingBusinessTimezoneFailsClearly()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(Configuration(null)));

        Assert.Contains("BusinessTime:TimeZoneId", exception.Message);
    }

    [Fact]
    public void InvalidBusinessTimezoneFailsClearly()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(Configuration("Not/A_TimeZone")));

        Assert.Contains("BusinessTime:TimeZoneId", exception.Message);
    }

    private static IConfiguration Configuration(string? timeZoneId)
    {
        var values = new Dictionary<string, string?>();
        if (timeZoneId is not null) values["BusinessTime:TimeZoneId"] = timeZoneId;
        return new FakeConfiguration(values);
    }

    private sealed class FakeConfiguration(IReadOnlyDictionary<string, string?> values) : IConfiguration
    {
        public string? this[string key]
        {
            get => values.TryGetValue(key, out var value) ? value : null;
            set => throw new NotSupportedException();
        }

        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public IChangeToken GetReloadToken() => NoopChangeToken.Instance;
        public IConfigurationSection GetSection(string key) => new FakeSection(values, key);
    }

    private sealed class FakeSection(IReadOnlyDictionary<string, string?> values, string path) : IConfigurationSection
    {
        public string? this[string key]
        {
            get => values.TryGetValue($"{Path}:{key}", out var value) ? value : null;
            set => throw new NotSupportedException();
        }

        public string Key => Path.Split(':').Last();
        public string Path { get; } = path;
        public string? Value
        {
            get => values.TryGetValue(Path, out var value) ? value : null;
            set => throw new NotSupportedException();
        }

        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public IChangeToken GetReloadToken() => NoopChangeToken.Instance;
        public IConfigurationSection GetSection(string key) => new FakeSection(values, $"{Path}:{key}");
    }

    private sealed class NoopChangeToken : IChangeToken
    {
        public static NoopChangeToken Instance { get; } = new();
        public bool HasChanged => false;
        public bool ActiveChangeCallbacks => false;
        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => NoopDisposable.Instance;
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();
        public void Dispose() { }
    }
}
